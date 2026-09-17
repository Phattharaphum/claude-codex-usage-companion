using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageHistorySeriesBuilderTests
{
    [Fact]
    public void BuildsAscendingWeeklySeriesWithinSelectedRange()
    {
        var reference = At(2026, 9, 17, 12);
        var entries = new[]
        {
            Entry("codex", At(2026, 9, 17, 12), 5, 80),
            Entry("codex", At(2026, 9, 15, 12), 10, 65),
            Entry("codex", At(2026, 9, 1, 12), 20, 40)
        };

        var series = UsageHistorySeriesBuilder.Build(
            entries,
            UsageHistoryQuotaWindow.Weekly,
            UsageHistoryTimeRange.SevenDays,
            reference);

        var result = Assert.Single(series);
        Assert.Equal(new[] { 65, 80 }, result.Points.Select(point => point.RemainingPercent));
        Assert.Equal(
            new[] { At(2026, 9, 15, 12), At(2026, 9, 17, 12) },
            result.Points.Select(point => point.UpdatedAt));
    }

    [Fact]
    public void IgnoresErrorsAndCanFilterProviders()
    {
        var reference = At(2026, 9, 17, 12);
        var entries = new[]
        {
            Entry("claude", At(2026, 9, 17, 11), 11, 41),
            Entry("codex", At(2026, 9, 17, 12), 12, 82, status: "error")
        };

        var series = UsageHistorySeriesBuilder.Build(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            UsageHistoryTimeRange.All,
            reference,
            new HashSet<string>(["codex"], StringComparer.OrdinalIgnoreCase));

        Assert.Empty(series);
    }

    [Fact]
    public void DetectsQuotaResetWhenResetTimeChangesAndRemainingRecovers()
    {
        var entries = new[]
        {
            Entry("claude", At(2026, 9, 17, 12), 12, 12, resetAt: At(2026, 9, 17, 17)),
            Entry("claude", At(2026, 9, 17, 13), 82, 82, resetAt: At(2026, 9, 17, 18))
        };

        var series = Assert.Single(UsageHistorySeriesBuilder.Build(
            entries,
            UsageHistoryQuotaWindow.FiveHour,
            UsageHistoryTimeRange.All,
            At(2026, 9, 17, 13)));

        Assert.Equal(new[] { At(2026, 9, 17, 13) }, series.ResetMarkers);
    }

    private static UsageHistoryEntry Entry(
        string provider,
        DateTimeOffset updatedAt,
        int fiveHour,
        int weekly,
        string status = "success",
        DateTimeOffset? resetAt = null) =>
        new(
            updatedAt,
            provider,
            status,
            fiveHour,
            resetAt ?? updatedAt.AddHours(5),
            weekly,
            resetAt ?? updatedAt.AddDays(7),
            null,
            "provider");

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);
}
