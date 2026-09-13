using System.Text.Json.Serialization;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Platform;

/// <summary>
/// The deliberately small, sanitized contract consumed by the optional GNOME
/// Shell extension. It contains no account, model, transport, or process data.
/// </summary>
public sealed record GnomeTopBarState(
    int SchemaVersion,
    bool HasAntigravity,
    int? GeminiFiveHourRemaining,
    int? GeminiWeeklyRemaining,
    int? ClaudeGptFiveHourRemaining,
    int? ClaudeGptWeeklyRemaining,
    long? LastUpdatedUnixMilliseconds)
{
    public const int CurrentSchemaVersion = 1;
}

public static class GnomeTopBarStateBuilder
{
    public static GnomeTopBarState Build(
        bool antigravityEnabled,
        AntigravityUsageState? state,
        DateTimeOffset? updatedAt)
    {
        if (!antigravityEnabled)
        {
            return Unavailable();
        }

        var gemini = FindPool(state, "gemini-models");
        var claudeGpt = FindPool(state, "claude-and-gpt-models");
        return new GnomeTopBarState(
            GnomeTopBarState.CurrentSchemaVersion,
            HasAntigravity: true,
            gemini?.FiveHour?.RemainingPercent,
            gemini?.Weekly?.RemainingPercent,
            claudeGpt?.FiveHour?.RemainingPercent,
            claudeGpt?.Weekly?.RemainingPercent,
            updatedAt?.ToUnixTimeMilliseconds());
    }

    public static GnomeTopBarState Unavailable() => new(
        GnomeTopBarState.CurrentSchemaVersion,
        HasAntigravity: false,
        GeminiFiveHourRemaining: null,
        GeminiWeeklyRemaining: null,
        ClaudeGptFiveHourRemaining: null,
        ClaudeGptWeeklyRemaining: null,
        LastUpdatedUnixMilliseconds: null);

    private static AntigravityQuotaPoolState? FindPool(
        AntigravityUsageState? state,
        string stableId)
    {
        if (state is null)
        {
            return null;
        }

        return state.QuotaPools.FirstOrDefault(pool =>
            string.Equals(pool.Id, stableId, StringComparison.OrdinalIgnoreCase))
            ?? state.QuotaPools.FirstOrDefault(pool => string.Equals(
                NormalizeId(pool.Name),
                NormalizeId(stableId),
                StringComparison.Ordinal));
    }

    private static string NormalizeId(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }
}
