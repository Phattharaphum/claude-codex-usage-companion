namespace CodexUsageCompanion.Diagnostics;

public enum UsageResetKind
{
    FiveHour,
    Weekly,
    Both
}

public sealed record UsageResetEvent(
    string Provider,
    UsageResetKind Kind,
    DateTimeOffset ExpectedAt,
    DateTimeOffset ObservedAt,
    DateTimeOffset PreviousObservationAt,
    int? FiveHourRemainingBefore,
    int? WeeklyRemainingBefore,
    int? FiveHourRemainingAfter,
    int? WeeklyRemainingAfter)
{
    public TimeSpan ObservationGap => ObservedAt - PreviousObservationAt;
}

public sealed record UsageWindowMetrics(
    UsageHistoryQuotaWindow Window,
    int SampleCount,
    int ResetCount,
    double AverageRemainingPercent,
    double ConsumedPercent,
    double ConsumptionPerHour,
    int? FirstRemainingPercent,
    int? LastRemainingPercent);

public sealed record UsageSelectionSummary(
    int RecordCount,
    int ProviderCount,
    TimeSpan ObservedDuration,
    UsageWindowMetrics FiveHour,
    UsageWindowMetrics Weekly);

/// <summary>
/// Computes reset-cycle and selected-period statistics from successful local
/// history records. Consumption totals only downward quota movement; quota
/// recovery at a detected reset is deliberately excluded.
/// </summary>
public static class UsageHistoryAnalytics
{
    private static readonly string[] SupportedProviders =
    [
        "claude",
        "codex",
        "antigravity-gemini",
        "antigravity-claudeandchatgpt"
    ];

    public static IReadOnlyList<UsageHistoryEntry> SelectEntries(
        IReadOnlyList<UsageHistoryEntry> entries,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlySet<string>? providers = null) => entries
        .Where(entry =>
            entry.UpdatedAt >= start &&
            entry.UpdatedAt <= end &&
            string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase) &&
            SupportedProviders.Contains(entry.Provider, StringComparer.OrdinalIgnoreCase) &&
            (providers is null || providers.Contains(entry.Provider)))
        .OrderByDescending(entry => entry.UpdatedAt)
        .ToArray();

    public static UsageSelectionSummary Summarize(IReadOnlyList<UsageHistoryEntry> entries)
    {
        var successful = entries
            .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.UpdatedAt)
            .ToArray();
        var duration = successful.Length < 2
            ? TimeSpan.Zero
            : successful[^1].UpdatedAt - successful[0].UpdatedAt;
        return new UsageSelectionSummary(
            successful.Length,
            successful.Select(entry => entry.Provider).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            duration,
            Metrics(successful, UsageHistoryQuotaWindow.FiveHour),
            Metrics(successful, UsageHistoryQuotaWindow.Weekly));
    }

    public static IReadOnlyList<UsageResetEvent> FindResetEvents(
        IReadOnlyList<UsageHistoryEntry> entries,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        IReadOnlySet<string>? providers = null)
    {
        var events = new List<UsageResetEvent>();
        var candidates = entries
            .Where(entry =>
                string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase) &&
                SupportedProviders.Contains(entry.Provider, StringComparer.OrdinalIgnoreCase) &&
                (providers is null || providers.Contains(entry.Provider)))
            .GroupBy(entry => entry.Provider, StringComparer.OrdinalIgnoreCase);

        foreach (var group in candidates)
        {
            var ordered = group.OrderBy(entry => entry.UpdatedAt).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                var fiveHourReset = ResetDetected(
                    previous.FiveHourResetAt,
                    current.FiveHourResetAt,
                    previous.FiveHourRemainingPercent,
                    current.FiveHourRemainingPercent,
                    current.UpdatedAt,
                    25);
                var weeklyReset = ResetDetected(
                    previous.WeeklyResetAt,
                    current.WeeklyResetAt,
                    previous.WeeklyRemainingPercent,
                    current.WeeklyRemainingPercent,
                    current.UpdatedAt,
                    20);
                if (!fiveHourReset && !weeklyReset)
                {
                    continue;
                }

                if ((start is not null && current.UpdatedAt < start) ||
                    (end is not null && current.UpdatedAt > end))
                {
                    continue;
                }

                var expectedAt = fiveHourReset
                    ? previous.FiveHourResetAt!.Value
                    : previous.WeeklyResetAt!.Value;
                events.Add(new UsageResetEvent(
                    group.Key,
                    fiveHourReset && weeklyReset
                        ? UsageResetKind.Both
                        : fiveHourReset ? UsageResetKind.FiveHour : UsageResetKind.Weekly,
                    expectedAt,
                    current.UpdatedAt,
                    previous.UpdatedAt,
                    previous.FiveHourRemainingPercent,
                    previous.WeeklyRemainingPercent,
                    current.FiveHourRemainingPercent,
                    current.WeeklyRemainingPercent));
            }
        }

        return events.OrderByDescending(item => item.ObservedAt).ToArray();
    }

    private static UsageWindowMetrics Metrics(
        IReadOnlyList<UsageHistoryEntry> entries,
        UsageHistoryQuotaWindow window)
    {
        var values = entries
            .Select(entry => new
            {
                entry.Provider,
                entry.UpdatedAt,
                Remaining = window == UsageHistoryQuotaWindow.FiveHour
                    ? entry.FiveHourRemainingPercent
                    : entry.WeeklyRemainingPercent
            })
            .Where(item => item.Remaining is not null)
            .ToArray();
        var consumed = 0d;
        foreach (var group in values.GroupBy(item => item.Provider, StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(item => item.UpdatedAt).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                consumed += Math.Max(0, ordered[index - 1].Remaining!.Value - ordered[index].Remaining!.Value);
            }
        }

        var first = values.OrderBy(item => item.UpdatedAt).FirstOrDefault()?.Remaining;
        var last = values.OrderBy(item => item.UpdatedAt).LastOrDefault()?.Remaining;
        var hours = entries.Count < 2
            ? 0
            : (entries.Max(entry => entry.UpdatedAt) - entries.Min(entry => entry.UpdatedAt)).TotalHours;
        var resetCount = FindResetEvents(entries).Count(item =>
            window == UsageHistoryQuotaWindow.FiveHour
                ? item.Kind is UsageResetKind.FiveHour or UsageResetKind.Both
                : item.Kind is UsageResetKind.Weekly or UsageResetKind.Both);
        return new UsageWindowMetrics(
            window,
            values.Length,
            resetCount,
            values.Length == 0 ? 0 : values.Average(item => item.Remaining!.Value),
            consumed,
            hours <= 0 ? 0 : consumed / hours,
            first,
            last);
    }

    private static bool ResetDetected(
        DateTimeOffset? previousReset,
        DateTimeOffset? currentReset,
        int? previousRemaining,
        int? currentRemaining,
        DateTimeOffset observedAt,
        int minimumRecovery) =>
        previousReset is not null &&
        currentReset is not null &&
        currentReset.Value > previousReset.Value &&
        previousRemaining is not null &&
        currentRemaining is not null &&
        (currentRemaining.Value - previousRemaining.Value >= minimumRecovery ||
            observedAt >= previousReset.Value.AddMinutes(-5));
}
