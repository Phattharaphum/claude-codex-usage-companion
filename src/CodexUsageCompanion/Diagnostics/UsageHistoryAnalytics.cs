namespace CodexUsageCompanion.Diagnostics;

public enum UsageResetKind
{
    FiveHour,
    Weekly,
    Both
}

/// <summary>
/// How far a reset event can be trusted as a measurement of how fully a cycle
/// was used. Only <see cref="Measured"/> events say something about usage.
/// </summary>
public enum UsageResetQuality
{
    /// <summary>Sampled closely enough that the pre-reset value is meaningful.</summary>
    Measured,

    /// <summary>The window was never touched: it expired full, so there is no cycle to judge.</summary>
    Idle,

    /// <summary>The app was not sampling around the reset, so usage just before it is unknown.</summary>
    Estimated
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

    public UsageResetQuality Quality { get; init; } = UsageResetQuality.Measured;

    /// <summary>The longest gap that still counted as continuous sampling.</summary>
    public TimeSpan GapThreshold { get; init; }
}

public sealed record UsageWindowMetrics(
    UsageHistoryQuotaWindow Window,
    int SampleCount,
    int ResetCount,
    double AverageRemainingPercent,
    double ConsumedPercent,
    double ConsumptionPerHour,
    int? FirstRemainingPercent,
    int? LastRemainingPercent)
{
    /// <summary>How many providers contributed to these figures.</summary>
    public int ProviderCount { get; init; } = 1;

    /// <summary>
    /// The average burn per provider. <see cref="ConsumptionPerHour"/> adds the
    /// providers together, which grows with how many are being tracked and so
    /// cannot be compared with a per-provider threshold.
    /// </summary>
    public double ConsumptionPerProviderHour { get; init; }

    /// <summary>Consumption expressed as whole quota windows used (100 points each).</summary>
    public double EquivalentWindows => ConsumedPercent / 100d;

    /// <summary>Hours until the last observed remaining quota runs out at the average burn.</summary>
    public double? RunwayHours => LastRemainingPercent is int remaining && ConsumptionPerHour > 0
        ? remaining / ConsumptionPerHour
        : null;
}

public sealed record UsageProviderSummary(
    string Provider,
    int RecordCount,
    TimeSpan ObservedDuration,
    UsageWindowMetrics FiveHour,
    UsageWindowMetrics Weekly,
    UsageDataQuality Quality)
{
    public UsageWindowMetrics For(UsageHistoryQuotaWindow window) =>
        window == UsageHistoryQuotaWindow.FiveHour ? FiveHour : Weekly;
}

public sealed record UsageRunway(string Provider, double Hours);

public sealed record UsageSelectionSummary(
    int RecordCount,
    int ProviderCount,
    TimeSpan ObservedDuration,
    UsageWindowMetrics FiveHour,
    UsageWindowMetrics Weekly)
{
    public IReadOnlyList<UsageProviderSummary> Providers { get; init; } = [];

    public UsageDataQuality Quality { get; init; } = UsageDataQuality.Complete;

    public IReadOnlyList<UsageResetEvent> ResetEvents { get; init; } = [];

    /// <summary>
    /// How long a typical provider went unobserved. <see cref="Quality"/> adds
    /// providers' gaps together, which can exceed the span being shown.
    /// </summary>
    public TimeSpan UnobservedPerProvider => ProviderCount <= 0
        ? TimeSpan.Zero
        : TimeSpan.FromTicks(Quality.MissingDuration.Ticks / ProviderCount);

    public UsageWindowMetrics For(UsageHistoryQuotaWindow window) =>
        window == UsageHistoryQuotaWindow.FiveHour ? FiveHour : Weekly;

    /// <summary>
    /// The provider that would run out first at its own average burn. A single
    /// combined runway would divide one provider's remaining quota by every
    /// provider's consumption, which answers nothing.
    /// </summary>
    public UsageRunway? SoonestRunway(UsageHistoryQuotaWindow window) => Providers
        .Select(provider => new UsageRunway(provider.Provider, provider.For(window).RunwayHours ?? double.NaN))
        .Where(runway => !double.IsNaN(runway.Hours))
        .OrderBy(runway => runway.Hours)
        .Cast<UsageRunway?>()
        .FirstOrDefault();
}

