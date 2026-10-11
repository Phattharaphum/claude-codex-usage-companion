namespace CodexUsageCompanion.Diagnostics;

/// <summary>A stretch of time with no successful sample from one provider.</summary>
public readonly record struct UsageGap(DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan Duration => End - Start;
}

/// <summary>
/// How much of an observed span is actually backed by samples. The app is not
/// always running, so quota can change unobserved; every analysis that is
/// sensitive to that needs to know where the holes are.
/// </summary>
public sealed record UsageDataQuality(int GapCount, TimeSpan MissingDuration, double CoveragePercent)
{
    public static UsageDataQuality Complete { get; } = new(0, TimeSpan.Zero, 100);
}

public static class UsageHistoryCoverage
{
    public static readonly TimeSpan MinimumGapThreshold = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan MaximumGapThreshold = TimeSpan.FromHours(3);
    private const int GapIntervalMultiplier = 5;
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LongestPlausibleInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// The typical spacing between samples: the median interval, so a few long
    /// outages do not inflate it. Expects times ordered ascending.
    /// </summary>
    public static TimeSpan TypicalInterval(IReadOnlyList<DateTimeOffset> orderedTimes)
    {
        if (orderedTimes.Count < 2)
        {
            return DefaultInterval;
        }

        var intervals = new List<long>(orderedTimes.Count - 1);
        for (var index = 1; index < orderedTimes.Count; index++)
        {
            var interval = orderedTimes[index] - orderedTimes[index - 1];
            if (interval > TimeSpan.Zero && interval <= LongestPlausibleInterval)
            {
                intervals.Add(interval.Ticks);
            }
        }

        if (intervals.Count == 0)
        {
            return DefaultInterval;
        }

        intervals.Sort();
        return TimeSpan.FromTicks(intervals[intervals.Count / 2]);
    }

    /// <summary>
    /// The interval beyond which a missing sample means "not observed" rather
    /// than "no change". Scales with the sampling rate but stays within sane
    /// bounds so a 1-minute poller and a 15-minute poller both behave.
    /// </summary>
    public static TimeSpan GapThreshold(TimeSpan typicalInterval)
    {
        var scaled = TimeSpan.FromTicks(typicalInterval.Ticks * GapIntervalMultiplier);
        return scaled < MinimumGapThreshold
            ? MinimumGapThreshold
            : scaled > MaximumGapThreshold ? MaximumGapThreshold : scaled;
    }

    public static TimeSpan GapThreshold(IReadOnlyList<DateTimeOffset> orderedTimes) =>
        GapThreshold(TypicalInterval(orderedTimes));

    public static IReadOnlyList<UsageGap> FindGaps(
        IReadOnlyList<DateTimeOffset> orderedTimes,
        TimeSpan? threshold = null)
    {
        if (orderedTimes.Count < 2)
        {
            return [];
        }

        var limit = threshold ?? GapThreshold(orderedTimes);
        var gaps = new List<UsageGap>();
        for (var index = 1; index < orderedTimes.Count; index++)
        {
            if (orderedTimes[index] - orderedTimes[index - 1] > limit)
            {
                gaps.Add(new UsageGap(orderedTimes[index - 1], orderedTimes[index]));
            }
        }

        return gaps;
    }

    public static UsageDataQuality Measure(IReadOnlyList<DateTimeOffset> orderedTimes)
    {
        if (orderedTimes.Count < 2)
        {
            return UsageDataQuality.Complete;
        }

        var gaps = FindGaps(orderedTimes);
        var span = orderedTimes[^1] - orderedTimes[0];
        var missing = TimeSpan.FromTicks(gaps.Sum(gap => gap.Duration.Ticks));
        return new UsageDataQuality(gaps.Count, missing, CoveragePercent(span, missing));
    }

    public static double CoveragePercent(TimeSpan span, TimeSpan missing) =>
        span <= TimeSpan.Zero
            ? 100
            : Math.Clamp((1 - missing.TotalSeconds / span.TotalSeconds) * 100, 0, 100);

    /// <summary>Combines per-provider qualities, weighting by observed span.</summary>
    public static UsageDataQuality Combine(
        IReadOnlyCollection<(UsageDataQuality Quality, TimeSpan Span)> parts)
    {
        if (parts.Count == 0)
        {
            return UsageDataQuality.Complete;
        }

        var missing = TimeSpan.FromTicks(parts.Sum(part => part.Quality.MissingDuration.Ticks));
        var span = TimeSpan.FromTicks(parts.Sum(part => part.Span.Ticks));
        return new UsageDataQuality(
            parts.Sum(part => part.Quality.GapCount),
            missing,
            CoveragePercent(span, missing));
    }
}
