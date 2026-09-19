using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CodexUsageCompanion.Diagnostics;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// Dependency-free interactive quota chart with an optional fixed viewport.
/// A fixed viewport makes empty time inside a selected calendar period visible;
/// the automatic viewport focuses on the span that contains samples.
/// </summary>
public sealed class UsageHistoryChart : Control
{
    private static readonly IBrush CanvasBrush = Brush("#FCFDFC");
    private static readonly IBrush PlotBrush = Brush("#F5F8F6");
    private static readonly IBrush GridBrush = Brush("#DFE7E2");
    private static readonly IBrush AxisBrush = Brush("#69756E");
    private static readonly IBrush ResetBrush = Brush("#E48332");
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen ResetPen = new(ResetBrush, 1.5, DashStyle.Dash);
    private static readonly Typeface ChartTypeface = new("Inter");

    private IReadOnlyList<UsageHistoryChartSeries> _series = [];
    private HoveredPoint? _hoveredPoint;
    private DateTimeOffset? _viewportStart;
    private DateTimeOffset? _viewportEnd;

    public string RemainingLabel { get; set; } = "Remaining";
    public string ResetLabel { get; set; } = "Reset";
    public string NoDataText { get; set; } = "No chart data for this selection.";
    public Func<string, string>? ProviderLabelFormatter { get; set; }

    public UsageHistoryChart()
    {
        MinHeight = 270;
        Height = 292;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        PointerMoved += HandlePointerMoved;
        PointerExited += (_, _) =>
        {
            _hoveredPoint = null;
            InvalidateVisual();
        };
    }

    public void SetSeries(IReadOnlyList<UsageHistoryChartSeries> series)
    {
        _series = series;
        _hoveredPoint = null;
        InvalidateVisual();
    }

    public void SetViewport(DateTimeOffset? start, DateTimeOffset? end)
    {
        _viewportStart = start;
        _viewportEnd = end;
        _hoveredPoint = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(CanvasBrush, null, new Rect(Bounds.Size));
        var plot = GetPlotBounds();
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(PlotBrush, null, plot, 8, 8);
        var points = _series.SelectMany(series => series.Points).ToArray();
        var (minimumTime, maximumTime) = ResolveTimeBounds(points);
        DrawGrid(context, plot, minimumTime, maximumTime);
        DrawLegend(context, plot);

        var resetMarkers = _series
            .SelectMany(series => series.ResetMarkers)
            .Where(marker => marker >= minimumTime && marker <= maximumTime)
            .Distinct()
            .OrderBy(marker => marker)
            .ToArray();
        foreach (var marker in resetMarkers)
        {
            DrawResetMarker(context, plot, marker, minimumTime, maximumTime);
        }

        foreach (var series in _series)
        {
            DrawSeries(context, plot, series, minimumTime, maximumTime);
        }

        if (points.Length == 0)
        {
            DrawEmptyState(context, plot);
            return;
        }

        if (_hoveredPoint is not null)
        {
            DrawTooltip(context, plot, _hoveredPoint, minimumTime, maximumTime);
        }
    }

    private (DateTimeOffset Start, DateTimeOffset End) ResolveTimeBounds(
        IReadOnlyList<UsageHistoryChartPoint> points)
    {
        if (_viewportStart is not null && _viewportEnd is not null && _viewportEnd > _viewportStart)
        {
            return (_viewportStart.Value, _viewportEnd.Value);
        }

        var minimum = points.Count == 0 ? DateTimeOffset.Now.AddHours(-1) : points.Min(point => point.UpdatedAt);
        var maximum = points.Count == 0 ? DateTimeOffset.Now : points.Max(point => point.UpdatedAt);
        if (minimum == maximum)
        {
            minimum -= TimeSpan.FromMinutes(30);
            maximum += TimeSpan.FromMinutes(30);
        }

        return (minimum, maximum);
    }

