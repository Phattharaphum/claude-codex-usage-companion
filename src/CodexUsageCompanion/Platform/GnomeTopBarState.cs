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
    public const int CurrentSchemaVersion = 4;

    // Schema 2 added the three compact meters used by the GNOME Shell
    // indicator. Schema 3 added weekly values and reset times. Schema 4 keeps
    // each Antigravity quota pool separate so all four panel rings are honest.
    // Keep the individual Antigravity pools above for backwards compatibility.
    public bool HasClaude { get; init; }
    public int? ClaudeFiveHourRemaining { get; init; }
    public bool HasCodex { get; init; }
    public int? CodexFiveHourRemaining { get; init; }
    public int? AntigravityRemaining { get; init; }
    public int? ClaudeWeeklyRemaining { get; init; }
    public int? CodexWeeklyRemaining { get; init; }
    public int? AntigravityWeeklyRemaining { get; init; }
    public long? ClaudeFiveHourResetUnixMilliseconds { get; init; }
    public long? CodexFiveHourResetUnixMilliseconds { get; init; }
    public long? AntigravityFiveHourResetUnixMilliseconds { get; init; }
    public long? ClaudeWeeklyResetUnixMilliseconds { get; init; }
    public long? CodexWeeklyResetUnixMilliseconds { get; init; }
    public long? AntigravityWeeklyResetUnixMilliseconds { get; init; }
    public long? GeminiFiveHourResetUnixMilliseconds { get; init; }
    public long? GeminiWeeklyResetUnixMilliseconds { get; init; }
    public long? ClaudeGptFiveHourResetUnixMilliseconds { get; init; }
    public long? ClaudeGptWeeklyResetUnixMilliseconds { get; init; }
    public long? PublishedAtUnixMilliseconds { get; init; }
}

public static class GnomeTopBarStateBuilder
{
    public static GnomeTopBarState Build(
        bool antigravityEnabled,
        AntigravityUsageState? state,
        DateTimeOffset? updatedAt,
        RateLimitState? claude = null,
        RateLimitState? codex = null,
        DateTimeOffset? claudeUpdatedAt = null,
        DateTimeOffset? codexUpdatedAt = null)
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
        var antigravityWeeklyValues = new[]
            {
                gemini?.Weekly?.RemainingPercent,
                claudeGpt?.Weekly?.RemainingPercent
            }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
        int? antigravityWeeklyRemaining = antigravityWeeklyValues.Length == 0
            ? null
            : antigravityWeeklyValues.Min();
        var antigravityFiveHourReset = new[] { gemini?.FiveHour, claudeGpt?.FiveHour }
            .Where(window => window is not null)
            .OrderBy(window => window!.RemainingPercent)
            .FirstOrDefault()
            ?.ResetAt;
        var antigravityWeeklyReset = new[] { gemini?.Weekly, claudeGpt?.Weekly }
            .Where(window => window is not null)
            .OrderBy(window => window!.RemainingPercent)
            .FirstOrDefault()
            ?.ResetAt;
        var latestUpdatedAt = new[]
            {
                antigravityEnabled && state is not null ? updatedAt : null,
                claudeUpdatedAt,
                codexUpdatedAt
            }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();

        return new GnomeTopBarState(
            GnomeTopBarState.CurrentSchemaVersion,
            HasAntigravity: antigravityEnabled,
            gemini?.FiveHour?.RemainingPercent,
            gemini?.Weekly?.RemainingPercent,
            claudeGpt?.FiveHour?.RemainingPercent,
            claudeGpt?.Weekly?.RemainingPercent,
            latestUpdatedAt == default ? null : latestUpdatedAt.ToUnixTimeMilliseconds())
        {
            HasClaude = claude?.FiveHour is not null || claude?.Weekly is not null,
            ClaudeFiveHourRemaining = claude?.FiveHour?.RemainingPercent,
            HasCodex = codex?.FiveHour is not null || codex?.Weekly is not null,
            CodexFiveHourRemaining = codex?.FiveHour?.RemainingPercent,
            AntigravityRemaining = antigravityRemaining,
            ClaudeWeeklyRemaining = claude?.Weekly?.RemainingPercent,
            CodexWeeklyRemaining = codex?.Weekly?.RemainingPercent,
            AntigravityWeeklyRemaining = antigravityWeeklyRemaining,
            ClaudeFiveHourResetUnixMilliseconds = UnixMilliseconds(claude?.FiveHour),
            CodexFiveHourResetUnixMilliseconds = UnixMilliseconds(codex?.FiveHour),
            AntigravityFiveHourResetUnixMilliseconds = antigravityFiveHourReset?.ToUnixTimeMilliseconds(),
            ClaudeWeeklyResetUnixMilliseconds = UnixMilliseconds(claude?.Weekly),
            CodexWeeklyResetUnixMilliseconds = UnixMilliseconds(codex?.Weekly),
            AntigravityWeeklyResetUnixMilliseconds = antigravityWeeklyReset?.ToUnixTimeMilliseconds(),
            GeminiFiveHourResetUnixMilliseconds = UnixMilliseconds(gemini?.FiveHour),
            GeminiWeeklyResetUnixMilliseconds = UnixMilliseconds(gemini?.Weekly),
            ClaudeGptFiveHourResetUnixMilliseconds = UnixMilliseconds(claudeGpt?.FiveHour),
            ClaudeGptWeeklyResetUnixMilliseconds = UnixMilliseconds(claudeGpt?.Weekly)
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

    private static long? UnixMilliseconds(RateLimitWindowState? window) =>
        window?.ResetsAt is long seconds
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).ToUnixTimeMilliseconds()
            : null;

    private static long? UnixMilliseconds(AntigravityQuotaWindowState? window) =>
        window?.ResetAt?.ToUnixTimeMilliseconds();
}
