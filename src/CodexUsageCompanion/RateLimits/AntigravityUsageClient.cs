using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodexUsageCompanion.Lifecycle;

namespace CodexUsageCompanion.RateLimits;

internal sealed class AntigravityUsageClient : IAntigravityUsageReader
{
    private const string GetUserStatusPath =
        "/exa.language_server_pb.LanguageServerService/GetUserStatus";
    private const string UnavailableMessage = "Antigravity local usage service was unavailable.";
    private const int MaximumResponseBytes = 1_048_576;

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly Func<IReadOnlyList<AntigravityLocalEndpoint>> _discover;
    private readonly TimeSpan _attemptTimeout;
    private readonly TimeSpan _operationTimeout;

    internal AntigravityUsageClient(
        HttpClient? httpClient = null,
        Func<IReadOnlyList<AntigravityLocalEndpoint>>? discover = null,
        TimeSpan? attemptTimeout = null,
        TimeSpan? operationTimeout = null)
    {
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? CreateLocalHttpClient();
        _discover = discover ?? AntigravityProcessDiscovery.Discover;
        _attemptTimeout = attemptTimeout ?? TimeSpan.FromSeconds(3);
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(15);
    }

    internal async Task<AntigravityUsageState> ReadUsageAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AntigravityLocalEndpoint> endpoints;
        try
        {
            endpoints = _discover();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            throw Unavailable();
        }

        if (endpoints.Count == 0)
        {
            throw Unavailable();
        }

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCancellation.CancelAfter(_operationTimeout);
        var successfulStates = new List<AntigravityUsageState>();
        foreach (var endpoint in endpoints)
        {
            if (operationCancellation.IsCancellationRequested)
            {
                break;
            }

            var state = await ReadEndpointAsync(endpoint, cancellationToken, operationCancellation.Token);
            if (state is not null)
            {
                successfulStates.Add(state);
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (successfulStates.Count == 0)
        {
            throw Unavailable();
        }

        var accounts = successfulStates
            .Select(state => state.Account)
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (accounts.Length > 1)
        {
            throw new InvalidOperationException("Multiple Antigravity accounts are active.");
        }

        return successfulStates[0];
    }

    Task<AntigravityUsageState> IAntigravityUsageReader.ReadUsageAsync(
        CancellationToken cancellationToken) => ReadUsageAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<AntigravityUsageState?> ReadEndpointAsync(
        AntigravityLocalEndpoint endpoint,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        foreach (var port in endpoint.Ports.Order())
        {
            foreach (var scheme in new[] { "https", "http" })
            {
                foreach (var host in new[] { "127.0.0.1", "::1" })
                {
                    if (operationCancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    var state = await TryReadAsync(
                        endpoint.CsrfToken,
                        scheme,
                        host,
                        port,
                        callerCancellationToken,
                        operationCancellationToken);
                    if (state is not null)
                    {
                        return state;
                    }
                }
            }
        }

        return null;
    }

    private async Task<AntigravityUsageState?> TryReadAsync(
        string csrfToken,
        string scheme,
        string host,
        int port,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellationToken);
        attemptCancellation.CancelAfter(_attemptTimeout);
        try
        {
            using var request = CreateRequest(scheme, host, port, csrfToken);
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                attemptCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await ReadContentAsync(response.Content, attemptCancellation.Token);
            var state = AntigravityUsageParser.ParseResponse(json);
            return state.Models.Count > 0 ? state : null;
        }
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or IOException or InvalidDataException)
        {
            return null;
        }
    }

    private static HttpRequestMessage CreateRequest(string scheme, string host, int port, string csrfToken)
    {
        var uri = new UriBuilder(scheme, host, port, GetUserStatusPath).Uri;
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    metadata = new
                    {
                        ideName = "antigravity",
                        extensionName = "antigravity",
                        locale = "en"
                    }
                }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("Connect-Protocol-Version", "1");
        request.Headers.TryAddWithoutValidation("X-Codeium-Csrf-Token", csrfToken);
        return request;
    }

    private static async Task<string> ReadContentAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (buffer.Length + read > MaximumResponseBytes)
            {
                throw new InvalidDataException("Antigravity usage response exceeded the local size limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private static HttpClient CreateLocalHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = static (request, _, _, _) =>
                request.RequestUri is { } uri && IsNumericLoopback(uri)
        };
        return new HttpClient(handler);
    }

    private static bool IsNumericLoopback(Uri uri) =>
        IPAddress.TryParse(uri.DnsSafeHost, out var address) && IPAddress.IsLoopback(address);

    private static InvalidOperationException Unavailable() => new(UnavailableMessage);
}