    private void DrawGrid(
        DrawingContext context,
        Rect plot,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        for (var tick = 0; tick <= 4; tick++)
        {
            var percent = 100 - tick * 25;
            var y = plot.Top + plot.Height * tick / 4d;
            context.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(context, $"{percent}%", new Point(7, y - 7), AxisBrush, 10);
        }

        var span = maximumTime - minimumTime;
        for (var tick = 0; tick <= 4; tick++)
        {
            var x = plot.Left + plot.Width * tick / 4d;
            context.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var time = minimumTime + TimeSpan.FromTicks(span.Ticks * tick / 4);
            var label = time.ToLocalTime().ToString(
                span > TimeSpan.FromDays(2) ? "MMM d" : span > TimeSpan.FromHours(26) ? "ddd HH:mm" : "HH:mm",
                CultureInfo.CurrentCulture);
            var formatted = Text(label, 10, AxisBrush);
            context.DrawText(formatted, new Point(x - formatted.Width / 2, plot.Bottom + 9));
        }
    }

    private void DrawLegend(DrawingContext context, Rect plot)
    {
        var legendX = plot.Left;
        foreach (var series in _series)
        {
            var color = ProviderColor(series.Provider);
            context.DrawEllipse(color, null, new Point(legendX + 4, 12), 4, 4);
            var label = Text(ProviderLabelFormatter?.Invoke(series.Provider) ?? series.Provider, 10, AxisBrush);
            context.DrawText(label, new Point(legendX + 13, 5));
            legendX += 28 + label.Width;
        }

        if (_series.SelectMany(series => series.ResetMarkers).Any())
        {
            context.DrawLine(ResetPen, new Point(legendX + 2, 5), new Point(legendX + 2, 19));
            DrawText(context, ResetLabel, new Point(legendX + 9, 5), AxisBrush, 10);
        }
    }

