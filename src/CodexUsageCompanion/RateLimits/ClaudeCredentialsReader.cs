using System.IO;
using System.Text.Json;

namespace CodexUsageCompanion.RateLimits;

public sealed record ClaudeCredentials(
    string AccessToken,
    long ExpiresAtUnixMs,
    string? RefreshToken = null,
    IReadOnlyList<string>? Scopes = null);

public sealed class ClaudeCredentialsMissingException(string message) : Exception(message);

public sealed class ClaudeSessionExpiredException(string message) : Exception(message);

public sealed class ClaudeCredentialsFormatException(string message) : Exception(message);

public static class ClaudeCredentialsReader
{
    // Refresh slightly before the access token lapses, matching the Claude CLI's own lead time.
    public static readonly TimeSpan RefreshLeadTime = TimeSpan.FromSeconds(120);

    private const string MissingMessage = "Claude credentials not found. Run 'claude' to sign in.";
    private const string ExpiredMessage = "Claude session expired. Run 'claude' to refresh your session.";
    private const string FormatMessage = "Unable to parse Claude credentials file.";

    public static ClaudeCredentials Read(string? credentialsPath) =>
        Read(credentialsPath, DateTimeOffset.UtcNow);

    public static ClaudeCredentials Read(string? credentialsPath, DateTimeOffset now)
    {
        var credentials = ReadAllowingExpired(credentialsPath);
        if (IsExpired(credentials, now))
        {
            throw new ClaudeSessionExpiredException(ExpiredMessage);
        }

        return credentials;
    }

    /// <summary>
    /// Reads the credentials without rejecting an expired access token, so the caller can
    /// renew it from the stored refresh token instead of failing outright.
    /// </summary>
    public static ClaudeCredentials ReadAllowingExpired(string? credentialsPath)
    {
        if (string.IsNullOrWhiteSpace(credentialsPath))
        {
            throw new ClaudeCredentialsMissingException(MissingMessage);
        }

        string json;
        try
        {
            json = File.ReadAllText(credentialsPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new ClaudeCredentialsMissingException(MissingMessage);
        }

        return Parse(json);
    }

    public static bool IsExpired(ClaudeCredentials credentials, DateTimeOffset now) =>
        DateTimeOffset.FromUnixTimeMilliseconds(credentials.ExpiresAtUnixMs) <= now;

    public static bool NeedsRefresh(ClaudeCredentials credentials, DateTimeOffset now) =>
        DateTimeOffset.FromUnixTimeMilliseconds(credentials.ExpiresAtUnixMs) - now <= RefreshLeadTime;

    public static ClaudeSessionExpiredException Expired() =>
        new ClaudeSessionExpiredException(ExpiredMessage);

    private static ClaudeCredentials Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var oauth = document.RootElement.GetProperty("claudeAiOauth");
            var accessToken = oauth.GetProperty("accessToken").GetString();
            var expiresAt = oauth.GetProperty("expiresAt").GetInt64();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ClaudeCredentialsFormatException(FormatMessage);
            }

            var refreshToken = oauth.TryGetProperty("refreshToken", out var refreshElement) &&
                refreshElement.ValueKind == JsonValueKind.String
                    ? refreshElement.GetString()
                    : null;

            return new ClaudeCredentials(
                accessToken,
                expiresAt,
                string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken,
                ParseScopes(oauth));
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ClaudeCredentialsFormatException(FormatMessage);
        }
    }

    private static IReadOnlyList<string>? ParseScopes(JsonElement oauth)
    {
        if (!oauth.TryGetProperty("scopes", out var scopes) ||
            scopes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = scopes
            .EnumerateArray()
            .Where(scope => scope.ValueKind == JsonValueKind.String)
            .Select(scope => scope.GetString()!)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .ToArray();
        return values.Length == 0 ? null : values;
    }
}
