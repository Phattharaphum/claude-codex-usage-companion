namespace CodexUsageCompanion.Diagnostics;

public enum UsageHistoryQuotaWindow
{
    FiveHour,
    Weekly
}

public enum UsageHistoryTimeRange
{
    TwentyFourHours,
    SevenDays,
    ThirtyDays,
    All
}

public sealed record UsageHistoryChartPoint(
    DateTimeOffset UpdatedAt,
    int RemainingPercent,
    DateTimeOffset? ResetAt);

public sealed record UsageHistoryChartSeries(
    string Provider,
    IReadOnlyList<UsageHistoryChartPoint> Points,
    IReadOnlyList<DateTimeOffset> ResetMarkers);

/// <summary>
/// Converts raw refresh records into the small, ordered series needed by the
/// history chart. The chart intentionally plots remaining quota, which is the
/// value the providers expose and the history log stores.
/// </summary>
public static class UsageHistorySeriesBuilder
{
    private const int MaximumChartPointsPerSeries = 800;
    private static readonly string[] SupportedProviders =
    [
        "claude",
        "codex",
        "antigravity-gemini",
        "antigravity-claudeandchatgpt"
    ];

    public static IReadOnlyList<UsageHistoryChartSeries> Build(
        IReadOnlyList<UsageHistoryEntry> entries,
        UsageHistoryQuotaWindow window,
        UsageHistoryTimeRange range,
        DateTimeOffset? referenceTime = null,
        IReadOnlySet<string>? providers = null)
    {
        var successfulEntries = entries
            .Where(entry =>
                string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase) &&
                SupportedProviders.Contains(entry.Provider, StringComparer.OrdinalIgnoreCase) &&
                (providers is null || providers.Contains(entry.Provider)))
            .Select(entry => ToPoint(entry, window))
            .Where(point => point is not null)
            .Select(point => point!)
            .ToArray();

        if (successfulEntries.Length == 0)
        {
            return [];
        }

        var latest = referenceTime ?? successfulEntries.Max(point => point.UpdatedAt);
        var earliest = range switch
        {
            UsageHistoryTimeRange.TwentyFourHours => latest - TimeSpan.FromHours(24),
            UsageHistoryTimeRange.SevenDays => latest - TimeSpan.FromDays(7),
            UsageHistoryTimeRange.ThirtyDays => latest - TimeSpan.FromDays(30),
            _ => DateTimeOffset.MinValue
        };

        return BuildSeries(successfulEntries, earliest, latest, window);
    }

    public static IReadOnlyList<UsageHistoryChartSeries> BuildForPeriod(
        IReadOnlyList<UsageHistoryEntry> entries,
        UsageHistoryQuotaWindow window,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlySet<string>? providers = null)
    {
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var points = entries
            .Where(entry =>
                string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase) &&
                SupportedProviders.Contains(entry.Provider, StringComparer.OrdinalIgnoreCase) &&
                (providers is null || providers.Contains(entry.Provider)))
            .Select(entry => ToPoint(entry, window))
            .Where(point => point is not null)
            .Select(point => point!)
            .ToArray();

        return BuildSeries(points, start, end, window);
    }

    private static IReadOnlyList<UsageHistoryChartSeries> BuildSeries(
        IReadOnlyList<ChartPointWithProvider> points,
        DateTimeOffset start,
        DateTimeOffset end,
        UsageHistoryQuotaWindow window)
    {
        var providerOrder = new[]
        {
            "claude",
            "codex",
            "antigravity-gemini",
            "antigravity-claudeandchatgpt"
        };

        return points
            .GroupBy(point => point.Provider, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => ProviderOrder(group.Key, providerOrder))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildSeries(
                group.Key,
                group.OrderBy(point => point.UpdatedAt).Select(point => point.Point).ToArray(),
                window,
                start,
                end))
            .Where(series => series.Points.Count > 0)
            .ToArray();
    }

    private static ChartPointWithProvider? ToPoint(
        UsageHistoryEntry entry,
        UsageHistoryQuotaWindow window)
    {
        var remaining = window == UsageHistoryQuotaWindow.FiveHour
            ? entry.FiveHourRemainingPercent
            : entry.WeeklyRemainingPercent;
        if (remaining is null)
        {
            return null;
        }

        var resetAt = window == UsageHistoryQuotaWindow.FiveHour
            ? entry.FiveHourResetAt
            : entry.WeeklyResetAt;
        return new ChartPointWithProvider(
            entry.Provider,
            new UsageHistoryChartPoint(
                entry.UpdatedAt,
                Math.Clamp(remaining.Value, 0, 100),
                resetAt));
    }

    private static UsageHistoryChartSeries BuildSeries(
        string provider,
        IReadOnlyList<UsageHistoryChartPoint> points,
        UsageHistoryQuotaWindow window,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var resetMarkers = new List<DateTimeOffset>();
        for (var index = 1; index < points.Count; index++)
        {
            var previous = points[index - 1];
            var current = points[index];
            var resetChanged = previous.ResetAt is not null &&
                current.ResetAt is not null &&
                current.ResetAt.Value > previous.ResetAt.Value;
            var quotaJumped = current.RemainingPercent - previous.RemainingPercent >=
                (window == UsageHistoryQuotaWindow.FiveHour ? 25 : 20);
            var crossedExpectedReset = previous.ResetAt is not null &&
                current.UpdatedAt >= previous.ResetAt.Value.AddMinutes(-5);

            // A changed reset timestamp is the primary signal. Quota recovery
            // confirms it early; crossing the former expected time also counts
            // because usage between sparse samples can hide the recovery jump.
            if (resetChanged && (quotaJumped || crossedExpectedReset))
            {
                resetMarkers.Add(current.UpdatedAt);
            }
        }

        var visiblePoints = points
            .Where(point => point.UpdatedAt >= start && point.UpdatedAt <= end)
            .ToArray();
        return new UsageHistoryChartSeries(
            provider,
            Downsample(visiblePoints),
            resetMarkers.Where(marker => marker >= start && marker <= end).ToArray());
    }

    private static IReadOnlyList<UsageHistoryChartPoint> Downsample(
        IReadOnlyList<UsageHistoryChartPoint> points)
    {
        if (points.Count <= MaximumChartPointsPerSeries)
        {
            return points;
        }

        var stride = (int)Math.Ceiling(
            points.Count / (double)MaximumChartPointsPerSeries);
        var sampled = points
            .Where((_, index) => index % stride == 0)
            .ToList();
        if (sampled[^1] != points[^1])
        {
            sampled.Add(points[^1]);
        }

        return sampled;
    }

    private static int ProviderOrder(string provider, IReadOnlyList<string> order)
    {
        var index = order
            .Select((name, index) => new { name, index })
            .FirstOrDefault(item => string.Equals(item.name, provider, StringComparison.OrdinalIgnoreCase));
        return index?.index ?? order.Count;
    }

    private sealed record ChartPointWithProvider(
        string Provider,
        UsageHistoryChartPoint Point)
    {
        public DateTimeOffset UpdatedAt => Point.UpdatedAt;
    }
}