    private void DrawResetMarker(
        DrawingContext context,
        Rect plot,
        DateTimeOffset marker,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var x = X(marker, minimumTime, maximumTime, plot);
        context.DrawLine(ResetPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        var triangle = new StreamGeometry();
        using (var geometry = triangle.Open())
        {
            geometry.BeginFigure(new Point(x - 5, plot.Top), true);
            geometry.LineTo(new Point(x + 5, plot.Top));
            geometry.LineTo(new Point(x, plot.Top + 7));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(ResetBrush, null, triangle);
    }

    private void DrawSeries(
        DrawingContext context,
        Rect plot,
        UsageHistoryChartSeries series,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var color = ProviderColor(series.Provider);
        var pen = new Pen(color, 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Point? previous = null;
        DateTimeOffset? previousTime = null;
        var visible = series.Points
            .Where(point => point.UpdatedAt >= minimumTime && point.UpdatedAt <= maximumTime)
            .ToArray();
        var span = maximumTime - minimumTime;
        foreach (var sample in visible)
        {
            var current = new Point(
                X(sample.UpdatedAt, minimumTime, maximumTime, plot),
                Y(sample.RemainingPercent, plot));
            if (previous is Point previousPoint && previousTime is not null &&
                sample.UpdatedAt - previousTime <= TimeSpan.FromTicks(Math.Max(TimeSpan.FromMinutes(90).Ticks, span.Ticks / 5)))
            {
                context.DrawLine(pen, previousPoint, current);
            }

            if (visible.Length <= 100)
            {
                context.DrawEllipse(CanvasBrush, new Pen(color, 1.5), current, 3, 3);
            }
            previous = current;
            previousTime = sample.UpdatedAt;
        }
    }

    private void DrawEmptyState(DrawingContext context, Rect plot)
    {
        var heading = Text(NoDataText, 12, AxisBrush);
        var center = new Point(plot.Center.X, plot.Center.Y);
        context.DrawEllipse(null, new Pen(GridBrush, 2), new Point(center.X, center.Y - 14), 13, 13);
        context.DrawLine(new Pen(GridBrush, 2), new Point(center.X - 5, center.Y - 14), new Point(center.X + 5, center.Y - 14));
        context.DrawText(heading, new Point(center.X - heading.Width / 2, center.Y + 9));
    }

    private void DrawTooltip(
        DrawingContext context,
        Rect plot,
        HoveredPoint hovered,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var point = new Point(
            X(hovered.Point.UpdatedAt, minimumTime, maximumTime, plot),
            Y(hovered.Point.RemainingPercent, plot));
        context.DrawLine(new Pen(Brush("#8A9890"), 1, DashStyle.Dash),
            new Point(point.X, plot.Top), new Point(point.X, plot.Bottom));
        var color = ProviderColor(hovered.Provider);
        context.DrawEllipse(CanvasBrush, new Pen(color, 2.5), point, 5, 5);
        var lines = new[]
        {
            ProviderLabelFormatter?.Invoke(hovered.Provider) ?? hovered.Provider,
            $"{hovered.Point.RemainingPercent}% {RemainingLabel.ToLowerInvariant()}",
            hovered.Point.UpdatedAt.ToLocalTime().ToString("ddd, MMM d · HH:mm", CultureInfo.CurrentCulture),
            hovered.Point.ResetAt is null
                ? string.Empty
                : $"{ResetLabel}: {hovered.Point.ResetAt.Value.ToLocalTime():MMM d, HH:mm}"
        }.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var width = Math.Max(166, lines.Max(line => Text(line, 11, Brushes.White).Width) + 22);
        var height = lines.Length * 18 + 14;
        var left = point.X + 14;
        if (left + width > Bounds.Width - 8)
        {
            left = point.X - width - 14;
        }
        var top = Math.Clamp(point.Y - height - 12, 6, Math.Max(6, Bounds.Height - height - 6));
        var bounds = new Rect(left, top, width, height);
        context.DrawRectangle(Brush("#25332C"), new Pen(Brush("#506158"), 1), bounds, 8, 8);
        context.DrawRectangle(color, null, new Rect(left, top, 4, height), 2, 2);
        for (var index = 0; index < lines.Length; index++)
        {
            DrawText(context, lines[index], new Point(left + 12, top + 8 + index * 18), Brushes.White, 11);
        }
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        var points = _series.SelectMany(series => series.Points).ToArray();
        var plot = GetPlotBounds();
        var pointer = eventArgs.GetPosition(this);
        if (points.Length == 0 || !plot.Contains(pointer))
        {
            if (_hoveredPoint is not null)
            {
                _hoveredPoint = null;
                InvalidateVisual();
            }
            return;
        }

        var (minimumTime, maximumTime) = ResolveTimeBounds(points);
        _hoveredPoint = _series
            .SelectMany(series => series.Points.Select(point => new HoveredPoint(series.Provider, point)))
            .Where(candidate => candidate.Point.UpdatedAt >= minimumTime && candidate.Point.UpdatedAt <= maximumTime)
            .OrderBy(candidate => Math.Abs(X(candidate.Point.UpdatedAt, minimumTime, maximumTime, plot) - pointer.X))
            .ThenBy(candidate => Math.Abs(Y(candidate.Point.RemainingPercent, plot) - pointer.Y))
            .FirstOrDefault();
        InvalidateVisual();
    }

    private Rect GetPlotBounds() => new(
        48,
        29,
        Math.Max(0, Bounds.Width - 68),
        Math.Max(0, Bounds.Height - 70));

    private static double X(DateTimeOffset value, DateTimeOffset minimum, DateTimeOffset maximum, Rect plot)
    {
        var total = (maximum - minimum).TotalMilliseconds;
        var fraction = total <= 0 ? 0.5 : (value - minimum).TotalMilliseconds / total;
        return plot.Left + Math.Clamp(fraction, 0, 1) * plot.Width;
    }

    private static double Y(int remainingPercent, Rect plot) =>
        plot.Bottom - Math.Clamp(remainingPercent, 0, 100) / 100d * plot.Height;

    private static FormattedText Text(string value, double fontSize, IBrush brush) => new(
        value,
        CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight,
        ChartTypeface,
        fontSize,
        brush);

    private static void DrawText(DrawingContext context, string text, Point point, IBrush brush, double fontSize) =>
        context.DrawText(Text(text, fontSize, brush), point);

    private static IBrush ProviderColor(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => Brush("#D46A45"),
        "codex" => Brush("#0F9B72"),
        "antigravity-gemini" => Brush("#5B6FEF"),
        "antigravity-claudeandchatgpt" => Brush("#9A62D7"),
        _ => Brush("#66706A")
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record HoveredPoint(string Provider, UsageHistoryChartPoint Point);
}