/// <summary>
/// Computes reset-cycle and selected-period statistics from successful local
/// history records. Consumption totals only downward quota movement; quota
/// recovery at a detected reset is deliberately excluded.
/// </summary>
public static class UsageHistoryAnalytics
{
    /// <summary>A window this full when it reset was effectively never used.</summary>
    public const int IdleRemainingPercent = 99;

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
        var events = FindResetEvents(successful);

        var providers = successful
            .GroupBy(entry => entry.Provider, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => ProviderRank(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var own = group.ToArray();
                var times = own.Select(entry => entry.UpdatedAt).ToArray();
                var ownEvents = events
                    .Where(item => string.Equals(item.Provider, group.Key, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new UsageProviderSummary(
                    group.Key,
                    own.Length,
                    own.Length < 2 ? TimeSpan.Zero : times[^1] - times[0],
                    Metrics(own, UsageHistoryQuotaWindow.FiveHour, ownEvents),
                    Metrics(own, UsageHistoryQuotaWindow.Weekly, ownEvents),
                    UsageHistoryCoverage.Measure(times));
            })
            .ToArray();

        return new UsageSelectionSummary(
            successful.Length,
            providers.Length,
            duration,
            WithProviderAverage(
                Metrics(successful, UsageHistoryQuotaWindow.FiveHour, events),
                providers.Select(provider => provider.FiveHour)),
            WithProviderAverage(
                Metrics(successful, UsageHistoryQuotaWindow.Weekly, events),
                providers.Select(provider => provider.Weekly)))
        {
            Providers = providers,
            Quality = UsageHistoryCoverage.Combine(providers
                .Select(provider => (provider.Quality, provider.ObservedDuration))
                .ToArray()),
            ResetEvents = events
        };
    }

    private static UsageWindowMetrics WithProviderAverage(
        UsageWindowMetrics combined,
        IEnumerable<UsageWindowMetrics> perProvider)
    {
        var contributing = perProvider.Where(item => item.SampleCount > 0).ToArray();
        return combined with
        {
            ProviderCount = contributing.Length,
            ConsumptionPerProviderHour = contributing.Length == 0
                ? 0
                : contributing.Average(item => item.ConsumptionPerHour)
        };
    }

    private static int ProviderRank(string provider)
    {
        var index = Array.FindIndex(
            SupportedProviders,
            name => string.Equals(name, provider, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? SupportedProviders.Length : index;
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
            var gapThreshold = UsageHistoryCoverage.GapThreshold(
                ordered.Select(entry => entry.UpdatedAt).ToArray());
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
                var kind = fiveHourReset && weeklyReset
                    ? UsageResetKind.Both
                    : fiveHourReset ? UsageResetKind.FiveHour : UsageResetKind.Weekly;
                events.Add(new UsageResetEvent(
                    group.Key,
                    kind,
                    expectedAt,
                    current.UpdatedAt,
                    previous.UpdatedAt,
                    previous.FiveHourRemainingPercent,
                    previous.WeeklyRemainingPercent,
                    current.FiveHourRemainingPercent,
                    current.WeeklyRemainingPercent)
                {
                    GapThreshold = gapThreshold,
                    Quality = Classify(
                        kind,
                        current.UpdatedAt - previous.UpdatedAt,
                        gapThreshold,
                        previous.FiveHourRemainingPercent,
                        previous.WeeklyRemainingPercent)
                });
            }
        }

        return events.OrderByDescending(item => item.ObservedAt).ToArray();
    }

    private static UsageResetQuality Classify(
        UsageResetKind kind,
        TimeSpan observationGap,
        TimeSpan gapThreshold,
        int? fiveHourBefore,
        int? weeklyBefore)
    {
        // Usage between the last sample and the reset is unknown, so even a
        // full-looking window may have been drained before it expired.
        if (observationGap > gapThreshold)
        {
            return UsageResetQuality.Estimated;
        }

        var fiveHourUntouched = fiveHourBefore is >= IdleRemainingPercent;
        var weeklyUntouched = weeklyBefore is >= IdleRemainingPercent;
        var untouched = kind switch
        {
            UsageResetKind.FiveHour => fiveHourUntouched,
            UsageResetKind.Weekly => weeklyUntouched,
            _ => fiveHourUntouched && weeklyUntouched
        };
        return untouched ? UsageResetQuality.Idle : UsageResetQuality.Measured;
    }

    private static UsageWindowMetrics Metrics(
        IReadOnlyList<UsageHistoryEntry> entries,
        UsageHistoryQuotaWindow window,
        IReadOnlyList<UsageResetEvent> resetEvents)
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
        var resetCount = resetEvents.Count(item =>
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
