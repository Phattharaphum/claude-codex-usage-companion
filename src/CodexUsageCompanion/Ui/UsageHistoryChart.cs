using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CodexUsageCompanion.Diagnostics;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// Lightweight Avalonia chart for quota history. It deliberately uses the
/// framework drawing API so the desktop app does not need a chart dependency.
/// </summary>
public sealed class UsageHistoryChart : Control
{
    private static readonly IBrush CanvasBrush = Brush("#FBFCFB");
    private static readonly IBrush GridBrush = Brush("#E1E7E2");
    private static readonly IBrush AxisBrush = Brush("#66706A");
    private static readonly IBrush ThresholdBrush = Brush("#D97706");
    private static readonly IBrush ResetBrush = Brush("#9B8060");
    private static readonly Pen GridPen = new(GridBrush, 1);
    private static readonly Pen ThresholdPen = new(ThresholdBrush, 1, DashStyle.Dash);
    private static readonly Pen ResetPen = new(ResetBrush, 1, DashStyle.Dash);
    private static readonly Typeface ChartTypeface = new("Inter");

    private IReadOnlyList<UsageHistoryChartSeries> _series = [];
    private HoveredPoint? _hoveredPoint;

    public string RemainingLabel { get; set; } = "Remaining";
    public string ResetLabel { get; set; } = "Reset";
    public string NoDataText { get; set; } = "No chart data for this selection.";
    public Func<string, string>? ProviderLabelFormatter { get; set; }

    public UsageHistoryChart()
    {
        MinHeight = 230;
        Height = 255;
        PointerMoved += HandlePointerMoved;
        PointerExited += HandlePointerExited;
    }

