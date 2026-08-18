using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CodexUsageCompanion.RateLimits;

public sealed class ClaudeUsageClient : IAsyncDisposable
{
    private const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";
    private static readonly TimeSpan RefreshLockTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly Func<string?> _locateCredentials;
    private readonly Func<string> _resolveUserAgent;
    private readonly ClaudeOAuthTokenRefresher _refresher;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public ClaudeUsageClient(
        HttpClient? httpClient = null,
        Func<string?>? locateCredentials = null,
        Func<string>? resolveUserAgent = null,
        ClaudeOAuthTokenRefresher? tokenRefresher = null)
    {
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _locateCredentials = locateCredentials ?? ClaudeCredentialsLocator.Find;
        _resolveUserAgent = resolveUserAgent ?? ClaudeUserAgentResolver.Resolve;
        _refresher = tokenRefresher ?? new ClaudeOAuthTokenRefresher(_http);
    }

    public async Task<RateLimitState> ReadUsageAsync(CancellationToken cancellationToken)
    {
        var credentialsPath = _locateCredentials();
        var credentials = ClaudeCredentialsReader.ReadAllowingExpired(credentialsPath);

        // The CLI only renews its token while it runs, so after a reboot the stored one is
        // routinely stale; renew it here instead of stalling until the user opens Claude Code.
        var refreshed = false;
        if (ClaudeCredentialsReader.NeedsRefresh(credentials, DateTimeOffset.UtcNow))
        {
            credentials = await RenewAsync(credentialsPath!, credentials, cancellationToken);
            refreshed = true;
        }

        using var response = await SendUsageRequestAsync(credentials.AccessToken, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
        {
            response.EnsureSuccessStatusCode();
            return ClaudeUsageParser.ParseResponse(
                await response.Content.ReadAsStringAsync(cancellationToken));
        }

        // A token the server rejects earlier than its recorded expiry still deserves one retry.
        if (refreshed || credentials.RefreshToken is null)
        {
            throw ClaudeCredentialsReader.Expired();
        }

        credentials = await RenewAsync(credentialsPath!, credentials, cancellationToken, force: true);
        using var retry = await SendUsageRequestAsync(credentials.AccessToken, cancellationToken);
        if (retry.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw ClaudeCredentialsReader.Expired();
        }

        retry.EnsureSuccessStatusCode();
        return ClaudeUsageParser.ParseResponse(
            await retry.Content.ReadAsStringAsync(cancellationToken));
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }

        _refreshGate.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<HttpResponseMessage> SendUsageRequestAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        request.Headers.UserAgent.ParseAdd(_resolveUserAgent());
        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task<ClaudeCredentials> RenewAsync(
        string credentialsPath,
        ClaudeCredentials used,
        CancellationToken cancellationToken,
        bool force = false)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            using var fileLock = ClaudeCredentialsRefreshLock.TryAcquire(
                credentialsPath,
                RefreshLockTimeout);

            // Whether or not the lock was won, the CLI or another instance may have renewed
            // the file in the meantime; a token that is no longer the one we used is theirs.
            var current = ReadOrFallback(credentialsPath, used);
            if (!string.Equals(current.AccessToken, used.AccessToken, StringComparison.Ordinal) ||
                (!force && !ClaudeCredentialsReader.NeedsRefresh(current, DateTimeOffset.UtcNow)))
            {
                return current;
            }

            if (fileLock is null)
            {
                throw new ClaudeSessionExpiredException(
                    "Claude session is being refreshed elsewhere. Usage will update shortly.");
            }

            var tokens = await _refresher.RefreshAsync(current, cancellationToken);
            ClaudeCredentialsWriter.Write(credentialsPath, tokens);
            return new ClaudeCredentials(
                tokens.AccessToken,
                tokens.ExpiresAtUnixMs,
                tokens.RefreshToken,
                tokens.Scopes ?? current.Scopes);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static ClaudeCredentials ReadOrFallback(string credentialsPath, ClaudeCredentials fallback)
    {
        try
        {
            return ClaudeCredentialsReader.ReadAllowingExpired(credentialsPath);
        }
        catch (Exception exception) when (
            exception is ClaudeCredentialsMissingException or ClaudeCredentialsFormatException)
        {
            return fallback;
        }
    }
}
