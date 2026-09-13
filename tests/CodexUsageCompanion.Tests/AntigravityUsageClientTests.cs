using System.Net;
using System.Net.Http;
using System.Text.Json;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class AntigravityUsageClientTests
{
    private const string Secret = "test-csrf-secret-do-not-log";

    [Fact]
    public async Task ReadUsageAsyncUsesHttpsFirstAndSendsTheValidatedRequest()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new StubHandler((request, _) =>
        {
            captured = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response();
        });
        await using var client = Client(handler, Endpoint(37017));

        var state = await client.ReadUsageAsync(CancellationToken.None);

        Assert.Equal("sanitized@example.invalid", state.Account);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("https", captured.RequestUri?.Scheme);
        Assert.Equal("127.0.0.1", captured.RequestUri?.Host);
        Assert.Equal(37017, captured.RequestUri?.Port);
        Assert.Equal(
            "/exa.language_server_pb.LanguageServerService/GetUserStatus",
            captured.RequestUri?.AbsolutePath);
        Assert.Equal("1", Assert.Single(captured.Headers.GetValues("Connect-Protocol-Version")));
        Assert.Equal(Secret, Assert.Single(captured.Headers.GetValues("X-Codeium-Csrf-Token")));
        Assert.Contains("application/json", captured.Headers.Accept.ToString());

        using var body = JsonDocument.Parse(capturedBody!);
        var metadata = body.RootElement.GetProperty("metadata");
        Assert.Equal("antigravity", metadata.GetProperty("ideName").GetString());
        Assert.Equal("antigravity", metadata.GetProperty("extensionName").GetString());
        Assert.Equal("en", metadata.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task ReadUsageAsyncTriesNextPortAfterConnectionFailure()
    {
        var attempts = new List<string>();
        var handler = new StubHandler((request, _) =>
        {
            attempts.Add(RequestKey(request));
            return request.RequestUri!.Port == 38507
                ? Response()
                : throw new HttpRequestException("connection refused");
        });
        await using var client = Client(handler, Endpoint(38507, 37017));

        var state = await client.ReadUsageAsync(CancellationToken.None);

        Assert.Single(state.Models);
        Assert.Equal(
            [
                "https://127.0.0.1:37017",
                "https://[::1]:37017",
                "http://127.0.0.1:37017",
                "http://[::1]:37017",
                "https://127.0.0.1:38507"
            ],
            attempts);
    }

    [Fact]
    public async Task ReadUsageAsyncFallsBackToHttpAfterHttpsFails()
    {
        var attempts = new List<string>();
        var handler = new StubHandler((request, _) =>
        {
            attempts.Add(RequestKey(request));
            return request.RequestUri!.Scheme == "http" && request.RequestUri.Host == "127.0.0.1"
                ? Response()
                : throw new HttpRequestException("unavailable");
        });
        await using var client = Client(handler, Endpoint(37017));

        await client.ReadUsageAsync(CancellationToken.None);

        Assert.Equal(
            [
                "https://127.0.0.1:37017",
                "https://[::1]:37017",
                "http://127.0.0.1:37017"
            ],
            attempts);
    }

    [Fact]
    public async Task ReadUsageAsyncProbesPastNonSuccessMalformedAndNoQuotaResponses()
    {
        var handler = new StubHandler((request, _) => request.RequestUri!.Port switch
        {
            37017 => new HttpResponseMessage(HttpStatusCode.NotFound),
            38507 => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{")
            },
            41011 => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "userStatus": {} }""")
            },
            _ => Response()
        });
        await using var client = Client(handler, Endpoint(37017, 38507, 41011, 42000));

        var state = await client.ReadUsageAsync(CancellationToken.None);

        Assert.Single(state.Models);
    }

    [Fact]
    public async Task ReadUsageAsyncProbesPastTimeouts()
    {
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.Port == 37017)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return Response();
        });
        await using var client = ClientWithTimeout(
            handler,
            TimeSpan.FromMilliseconds(25),
            Endpoint(37017, 38507));

        var state = await client.ReadUsageAsync(CancellationToken.None);

        Assert.Single(state.Models);
    }

    [Fact]
    public async Task ReadUsageAsyncPropagatesCallerCancellation()
    {
        var handler = new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Response();
        });
        await using var client = ClientWithTimeout(
            handler,
            TimeSpan.FromSeconds(5),
            Endpoint(37017));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.ReadUsageAsync(cancellation.Token));
    }

    [Fact]
    public async Task ReadUsageAsyncReturnsSanitizedUnavailableFailure()
    {
        await using var noCandidates = Client(new StubHandler((_, _) => Response()));
        var noCandidatesError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => noCandidates.ReadUsageAsync(CancellationToken.None));

        await using var unreachable = Client(
            new StubHandler((Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>)
                ((_, _) => throw new HttpRequestException(Secret))),
            Endpoint(37017));
        var unreachableError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unreachable.ReadUsageAsync(CancellationToken.None));

        Assert.Equal("Antigravity local usage service was unavailable.", noCandidatesError.Message);
        Assert.Equal("Antigravity local usage service was unavailable.", unreachableError.Message);
        Assert.DoesNotContain(Secret, unreachableError.Message);
    }

    [Fact]
    public async Task ReadUsageAsyncTreatsSameAccountProcessesAsDuplicateLocalInstances()
    {
        var handler = new StubHandler((request, _) => request.RequestUri!.Port == 37017
            ? Response("same@example.invalid")
            : Response("same@example.invalid"));
        await using var client = Client(handler, Endpoint(37017), Endpoint(38507));

        var state = await client.ReadUsageAsync(CancellationToken.None);

        Assert.Equal("same@example.invalid", state.Account);
    }

    [Fact]
    public async Task ReadUsageAsyncRejectsDistinctAccounts()
    {
        var handler = new StubHandler((request, _) => request.RequestUri!.Port == 37017
            ? Response("first@example.invalid")
            : Response("second@example.invalid"));
        await using var client = Client(handler, Endpoint(37017), Endpoint(38507));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ReadUsageAsync(CancellationToken.None));

        Assert.Equal("Multiple Antigravity accounts are active.", exception.Message);
        Assert.DoesNotContain(Secret, exception.Message);
    }

    private static AntigravityUsageClient Client(
        HttpMessageHandler handler,
        params AntigravityLocalEndpoint[] endpoints) =>
        ClientWithTimeout(handler, TimeSpan.FromSeconds(3), endpoints);

    private static AntigravityUsageClient ClientWithTimeout(
        HttpMessageHandler handler,
        TimeSpan attemptTimeout,
        params AntigravityLocalEndpoint[] endpoints) => new(
        new HttpClient(handler),
        () => endpoints,
        attemptTimeout,
        TimeSpan.FromSeconds(2));

    private static AntigravityLocalEndpoint Endpoint(params int[] ports) =>
        new(71740, Secret, ports);

    private static HttpResponseMessage Response(string account = "sanitized@example.invalid") =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
                {
                  "userStatus": {
                    "email": "{{account}}",
                    "cascadeModelConfigData": {
                      "clientModelConfigs": [
                        {
                          "label": "Gemini",
                          "modelOrAlias": { "model": "gemini" },
                          "quotaInfo": { "remainingFraction": 0.5 }
                        }
                      ]
                    }
                  }
                }
                """)
        };

    private static string RequestKey(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        var host = IPAddress.TryParse(uri.DnsSafeHost, out var address) &&
                   address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{uri.DnsSafeHost}]"
            : uri.DnsSafeHost;
        return $"{uri.Scheme}://{host}:{uri.Port}";
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        internal StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
            : this((request, cancellationToken) => Task.FromResult(respond(request, cancellationToken)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => respond(request, cancellationToken);
    }
}
