namespace CodexUsageCompanion.Diagnostics;

public enum ChartMove
{
    /// <summary>The previous sample of the same provider (earlier in time).</summary>
    PreviousPoint,

    /// <summary>The next sample of the same provider (later in time).</summary>
    NextPoint,

    /// <summary>The same moment on the previous provider's line.</summary>
    PreviousSeries,

    /// <summary>The same moment on the next provider's line.</summary>
    NextSeries,

    FirstPoint,
    LastPoint
}

public readonly record struct ChartCursor(int Series, int Point);

/// <summary>
/// Keyboard navigation over the chart's samples, so a line can be read without
/// a mouse. It is pure logic over the series; the chart only draws the result.
/// </summary>
public static class UsageChartCursor
{
    /// <summary>
    /// Where the cursor goes after <paramref name="move"/>. With no current
    /// position the first move lands on a sensible end of the first line.
    /// Moves clamp at the ends (they do not wrap), and empty series are skipped.
    /// </summary>
    public static ChartCursor? Move(
        IReadOnlyList<UsageHistoryChartSeries> series,
        ChartCursor? current,
        ChartMove move)
    {
        var firstSeries = FirstNonEmpty(series);
        if (firstSeries is null)
        {
            return null;
        }

        if (current is not { } cursor || !IsValid(series, cursor))
        {
            var start = series[firstSeries.Value];
            return move is ChartMove.PreviousPoint or ChartMove.LastPoint
                ? new ChartCursor(firstSeries.Value, start.Points.Count - 1)
                : new ChartCursor(firstSeries.Value, 0);
        }

        var points = series[cursor.Series].Points;
        switch (move)
        {
            case ChartMove.PreviousPoint:
                return cursor with { Point = Math.Max(0, cursor.Point - 1) };
            case ChartMove.NextPoint:
                return cursor with { Point = Math.Min(points.Count - 1, cursor.Point + 1) };
            case ChartMove.FirstPoint:
                return cursor with { Point = 0 };
            case ChartMove.LastPoint:
                return cursor with { Point = points.Count - 1 };
            default:
            {
                var step = move == ChartMove.NextSeries ? 1 : -1;
                for (var index = cursor.Series + step; index >= 0 && index < series.Count; index += step)
                {
                    if (series[index].Points.Count > 0)
                    {
                        return new ChartCursor(
                            index,
                            NearestInTime(series[index].Points, points[cursor.Point].UpdatedAt));
                    }
                }

                return cursor;
            }
        }
    }

    /// <summary>The cursor on a given sample, or null when it is not in the series.</summary>
    public static ChartCursor? Find(
        IReadOnlyList<UsageHistoryChartSeries> series,
        string provider,
        UsageHistoryChartPoint point)
    {
        for (var seriesIndex = 0; seriesIndex < series.Count; seriesIndex++)
        {
            if (!string.Equals(series[seriesIndex].Provider, provider, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pointIndex = series[seriesIndex].Points.ToList().IndexOf(point);
            return pointIndex < 0 ? null : new ChartCursor(seriesIndex, pointIndex);
        }

        return null;
    }

    private static int? FirstNonEmpty(IReadOnlyList<UsageHistoryChartSeries> series)
    {
        for (var index = 0; index < series.Count; index++)
        {
            if (series[index].Points.Count > 0)
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsValid(IReadOnlyList<UsageHistoryChartSeries> series, ChartCursor cursor) =>
        cursor.Series >= 0 &&
        cursor.Series < series.Count &&
        cursor.Point >= 0 &&
        cursor.Point < series[cursor.Series].Points.Count;

    private static int NearestInTime(IReadOnlyList<UsageHistoryChartPoint> points, DateTimeOffset moment)
    {
        var best = 0;
        var bestDistance = TimeSpan.MaxValue;
        for (var index = 0; index < points.Count; index++)
        {
            var distance = (points[index].UpdatedAt - moment).Duration();
            if (distance < bestDistance)
            {
                best = index;
                bestDistance = distance;
            }
        }

        return best;
    }
}