    public void SetSeries(IReadOnlyList<UsageHistoryChartSeries> series)
    {
        _series = series;
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

        var points = _series.SelectMany(series => series.Points).ToArray();
        if (points.Length == 0)
        {
            DrawText(context, NoDataText, new Point(18, Math.Max(18, Bounds.Height / 2 - 7)), AxisBrush, 12);
            return;
        }

        var minimumTime = points.Min(point => point.UpdatedAt);
        var maximumTime = points.Max(point => point.UpdatedAt);
        if (minimumTime == maximumTime)
        {
            minimumTime -= TimeSpan.FromHours(1);
            maximumTime += TimeSpan.FromHours(1);
        }

        for (var tick = 0; tick <= 4; tick++)
        {
            var percent = 100 - tick * 25;
            var y = plot.Top + plot.Height * tick / 4d;
            context.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(context, $"{percent}%", new Point(4, y - 7), AxisBrush, 10);
        }

        for (var tick = 0; tick <= 4; tick++)
        {
            var x = plot.Left + plot.Width * tick / 4d;
            context.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var time = minimumTime + TimeSpan.FromTicks(
                (maximumTime - minimumTime).Ticks * tick / 4);
            var label = time.ToLocalTime().ToString(
                maximumTime - minimumTime > TimeSpan.FromDays(2) ? "MMM d" : "HH:mm",
                CultureInfo.CurrentCulture);
            var text = new FormattedText(
                label,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ChartTypeface,
                10,
                AxisBrush);
            context.DrawText(text, new Point(x - text.Width / 2, plot.Bottom + 8));
        }

        var thresholdY = plot.Top + plot.Height * 0.8;
        context.DrawLine(ThresholdPen, new Point(plot.Left, thresholdY), new Point(plot.Right, thresholdY));

        var legendX = plot.Left;
        foreach (var series in _series)
        {
            var color = ProviderColor(series.Provider);
            context.DrawLine(new Pen(color, 2), new Point(legendX, 9), new Point(legendX + 18, 9));
            var label = new FormattedText(
                ProviderLabelFormatter?.Invoke(series.Provider) ?? series.Provider,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ChartTypeface,
                10,
                AxisBrush);
            context.DrawText(label, new Point(legendX + 23, 2));
            legendX += 36 + label.Width;
        }

        foreach (var series in _series)
        {
            DrawSeries(context, plot, series, minimumTime, maximumTime);
            foreach (var marker in series.ResetMarkers)
            {
                var x = X(marker, minimumTime, maximumTime, plot);
                context.DrawLine(ResetPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            }
        }

        if (_hoveredPoint is not null)
        {
            DrawTooltip(context, plot, _hoveredPoint, minimumTime, maximumTime);
        }
    }

    private void DrawSeries(
        DrawingContext context,
        Rect plot,
        UsageHistoryChartSeries series,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var pen = new Pen(ProviderColor(series.Provider), 2);
        var previous = (Point?)null;
        foreach (var point in series.Points)
        {
            var current = new Point(
                X(point.UpdatedAt, minimumTime, maximumTime, plot),
                Y(point.RemainingPercent, plot));
            if (previous is Point previousPoint)
            {
                context.DrawLine(pen, previousPoint, current);
            }

            // Dense refresh logs become unreadable when every sample gets a
            // marker. The hover tooltip still identifies the nearest sample.
            if (series.Points.Count <= 80)
            {
                context.DrawEllipse(ProviderColor(series.Provider), null, current, 2.25, 2.25);
            }
            previous = current;
        }
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
        var lines = new[]
        {
            ProviderLabelFormatter?.Invoke(hovered.Provider) ?? hovered.Provider,
            $"{hovered.Point.RemainingPercent}% {RemainingLabel.ToLowerInvariant()}",
            hovered.Point.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            hovered.Point.ResetAt is null
                ? string.Empty
                : $"{ResetLabel}: {hovered.Point.ResetAt.Value.ToLocalTime():MMM d HH:mm}"
        }.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();

        var textWidths = lines.Select(line => new FormattedText(
            line,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            ChartTypeface,
            11,
            Brushes.White).Width);
        var width = Math.Max(148, textWidths.Max() + 20);
        var height = lines.Length * 17 + 12;
        var left = point.X + 12;
        if (left + width > Bounds.Width - 8)
        {
            left = point.X - width - 12;
        }

        var top = point.Y - height - 10;
        if (top < 8)
        {
            top = point.Y + 10;
        }

        var tooltipBounds = new Rect(left, top, width, height);
        context.DrawRectangle(Brush("#26332D"), new Pen(Brush("#506158"), 1), tooltipBounds, 6, 6);
        for (var index = 0; index < lines.Length; index++)
        {
            DrawText(context, lines[index], new Point(left + 10, top + 7 + index * 17), Brushes.White, 11);
        }
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        var points = _series.SelectMany(series => series.Points).ToArray();
        if (points.Length == 0)
        {
            return;
        }

        var plot = GetPlotBounds();
        if (!plot.Contains(eventArgs.GetPosition(this)))
        {
            _hoveredPoint = null;
            InvalidateVisual();
            return;
        }

        var minimumTime = points.Min(point => point.UpdatedAt);
        var maximumTime = points.Max(point => point.UpdatedAt);
        if (minimumTime == maximumTime)
        {
            minimumTime -= TimeSpan.FromHours(1);
            maximumTime += TimeSpan.FromHours(1);
        }

        var pointer = eventArgs.GetPosition(this);
        _hoveredPoint = _series
            .SelectMany(series => series.Points.Select(point => new HoveredPoint(series.Provider, point)))
            .OrderBy(candidate => Math.Abs(
                X(candidate.Point.UpdatedAt, minimumTime, maximumTime, plot) - pointer.X))
            .FirstOrDefault();
        InvalidateVisual();
    }

    private void HandlePointerExited(object? sender, PointerEventArgs eventArgs)
    {
        _hoveredPoint = null;
        InvalidateVisual();
    }

    private Rect GetPlotBounds() => new(
        44,
        22,
        Math.Max(0, Bounds.Width - 62),
        Math.Max(0, Bounds.Height - 58));

    private static double X(
        DateTimeOffset value,
        DateTimeOffset minimum,
        DateTimeOffset maximum,
        Rect plot)
    {
        var total = (maximum - minimum).TotalMilliseconds;
        var fraction = total <= 0 ? 0.5 : (value - minimum).TotalMilliseconds / total;
        return plot.Left + Math.Clamp(fraction, 0, 1) * plot.Width;
    }

    private static double Y(int remainingPercent, Rect plot) =>
        plot.Bottom - Math.Clamp(remainingPercent, 0, 100) / 100d * plot.Height;

    private static void DrawText(
        DrawingContext context,
        string text,
        Point point,
        IBrush brush,
        double fontSize)
    {
        context.DrawText(
            new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ChartTypeface,
                fontSize,
                brush),
            point);
    }

    private static IBrush ProviderColor(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => Brush("#C65D3B"),
        "codex" => Brush("#0F8A5F"),
        "antigravity-gemini" => Brush("#5B6FEF"),
        "antigravity-claudeandchatgpt" => Brush("#8A5CD7"),
        _ => Brush("#66706A")
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record HoveredPoint(string Provider, UsageHistoryChartPoint Point);
}
