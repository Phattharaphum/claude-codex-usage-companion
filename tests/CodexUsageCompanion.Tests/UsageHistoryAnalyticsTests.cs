using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageHistoryAnalyticsTests
{
    [Fact]
    public void FindsResetAndPreservesBothQuotaValuesBeforeIt()
    {
        var expectedReset = At(2026, 9, 20, 10);
        var entries = new[]
        {
            Entry(At(2026, 9, 20, 9), 18, expectedReset, 42, At(2026, 9, 24, 10)),
            Entry(At(2026, 9, 20, 10), 96, expectedReset.AddHours(5), 40, At(2026, 9, 24, 10))
        };

        var result = Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));

        Assert.Equal(UsageResetKind.FiveHour, result.Kind);
        Assert.Equal(18, result.FiveHourRemainingBefore);
        Assert.Equal(42, result.WeeklyRemainingBefore);
        Assert.Equal(expectedReset, result.ExpectedAt);
    }

    [Fact]
    public void ResetTimeChangeAfterExpectedTimeIsPrimarySignalWithSparseSamples()
    {
        var expectedReset = At(2026, 9, 20, 10);
        var entries = new[]
        {
            Entry(At(2026, 9, 20, 9), 40, expectedReset, 70, At(2026, 9, 24, 10)),
            Entry(At(2026, 9, 20, 12), 50, expectedReset.AddHours(5), 68, At(2026, 9, 24, 10))
        };

        Assert.Single(UsageHistoryAnalytics.FindResetEvents(entries));
    }

    [Fact]
    public void SummaryCountsOnlyDownwardMovementAsConsumption()
    {
        var entries = new[]
        {
            Entry(At(2026, 9, 20, 8), 90, At(2026, 9, 20, 10), 80, At(2026, 9, 24, 10)),
            Entry(At(2026, 9, 20, 9), 70, At(2026, 9, 20, 10), 75, At(2026, 9, 24, 10)),
            Entry(At(2026, 9, 20, 10), 100, At(2026, 9, 20, 15), 73, At(2026, 9, 24, 10))
        };

        var summary = UsageHistoryAnalytics.Summarize(entries);

        Assert.Equal(20, summary.FiveHour.ConsumedPercent);
        Assert.Equal(7, summary.Weekly.ConsumedPercent);
        Assert.Equal(10, summary.FiveHour.ConsumptionPerHour);
    }

    [Fact]
    public void ExactPeriodSeriesDoesNotAnchorToLatestSample()
    {
        var entries = new[]
        {
            Entry(At(2026, 9, 18, 8), 90, At(2026, 9, 18, 13), 80, At(2026, 9, 24, 10)),
            Entry(At(2026, 9, 20, 8), 70, At(2026, 9, 20, 13), 60, At(2026, 9, 24, 10))
        };

        var series = Assert.Single(UsageHistorySeriesBuilder.BuildForPeriod(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            At(2026, 9, 18, 0),
            At(2026, 9, 18, 23)));

        Assert.Equal(90, Assert.Single(series.Points).RemainingPercent);
    }

    private static UsageHistoryEntry Entry(
        DateTimeOffset updatedAt,
        int fiveHour,
        DateTimeOffset fiveHourReset,
        int weekly,
        DateTimeOffset weeklyReset) => new(
            updatedAt,
            "codex",
            "success",
            fiveHour,
            fiveHourReset,
            weekly,
            weeklyReset,
            null,
            "provider");

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);
}
