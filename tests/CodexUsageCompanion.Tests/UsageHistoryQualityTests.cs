using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageHistoryQualityTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TypicalIntervalIsTheMedianSoOutagesDoNotInflateIt()
    {
        var times = Minutes(0, 1, 2, 3, 4, 5, 600, 601, 602);

        Assert.Equal(TimeSpan.FromMinutes(1), UsageHistoryCoverage.TypicalInterval(times));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 10)]
    [InlineData(5, 25)]
    [InlineData(60, 180)]
    public void GapThresholdScalesWithTheSamplingRateWithinBounds(int intervalMinutes, int expectedMinutes)
    {
        var threshold = UsageHistoryCoverage.GapThreshold(TimeSpan.FromMinutes(intervalMinutes));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), threshold);
    }

    [Fact]
    public void FindGapsReportsOnlyHolesLongerThanTheThreshold()
    {
        var times = Minutes(0, 1, 2, 3, 4, 120, 121, 122);

        var gap = Assert.Single(UsageHistoryCoverage.FindGaps(times));

        Assert.Equal(Origin.AddMinutes(4), gap.Start);
        Assert.Equal(Origin.AddMinutes(120), gap.End);
        Assert.Equal(TimeSpan.FromMinutes(116), gap.Duration);
    }

    [Fact]
    public void MeasureReportsCoverageOfTheObservedSpan()
    {
        // 100 minutes observed, 80 of which are a hole.
        var times = Minutes(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100);

        var quality = UsageHistoryCoverage.Measure(times);

        Assert.Equal(1, quality.GapCount);
        Assert.Equal(TimeSpan.FromMinutes(80), quality.MissingDuration);
        Assert.Equal(20, quality.CoveragePercent, 1);
    }

    [Fact]
    public void ContinuousSamplingIsFullyCovered()
    {
        var quality = UsageHistoryCoverage.Measure(Minutes(Enumerable.Range(0, 60).ToArray()));

        Assert.Equal(0, quality.GapCount);
        Assert.Equal(100, quality.CoveragePercent);
    }

    [Fact]
    public void ResetAfterAnObservationGapIsEstimatedNotMeasured()
    {
        var entries = ContinuousThenReset(gapMinutes: 334, remainingBefore: 40);

        var reset = Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));

        Assert.Equal(UsageResetQuality.Estimated, reset.Quality);
        Assert.True(reset.ObservationGap > reset.GapThreshold);
    }

    [Fact]
    public void ResetOfAnUntouchedWindowIsIdle()
    {
        var entries = ContinuousThenReset(gapMinutes: 1, remainingBefore: 100);

        var reset = Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));

        Assert.Equal(UsageResetQuality.Idle, reset.Quality);
    }

    [Fact]
    public void ResetOfAUsedWindowSampledClosely_IsMeasured()
    {
        var entries = ContinuousThenReset(gapMinutes: 1, remainingBefore: 37);

        var reset = Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));

        Assert.Equal(UsageResetQuality.Measured, reset.Quality);
    }

    [Fact]
    public void AGapOutranksIdleBecauseTheWindowMayHaveBeenDrainedUnobserved()
    {
        var entries = ContinuousThenReset(gapMinutes: 300, remainingBefore: 100);

        var reset = Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));

        Assert.Equal(UsageResetQuality.Estimated, reset.Quality);
    }

    [Fact]
    public void BothWindowsResettingIsIdleOnlyWhenBothWereUntouched()
    {
        var expectedFive = Origin.AddMinutes(30);
        var expectedWeek = Origin.AddMinutes(30);
        UsageHistoryEntry[] Build(int weeklyBefore) =>
        [
            Entry("codex", Origin.AddMinutes(28), 100, expectedFive, weeklyBefore, expectedWeek),
            Entry("codex", Origin.AddMinutes(29), 100, expectedFive, weeklyBefore, expectedWeek),
            Entry("codex", Origin.AddMinutes(31), 100, expectedFive.AddHours(5), 100, expectedWeek.AddDays(7))
        ];

        var untouched = Assert.Single(UsageHistoryAnalytics.FindResetEvents(Build(100)));
        var used = Assert.Single(UsageHistoryAnalytics.FindResetEvents(Build(60)));

        Assert.Equal(UsageResetKind.Both, untouched.Kind);
        Assert.Equal(UsageResetQuality.Idle, untouched.Quality);
        Assert.Equal(UsageResetQuality.Measured, used.Quality);
    }

    [Fact]
    public void SummarizeSeparatesProvidersAndKeepsTheirOwnRates()
    {
        var entries = new List<UsageHistoryEntry>();
        for (var minute = 0; minute <= 60; minute += 10)
        {
            // Claude burns 1 point per 10 minutes; Codex burns 5.
            entries.Add(Entry("claude", Origin.AddMinutes(minute), 100 - minute / 10, Origin.AddHours(5), 90, Origin.AddDays(5)));
            entries.Add(Entry("codex", Origin.AddMinutes(minute), 100 - 5 * (minute / 10), Origin.AddHours(5), 90, Origin.AddDays(5)));
        }

        var summary = UsageHistoryAnalytics.Summarize(entries);

        Assert.Equal(2, summary.Providers.Count);
        var claude = Assert.Single(summary.Providers, item => item.Provider == "claude");
        var codex = Assert.Single(summary.Providers, item => item.Provider == "codex");
        Assert.Equal(6, claude.FiveHour.ConsumedPercent);
        Assert.Equal(30, codex.FiveHour.ConsumedPercent);
        Assert.Equal(6, claude.FiveHour.ConsumptionPerHour, 3);
        Assert.Equal(30, codex.FiveHour.ConsumptionPerHour, 3);
        // Combined sums providers; the per-provider average does not grow with their number.
        Assert.Equal(36, summary.FiveHour.ConsumptionPerHour, 3);
        Assert.Equal(18, summary.FiveHour.ConsumptionPerProviderHour, 3);
        Assert.Equal(2, summary.FiveHour.ProviderCount);
        Assert.Equal(0.36, summary.FiveHour.EquivalentWindows, 3);
    }

    [Fact]
    public void SoonestRunwayNamesTheProviderThatRunsOutFirst()
    {
        var entries = new List<UsageHistoryEntry>();
        for (var minute = 0; minute <= 60; minute += 10)
        {
            // Both end near 50% but Codex burns far faster, so it runs out sooner.
            entries.Add(Entry("claude", Origin.AddMinutes(minute), 56 - minute / 10, Origin.AddHours(5), 90, Origin.AddDays(5)));
            entries.Add(Entry("codex", Origin.AddMinutes(minute), 100 - 5 * (minute / 10), Origin.AddHours(5), 90, Origin.AddDays(5)));
        }

        var runway = UsageHistoryAnalytics.Summarize(entries).SoonestRunway(UsageHistoryQuotaWindow.FiveHour);

        Assert.NotNull(runway);
        Assert.Equal("codex", runway!.Provider);
        Assert.Equal(70d / 30d, runway.Hours, 3);
    }

    [Fact]
    public void SummarizeReportsCoverageAcrossProviders()
    {
        var entries = new List<UsageHistoryEntry>();
        for (var minute = 0; minute <= 10; minute++)
        {
            entries.Add(Entry("codex", Origin.AddMinutes(minute), 80, Origin.AddHours(5), 90, Origin.AddDays(5)));
        }

        for (var minute = 200; minute <= 210; minute++)
        {
            entries.Add(Entry("codex", Origin.AddMinutes(minute), 70, Origin.AddHours(5), 90, Origin.AddDays(5)));
        }

        var summary = UsageHistoryAnalytics.Summarize(entries);

        Assert.Equal(1, summary.Quality.GapCount);
        Assert.InRange(summary.Quality.CoveragePercent, 8, 12);
    }

    private static IReadOnlyList<DateTimeOffset> Minutes(params int[] offsets) =>
        offsets.Select(offset => Origin.AddMinutes(offset)).ToArray();

    // Steady one-minute sampling, then one more sample after the window expired.
    private static UsageHistoryEntry[] ContinuousThenReset(int gapMinutes, int remainingBefore)
    {
        var expected = Origin.AddMinutes(30);
        var entries = new List<UsageHistoryEntry>();
        for (var minute = 0; minute < 30; minute++)
        {
            entries.Add(Entry("codex", Origin.AddMinutes(minute), remainingBefore, expected, 80, Origin.AddDays(5)));
        }

        entries.Add(Entry(
            "codex",
            Origin.AddMinutes(29 + gapMinutes),
            100,
            expected.AddHours(5),
            80,
            Origin.AddDays(5)));
        return entries.ToArray();
    }

    private static UsageHistoryEntry Entry(
        string provider,
        DateTimeOffset at,
        int fiveHour,
        DateTimeOffset fiveHourReset,
        int weekly,
        DateTimeOffset weeklyReset) =>
        new(at, provider, "success", fiveHour, fiveHourReset, weekly, weeklyReset, null, null);

    [Fact]
    public void ChartSeriesMarksThePointThatFollowsAHole()
    {
        var entries = Samples("codex", 0, 1, 2, 3, 600, 601, 602);

        var series = Assert.Single(UsageHistorySeriesBuilder.BuildForPeriod(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            Origin,
            Origin.AddDays(1)));

        Assert.Equal(
            [false, false, false, false, true, false, false],
            series.Points.Select(point => point.BreaksBefore).ToArray());
        var gap = Assert.Single(series.Gaps);
        Assert.Equal(Origin.AddMinutes(3), gap.Start);
        Assert.Equal(Origin.AddMinutes(600), gap.End);
    }

    [Fact]
    public void ChartGapDetectionDoesNotDependOnTheViewedSpan()
    {
        // A 30-hour hole used to be bridged by a 7-day view because the
        // tolerance grew with the span. It must be broken at any zoom level.
        var entries = Samples("claude", 0, 1, 2, 3, 30 * 60, 30 * 60 + 1);

        var series = Assert.Single(UsageHistorySeriesBuilder.BuildForPeriod(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            Origin,
            Origin.AddDays(7)));

        Assert.True(series.Points.Single(point => point.UpdatedAt == Origin.AddMinutes(30 * 60)).BreaksBefore);
    }

    [Fact]
    public void DownsamplingKeepsBothEdgesOfEveryHole()
    {
        var minutes = Enumerable.Range(0, 2000).Concat(Enumerable.Range(5000, 2000)).ToArray();
        var entries = Samples("codex", minutes);

        var series = Assert.Single(UsageHistorySeriesBuilder.BuildForPeriod(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            Origin,
            Origin.AddDays(10)));

        Assert.True(series.Points.Count < minutes.Length);
        Assert.Contains(series.Points, point => point.UpdatedAt == Origin.AddMinutes(1999));
        var after = Assert.Single(series.Points, point => point.BreaksBefore);
        Assert.Equal(Origin.AddMinutes(5000), after.UpdatedAt);
    }

    [Fact]
    public void CommonGapsAreTheStretchesNoProviderWasObserved()
    {
        var entries = Samples("codex", 0, 1, 2, 3, 300, 301)
            .Concat(Samples("claude", 0, 1, 2, 3, 4, 5, 6, 7, 100, 101, 102, 103, 104, 300, 301))
            .ToArray();

        var series = UsageHistorySeriesBuilder.BuildForPeriod(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            Origin,
            Origin.AddDays(1));
        var common = UsageHistorySeriesBuilder.CommonGaps(series);

        // Codex is dark from 3-300; Claude is dark from 7-100 and 104-300.
        Assert.Equal(2, common.Count);
        Assert.Equal(Origin.AddMinutes(7), common[0].Start);
        Assert.Equal(Origin.AddMinutes(100), common[0].End);
        Assert.Equal(Origin.AddMinutes(104), common[1].Start);
        Assert.Equal(Origin.AddMinutes(300), common[1].End);
    }

    [Fact]
    public void ContinuousSeriesHasNoGapsAndNoBreaks()
    {
        var series = Assert.Single(UsageHistorySeriesBuilder.BuildForPeriod(
            Samples("codex", Enumerable.Range(0, 120).ToArray()),
            UsageHistoryQuotaWindow.FiveHour,
            Origin,
            Origin.AddDays(1)));

        Assert.Empty(series.Gaps);
        Assert.DoesNotContain(series.Points, point => point.BreaksBefore);
    }

    private static UsageHistoryEntry[] Samples(string provider, params int[] minutes) => minutes
        .Select(minute => Entry(provider, Origin.AddMinutes(minute), 80, Origin.AddHours(5), 90, Origin.AddDays(5)))
        .ToArray();
}
