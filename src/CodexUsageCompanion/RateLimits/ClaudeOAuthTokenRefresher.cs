using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CodexUsageCompanion.RateLimits;

public sealed record ClaudeRefreshedTokens(
    string AccessToken,
    string RefreshToken,
    long ExpiresAtUnixMs,
    long? RefreshTokenExpiresAtUnixMs,
    IReadOnlyList<string>? Scopes);

/// <summary>
/// Renews an expired Claude access token with the OAuth refresh grant the Claude CLI uses.
/// </summary>
public sealed class ClaudeOAuthTokenRefresher
{
    public const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    private const string ReauthMessage =
        "Claude session could not be renewed. Run 'claude' to sign in again.";

    private readonly HttpClient _http;

    public ClaudeOAuthTokenRefresher(HttpClient http)
    {
        _http = http;
    }

    public async Task<ClaudeRefreshedTokens> RefreshAsync(
        ClaudeCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            throw ClaudeCredentialsReader.Expired();
        }

        var payload = new JsonObject
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = credentials.RefreshToken,
            ["client_id"] = ClientId
        };
        if (credentials.Scopes is { Count: > 0 } scopes)
        {
            payload["scope"] = string.Join(' ', scopes);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await _http.SendAsync(request, cancellationToken);

        // The grant is rejected once the refresh token itself lapses or is revoked; only a
        // fresh CLI sign-in can recover from that, so say so instead of retrying forever.
        if (response.StatusCode is HttpStatusCode.BadRequest or
            HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden)
        {
            throw new ClaudeSessionExpiredException(ReauthMessage);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(body, credentials.RefreshToken!);
    }

    internal static ClaudeRefreshedTokens Parse(string body, string previousRefreshToken)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var accessToken = root.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ClaudeCredentialsFormatException("Claude token refresh returned no access token.");
            }

            // Without a lifetime the new token would look expired on arrival and loop the refresh.
            if (ReadSeconds(root, "expires_in") is not { } lifetimeSeconds || lifetimeSeconds <= 0)
            {
                throw new ClaudeCredentialsFormatException("Claude token refresh returned no expiry.");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var refreshToken = root.TryGetProperty("refresh_token", out var refreshElement) &&
                refreshElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(refreshElement.GetString())
                    ? refreshElement.GetString()!
                    : previousRefreshToken;

            return new ClaudeRefreshedTokens(
                accessToken,
                refreshToken,
                now + (lifetimeSeconds * 1000),
                ReadSeconds(root, "refresh_token_expires_in") is { } refreshSeconds
                    ? now + (refreshSeconds * 1000)
                    : null,
                ReadScope(root));
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ClaudeCredentialsFormatException("Unable to parse the Claude token refresh response.");
        }
    }

    private static long? ReadSeconds(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element) &&
        element.ValueKind == JsonValueKind.Number &&
        element.TryGetInt64(out var seconds)
            ? seconds
            : null;

    private static IReadOnlyList<string>? ReadScope(JsonElement root)
    {
        if (!root.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var values = (scope.GetString() ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return values.Length == 0 ? null : values;
    }
}
