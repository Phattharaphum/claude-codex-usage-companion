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
    private const string RetrieveUserQuotaSummaryPath =
        "/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary";
    private const int MaximumResponseBytes = 1_048_576;
    private const int MaximumConcurrentProbes = 6;

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly Func<IReadOnlyList<AntigravityLocalEndpoint>> _discover;
    private readonly TimeSpan _attemptTimeout;
    private readonly TimeSpan _operationTimeout;

    // The transport that answered last time. The language server keeps the same
    // port for as long as it runs, so probing it first makes a refresh a single
    // round trip instead of a scan of every port, scheme and loopback host.
    private ProbeTarget? _preferredTarget;

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
        catch (AntigravityUsageException)
        {
            throw;
        }
        catch (Exception)
        {
            // Discovery failures can carry process details; report only the kind.
            throw new AntigravityUsageException(AntigravityFailureKind.DiscoveryUnavailable);
        }

        if (endpoints.Count == 0)
        {
            throw new AntigravityUsageException(AntigravityFailureKind.NotRunning);
        }

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCancellation.CancelAfter(_operationTimeout);
        var diagnostics = new ProbeDiagnostics();
        var successfulStates = new List<AntigravityUsageState>();
        foreach (var endpoint in endpoints)
        {
            if (operationCancellation.IsCancellationRequested)
            {
                break;
            }

            var state = await ReadEndpointAsync(
                endpoint,
                diagnostics,
                cancellationToken,
                operationCancellation.Token);
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
            throw new AntigravityUsageException(
                operationCancellation.IsCancellationRequested
                    ? AntigravityFailureKind.Timeout
                    : diagnostics.Classify());
        }

        var accounts = successfulStates
            .Select(state => state.Account)
            .Where(account => !string.IsNullOrWhiteSpace(account))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (accounts.Length > 1)
        {
            throw new AntigravityUsageException(AntigravityFailureKind.MultipleAccounts);
        }

        return successfulStates[0] with { ObservedAt = DateTimeOffset.Now };
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
        ProbeDiagnostics diagnostics,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        var probe = new EndpointProbe();
        var targets = BuildTargets(endpoint.Ports);

        var preferred = Volatile.Read(ref _preferredTarget);
        if (preferred is not null && targets.Remove(preferred))
        {
            await ProbeTargetAsync(
                endpoint.CsrfToken,
                preferred,
                probe,
                diagnostics,
                callerCancellationToken,
                operationCancellationToken);
        }

        // HTTPS carries both the metadata and the quota summary, so scan it
        // first. Plain HTTP is only a fallback for the metadata.
        if (!probe.IsComplete)
        {
            await ProbeTargetsAsync(
                endpoint.CsrfToken,
                targets.Where(target => target.Scheme == "https").ToArray(),
                probe,
                diagnostics,
                callerCancellationToken,
                operationCancellationToken);
        }

        if (probe.NeedsMetadata)
        {
            await ProbeTargetsAsync(
                endpoint.CsrfToken,
                targets.Where(target => target.Scheme != "https").ToArray(),
                probe,
                diagnostics,
                callerCancellationToken,
                operationCancellationToken);
        }

        return probe.ToState();
    }

    private static List<ProbeTarget> BuildTargets(IReadOnlyList<int> ports)
    {
        var targets = new List<ProbeTarget>();
        foreach (var port in ports.Order())
        {
            foreach (var scheme in new[] { "https", "http" })
            {
                foreach (var host in new[] { "127.0.0.1", "::1" })
                {
                    targets.Add(new ProbeTarget(scheme, host, port));
                }
            }
        }

        return targets;
    }

    private async Task ProbeTargetsAsync(
        string csrfToken,
        IReadOnlyList<ProbeTarget> targets,
        EndpointProbe probe,
        ProbeDiagnostics diagnostics,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        if (targets.Count == 0 || operationCancellationToken.IsCancellationRequested)
        {
            return;
        }

        // A hung port must not delay the ones that answer, so probe the
        // candidates together and stop as soon as the snapshot is complete.
        using var settled = CancellationTokenSource.CreateLinkedTokenSource(operationCancellationToken);
        try
        {
            await Parallel.ForEachAsync(
                targets,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaximumConcurrentProbes,
                    CancellationToken = settled.Token
                },
                async (target, token) =>
                {
                    await ProbeTargetAsync(
                        csrfToken,
                        target,
                        probe,
                        diagnostics,
                        callerCancellationToken,
                        token);
                    if (probe.IsComplete)
                    {
                        settled.Cancel();
                    }
                });
        }
        catch (OperationCanceledException) when (!callerCancellationToken.IsCancellationRequested)
        {
            // Either the snapshot is complete or the operation budget ran out.
        }
    }

    private async Task ProbeTargetAsync(
        string csrfToken,
        ProbeTarget target,
        EndpointProbe probe,
        ProbeDiagnostics diagnostics,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        if (operationCancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (probe.NeedsMetadata)
        {
            var metadata = await TryReadMetadataAsync(
                csrfToken,
                target,
                diagnostics,
                callerCancellationToken,
                operationCancellationToken);
            if (metadata is not null)
            {
                probe.SetMetadata(metadata);
                Volatile.Write(ref _preferredTarget, target);
            }
        }

        if (target.Scheme == "https" &&
            probe.NeedsQuotaPools &&
            !operationCancellationToken.IsCancellationRequested)
        {
            var quotaPools = await TryReadQuotaSummaryAsync(
                csrfToken,
                target,
                diagnostics,
                callerCancellationToken,
                operationCancellationToken);
            if (quotaPools.Count > 0)
            {
                probe.SetQuotaPools(quotaPools);
                Volatile.Write(ref _preferredTarget, target);
            }
        }
    }

    private async Task<AntigravityUsageState?> TryReadMetadataAsync(
        string csrfToken,
        ProbeTarget target,
        ProbeDiagnostics diagnostics,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellationToken);
        attemptCancellation.CancelAfter(_attemptTimeout);
        try
        {
            using var request = CreateUserStatusRequest(target.Scheme, target.Host, target.Port, csrfToken);
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                attemptCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                diagnostics.RecordStatus(response.StatusCode);
                return null;
            }

            var json = await ReadContentAsync(response.Content, attemptCancellation.Token);
            var state = AntigravityUsageParser.ParseResponse(json);
            if (state.Models.Count == 0)
            {
                diagnostics.RecordInvalidPayload();
                return null;
            }

            return state;
        }
        catch (Exception exception) when (
            diagnostics.TryRecordFailure(
                exception,
                callerCancellationToken,
                operationCancellationToken,
                attemptCancellation.Token))
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<AntigravityQuotaPoolState>> TryReadQuotaSummaryAsync(
        string csrfToken,
        ProbeTarget target,
        ProbeDiagnostics diagnostics,
        CancellationToken callerCancellationToken,
        CancellationToken operationCancellationToken)
    {
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(operationCancellationToken);
        attemptCancellation.CancelAfter(_attemptTimeout);
        try
        {
            using var request = CreateQuotaSummaryRequest(target.Scheme, target.Host, target.Port, csrfToken);
            using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                attemptCancellation.Token);
            if (!response.IsSuccessStatusCode)
            {
                diagnostics.RecordStatus(response.StatusCode);
                return [];
            }

            var json = await ReadContentAsync(response.Content, attemptCancellation.Token);
            return AntigravityQuotaSummaryParser.ParseResponse(json);
        }
        catch (Exception exception) when (
            diagnostics.TryRecordFailure(
                exception,
                callerCancellationToken,
                operationCancellationToken,
                attemptCancellation.Token))
        {
            return [];
        }
    }

    private static HttpRequestMessage CreateUserStatusRequest(string scheme, string host, int port, string csrfToken)
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

    private static HttpRequestMessage CreateQuotaSummaryRequest(string scheme, string host, int port, string csrfToken)
    {
        var uri = new UriBuilder(scheme, host, port, RetrieveUserQuotaSummaryPath).Uri;
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent("{\"forceRefresh\":true}", Encoding.UTF8, "application/json")
        };
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

    private sealed record ProbeTarget(string Scheme, string Host, int Port);

    // What one endpoint's probes have found so far. Probes run concurrently.
    private sealed class EndpointProbe
    {
        private readonly object _gate = new();
        private AntigravityUsageState? _metadata;
        private IReadOnlyList<AntigravityQuotaPoolState> _quotaPools = [];

        internal bool NeedsMetadata
        {
            get { lock (_gate) { return _metadata is null; } }
        }

        internal bool NeedsQuotaPools
        {
            get { lock (_gate) { return _quotaPools.Count == 0; } }
        }

        internal bool IsComplete
        {
            get { lock (_gate) { return _metadata is not null && _quotaPools.Count > 0; } }
        }

        internal void SetMetadata(AntigravityUsageState metadata)
        {
            lock (_gate)
            {
                _metadata ??= metadata;
            }
        }

        internal void SetQuotaPools(IReadOnlyList<AntigravityQuotaPoolState> quotaPools)
        {
            lock (_gate)
            {
                if (_quotaPools.Count == 0)
                {
                    _quotaPools = quotaPools;
                }
            }
        }

        internal AntigravityUsageState? ToState()
        {
            lock (_gate)
            {
                if (_metadata is not null)
                {
                    return _quotaPools.Count > 0
                        ? _metadata with { QuotaPools = _quotaPools }
                        : _metadata;
                }

                return _quotaPools.Count > 0
                    ? new AntigravityUsageState(null, null, [], _quotaPools)
                    : null;
            }
        }
    }

    // Individual probes are expected to fail (wrong scheme, closed port), so
    // they only leave a hint; the hints pick the message when nothing answered.
    private sealed class ProbeDiagnostics
    {
        private int _authRejected;
        private int _invalidPayload;
        private int _timedOut;

        internal void RecordStatus(HttpStatusCode status)
        {
            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                Volatile.Write(ref _authRejected, 1);
            }
        }

        internal void RecordInvalidPayload() => Volatile.Write(ref _invalidPayload, 1);

        // Exception filter: records a probe failure and returns true when the
        // probe may simply report "no data". Caller cancellation is rethrown.
        internal bool TryRecordFailure(
            Exception exception,
            CancellationToken callerCancellationToken,
            CancellationToken operationCancellationToken,
            CancellationToken attemptCancellationToken)
        {
            switch (exception)
            {
                case OperationCanceledException when callerCancellationToken.IsCancellationRequested:
                    return false;
                case OperationCanceledException:
                    if (attemptCancellationToken.IsCancellationRequested &&
                        !operationCancellationToken.IsCancellationRequested)
                    {
                        Volatile.Write(ref _timedOut, 1);
                    }

                    return true;
                case JsonException or InvalidDataException:
                    RecordInvalidPayload();
                    return true;
                case HttpRequestException or IOException:
                    return true;
                default:
                    return false;
            }
        }

        internal AntigravityFailureKind Classify()
        {
            if (Volatile.Read(ref _authRejected) != 0)
            {
                return AntigravityFailureKind.AuthRejected;
            }

            if (Volatile.Read(ref _invalidPayload) != 0)
            {
                return AntigravityFailureKind.InvalidResponse;
            }

            return Volatile.Read(ref _timedOut) != 0
                ? AntigravityFailureKind.Timeout
                : AntigravityFailureKind.Unreachable;
        }
    }
}
