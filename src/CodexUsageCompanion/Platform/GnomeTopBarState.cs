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
    public const int CurrentSchemaVersion = 2;

    // Schema 2 adds the three compact meters used by the GNOME Shell
    // indicator. Keep the individual Antigravity pools above so a future
    // detailed view does not need to infer anything from a displayed value.
    public bool HasClaude { get; init; }
    public int? ClaudeFiveHourRemaining { get; init; }
    public bool HasCodex { get; init; }
    public int? CodexFiveHourRemaining { get; init; }
    public int? AntigravityRemaining { get; init; }
}

public static class GnomeTopBarStateBuilder
{
    public static GnomeTopBarState Build(
        bool antigravityEnabled,
        AntigravityUsageState? state,
        DateTimeOffset? updatedAt,
        RateLimitState? claude = null,
        RateLimitState? codex = null)
    {
        var gemini = antigravityEnabled ? FindPool(state, "gemini-models") : null;
        var claudeGpt = antigravityEnabled ? FindPool(state, "claude-and-gpt-models") : null;
        var antigravityValues = new[]
            {
                gemini?.FiveHour?.RemainingPercent,
                claudeGpt?.FiveHour?.RemainingPercent
            }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        int? antigravityRemaining = antigravityValues.Length == 0
            ? null
            : antigravityValues.Min();

        return new GnomeTopBarState(
            GnomeTopBarState.CurrentSchemaVersion,
            HasAntigravity: antigravityEnabled,
            gemini?.FiveHour?.RemainingPercent,
            gemini?.Weekly?.RemainingPercent,
            claudeGpt?.FiveHour?.RemainingPercent,
            claudeGpt?.Weekly?.RemainingPercent,
            antigravityEnabled ? updatedAt?.ToUnixTimeMilliseconds() : null)
        {
            HasClaude = claude?.FiveHour is not null,
            ClaudeFiveHourRemaining = claude?.FiveHour?.RemainingPercent,
            HasCodex = codex?.FiveHour is not null,
            CodexFiveHourRemaining = codex?.FiveHour?.RemainingPercent,
            AntigravityRemaining = antigravityRemaining
        };
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
