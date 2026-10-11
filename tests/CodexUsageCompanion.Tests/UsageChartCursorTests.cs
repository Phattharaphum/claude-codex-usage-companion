using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageChartCursorTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstMoveLandsOnAnEndOfTheFirstLine()
    {
        var series = Series(("claude", 0, 10, 20, 30), ("codex", 0, 10));

        Assert.Equal(new ChartCursor(0, 0), UsageChartCursor.Move(series, null, ChartMove.NextPoint));
        Assert.Equal(new ChartCursor(0, 3), UsageChartCursor.Move(series, null, ChartMove.PreviousPoint));
        Assert.Equal(new ChartCursor(0, 3), UsageChartCursor.Move(series, null, ChartMove.LastPoint));
        Assert.Equal(new ChartCursor(0, 0), UsageChartCursor.Move(series, null, ChartMove.FirstPoint));
    }

    [Fact]
    public void PointMovesStepAlongTheCurrentLineAndStopAtItsEnds()
    {
        var series = Series(("claude", 0, 10, 20));

        Assert.Equal(new ChartCursor(0, 1), UsageChartCursor.Move(series, new ChartCursor(0, 0), ChartMove.NextPoint));
        Assert.Equal(new ChartCursor(0, 2), UsageChartCursor.Move(series, new ChartCursor(0, 2), ChartMove.NextPoint));
        Assert.Equal(new ChartCursor(0, 0), UsageChartCursor.Move(series, new ChartCursor(0, 0), ChartMove.PreviousPoint));
        Assert.Equal(new ChartCursor(0, 1), UsageChartCursor.Move(series, new ChartCursor(0, 2), ChartMove.PreviousPoint));
        Assert.Equal(new ChartCursor(0, 2), UsageChartCursor.Move(series, new ChartCursor(0, 0), ChartMove.LastPoint));
        Assert.Equal(new ChartCursor(0, 0), UsageChartCursor.Move(series, new ChartCursor(0, 2), ChartMove.FirstPoint));
    }

    [Fact]
    public void SwitchingLinesKeepsTheMomentInTime()
    {
        // Claude was sampled at 0, 10, 20, 30; Codex at 5, 15, 25.
        var series = Series(("claude", 0, 10, 20, 30), ("codex", 5, 15, 25));

        var onCodex = UsageChartCursor.Move(series, new ChartCursor(0, 3), ChartMove.NextSeries);
        var backOnClaude = UsageChartCursor.Move(series, new ChartCursor(1, 0), ChartMove.PreviousSeries);

        Assert.Equal(new ChartCursor(1, 2), onCodex);     // 30 -> nearest codex sample is 25
        Assert.Equal(new ChartCursor(0, 0), backOnClaude); // 5 -> nearest claude sample is 0
    }

    [Fact]
    public void SwitchingLinesStopsAtTheLastLine()
    {
        var series = Series(("claude", 0, 10), ("codex", 0, 10));

        Assert.Equal(new ChartCursor(1, 1), UsageChartCursor.Move(series, new ChartCursor(1, 1), ChartMove.NextSeries));
        Assert.Equal(new ChartCursor(0, 1), UsageChartCursor.Move(series, new ChartCursor(0, 1), ChartMove.PreviousSeries));
    }

    [Fact]
    public void EmptyLinesAreSkipped()
    {
        var series = Series(("claude", 0, 10), ("codex"), ("gemini", 0, 10));

        var next = UsageChartCursor.Move(series, new ChartCursor(0, 1), ChartMove.NextSeries);

        Assert.Equal(new ChartCursor(2, 1), next);
    }

    [Fact]
    public void NothingToNavigateGivesNoCursor()
    {
        Assert.Null(UsageChartCursor.Move([], null, ChartMove.NextPoint));
        Assert.Null(UsageChartCursor.Move(Series(("claude")), null, ChartMove.NextPoint));
    }

    [Fact]
    public void AStaleCursorIsTreatedAsNoCursor()
    {
        var series = Series(("claude", 0, 10));

        // The series changed under the cursor (a new period was selected).
        Assert.Equal(new ChartCursor(0, 0), UsageChartCursor.Move(series, new ChartCursor(5, 9), ChartMove.NextPoint));
    }

    [Fact]
    public void FindLocatesASampleByProviderAndValue()
    {
        var series = Series(("claude", 0, 10), ("codex", 0, 10, 20));
        var target = series[1].Points[2];

        Assert.Equal(new ChartCursor(1, 2), UsageChartCursor.Find(series, "CODEX", target));
        Assert.Null(UsageChartCursor.Find(series, "gemini", target));
    }

    private static IReadOnlyList<UsageHistoryChartSeries> Series(params object[] definitions)
    {
        var result = new List<UsageHistoryChartSeries>();
        foreach (var definition in definitions)
        {
            var (provider, minutes) = definition switch
            {
                ValueTuple<string, int, int, int, int> four => (four.Item1, new[] { four.Item2, four.Item3, four.Item4, four.Item5 }),
                ValueTuple<string, int, int, int> three => (three.Item1, new[] { three.Item2, three.Item3, three.Item4 }),
                ValueTuple<string, int, int> two => (two.Item1, new[] { two.Item2, two.Item3 }),
                string name => (name, Array.Empty<int>()),
                _ => throw new ArgumentException("unsupported definition")
            };
            result.Add(new UsageHistoryChartSeries(
                provider,
                minutes.Select(minute => new UsageHistoryChartPoint(Origin.AddMinutes(minute), 50, null)).ToArray(),
                []));
        }

        return result;
    }
}
