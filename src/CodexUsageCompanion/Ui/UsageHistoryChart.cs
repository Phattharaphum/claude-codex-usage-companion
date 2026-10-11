using System.Globalization;
using Avalonia;
using Avalonia.Automation;
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
    private static readonly Typeface ChartTypeface = new("Inter");

    private IBrush _canvasBrush = Brush("#FCFDFC");
    private IBrush _plotBrush = Brush("#F5F8F6");
    private IBrush _gridBrush = Brush("#DFE7E2");
    private IBrush _axisBrush = Brush("#69756E");
    private IBrush _resetBrush = Brush("#E48332");
    private IBrush _hoverGuideBrush = Brush("#8A9890");
    private IBrush _focusBrush = Brush("#0B7A5A");
    private ChartCursor? _cursor;
    private bool _keyboardFocus;
    private IBrush _gapBrush = Brush("#0D000000");
    private IBrush _gapEdgeBrush = Brush("#30000000");
    private IBrush _tooltipBrush = Brush("#25332C");
    private IBrush _tooltipBorderBrush = Brush("#506158");
    private Pen _gridPen = new(Brush("#DFE7E2"), 1);
    private Pen _resetPen = new(Brush("#E48332"), 1.5, DashStyle.Dash);
    private bool _isLight = true;
    private IReadOnlyList<UsageHistoryChartSeries> _series = [];
    private IReadOnlyList<UsageGap> _commonGaps = [];
    private UsageGap? _hoveredGap;
    private HoveredPoint? _hoveredPoint;
    private DateTimeOffset? _viewportStart;
    private DateTimeOffset? _viewportEnd;

    public string RemainingLabel { get; set; } = "Remaining";
    public string ResetLabel { get; set; } = "Reset";
    public string NoDataText { get; set; } = "No chart data for this selection.";

    /// <summary>Legend text for stretches nobody was sampling, e.g. "No data".</summary>
    public string GapLabel { get; set; } = "No data";

    /// <summary>Formats a gap's length for its hover tooltip.</summary>
    public Func<TimeSpan, string> GapDurationFormatter { get; set; } = duration =>
        duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{duration.Minutes}m";
    public Func<string, string>? ProviderLabelFormatter { get; set; }

    /// <summary>Formats axis and tooltip dates in the interface language.</summary>
    public Localization.UiText? Labels { get; set; }

    /// <summary>
    /// What a screen reader announces for the chart as a whole; the arrow keys
    /// then read out individual samples.
    /// </summary>
    public string AccessibleLabel { get; set; } =
        "Remaining quota chart. Use the arrow keys to read each sample.";

    public UsageHistoryChart()
    {
        MinHeight = 270;
        Height = 292;
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        // A chart you can only read with a mouse is not readable to everyone.
        Focusable = true;
        IsTabStop = true;
        KeyDown += HandleKeyDown;
        // The ring marks keyboard focus; a mouse click should not leave one behind.
        GotFocus += (_, eventArgs) =>
        {
            _keyboardFocus = eventArgs.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
            InvalidateVisual();
        };
        LostFocus += (_, _) =>
        {
            _keyboardFocus = false;
            InvalidateVisual();
        };
        PointerMoved += HandlePointerMoved;
        PointerExited += (_, _) =>
        {
            _hoveredPoint = null;
            _hoveredGap = null;
            InvalidateVisual();
        };
    }

    public void SetSeries(IReadOnlyList<UsageHistoryChartSeries> series)
    {
        _series = series;
        _commonGaps = UsageHistorySeriesBuilder.CommonGaps(series);
        _hoveredPoint = null;
        _hoveredGap = null;
        _cursor = null;
        AutomationProperties.SetName(this, AccessibleLabel);
        AutomationProperties.SetItemStatus(this, string.Empty);
        InvalidateVisual();
    }

    public void SetViewport(DateTimeOffset? start, DateTimeOffset? end)
    {
        _viewportStart = start;
        _viewportEnd = end;
        _hoveredPoint = null;
        InvalidateVisual();
    }

    public void SetTheme(bool isLight)
    {
        _isLight = isLight;
        _canvasBrush = Brush(isLight ? "#FFFFFFFF" : "#FF202421");
        _plotBrush = Brush(isLight ? "#FFF7F9F7" : "#FF191D1A");
        _gridBrush = Brush(isLight ? "#FFE1E7E2" : "#FF343A35");
        _axisBrush = Brush(isLight ? "#FF667169" : "#FFADB7AF");
        _resetBrush = Brush(isLight ? "#FFE47B2B" : "#FFFFA04F");
        _hoverGuideBrush = Brush(isLight ? "#FF8A9890" : "#FF758079");
        _focusBrush = Brush(isLight ? "#FF0B7A5A" : "#FF4AD894");
        _gapBrush = Brush(isLight ? "#0D000000" : "#0DFFFFFF");
        _gapEdgeBrush = Brush(isLight ? "#26000000" : "#2EFFFFFF");
        _tooltipBrush = Brush(isLight ? "#FF24312A" : "#FFEEF3EF");
        _tooltipBorderBrush = Brush(isLight ? "#FF506158" : "#FFBEC8C0");
        _gridPen = new Pen(_gridBrush, 1);
        _resetPen = new Pen(_resetBrush, 1.5, DashStyle.Dash);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(_canvasBrush, null, new Rect(Bounds.Size));
        var plot = GetPlotBounds();
        if (plot.Width <= 0 || plot.Height <= 0)
        {
            return;
        }

        context.DrawRectangle(_plotBrush, null, plot, 10, 10);
        if (IsFocused && _keyboardFocus)
        {
            context.DrawRectangle(null, new Pen(_focusBrush, 2), plot.Inflate(2), 11, 11);
        }
        var points = _series.SelectMany(series => series.Points).ToArray();
        var (minimumTime, maximumTime) = ResolveTimeBounds(points);
        DrawGrid(context, plot, minimumTime, maximumTime);
        DrawLegend(context, plot);

        DrawGaps(context, plot, minimumTime, maximumTime);

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
        else if (_hoveredGap is UsageGap hoveredGap)
        {
            DrawGapTooltip(context, plot, hoveredGap, minimumTime, maximumTime);
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
            context.DrawLine(_gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            DrawText(context, $"{percent}%", new Point(7, y - 7), _axisBrush, 10);
        }

        var span = maximumTime - minimumTime;
        for (var tick = 0; tick <= 4; tick++)
        {
            var x = plot.Left + plot.Width * tick / 4d;
            context.DrawLine(_gridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            var time = minimumTime + TimeSpan.FromTicks(span.Ticks * tick / 4);
            var local = time.ToLocalTime();
            var label = span > TimeSpan.FromDays(2)
                ? FormatDateShort(local)
                : span > TimeSpan.FromHours(26)
                    ? $"{FormatWeekdayShort(local)} {local:HH:mm}"
                    : local.ToString("HH:mm", CultureInfo.InvariantCulture);
            var formatted = Text(label, 10, _axisBrush);
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
            var label = Text(ProviderLabelFormatter?.Invoke(series.Provider) ?? series.Provider, 10, _axisBrush);
            context.DrawText(label, new Point(legendX + 13, 5));
            legendX += 28 + label.Width;
        }

        if (_series.SelectMany(series => series.ResetMarkers).Any())
        {
            context.DrawLine(_resetPen, new Point(legendX + 2, 5), new Point(legendX + 2, 19));
            var resetText = Text(ResetLabel, 10, _axisBrush);
            context.DrawText(resetText, new Point(legendX + 9, 5));
            legendX += 24 + resetText.Width;
        }

        if (_commonGaps.Count > 0)
        {
            context.DrawRectangle(_gapBrush, new Pen(_gapEdgeBrush, 1), new Rect(legendX, 6, 12, 10), 2, 2);
            DrawText(context, GapLabel, new Point(legendX + 17, 5), _axisBrush, 10);
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
        context.DrawLine(_resetPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        var triangle = new StreamGeometry();
        using (var geometry = triangle.Open())
        {
            geometry.BeginFigure(new Point(x - 5, plot.Top), true);
            geometry.LineTo(new Point(x + 5, plot.Top));
            geometry.LineTo(new Point(x, plot.Top + 7));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(_resetBrush, null, triangle);
    }

    private void DrawGaps(
        DrawingContext context,
        Rect plot,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var edge = new Pen(_gapEdgeBrush, 1, DashStyle.Dot);
        foreach (var gap in _commonGaps)
        {
            if (gap.End < minimumTime || gap.Start > maximumTime)
            {
                continue;
            }

            var left = X(gap.Start < minimumTime ? minimumTime : gap.Start, minimumTime, maximumTime, plot);
            var right = X(gap.End > maximumTime ? maximumTime : gap.End, minimumTime, maximumTime, plot);
            if (right - left < 1.5)
            {
                continue;
            }

            context.DrawRectangle(_gapBrush, null, new Rect(left, plot.Top, right - left, plot.Height));
            context.DrawLine(edge, new Point(left, plot.Top), new Point(left, plot.Bottom));
            context.DrawLine(edge, new Point(right, plot.Top), new Point(right, plot.Bottom));
        }
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
        // Across a hole the value is unknown, so only hint at a connection.
        var unobservedPen = new Pen(
            new SolidColorBrush(((SolidColorBrush)color).Color, 0.3),
            1.4,
            DashStyle.Dot,
            PenLineCap.Round);
        Point? previous = null;
        var visible = series.Points
            .Where(point => point.UpdatedAt >= minimumTime && point.UpdatedAt <= maximumTime)
            .ToArray();
        foreach (var sample in visible)
        {
            var current = new Point(
                X(sample.UpdatedAt, minimumTime, maximumTime, plot),
                Y(sample.RemainingPercent, plot));
            if (previous is Point previousPoint)
            {
                context.DrawLine(sample.BreaksBefore ? unobservedPen : pen, previousPoint, current);
            }

            if (visible.Length <= 100)
            {
                context.DrawEllipse(_canvasBrush, new Pen(color, 1.5), current, 3, 3);
            }
            previous = current;
        }
    }

    private void DrawEmptyState(DrawingContext context, Rect plot)
    {
        var heading = Text(NoDataText, 12, _axisBrush);
        var center = new Point(plot.Center.X, plot.Center.Y);
        context.DrawEllipse(null, new Pen(_gridBrush, 2), new Point(center.X, center.Y - 14), 13, 13);
        context.DrawLine(new Pen(_gridBrush, 2), new Point(center.X - 5, center.Y - 14), new Point(center.X + 5, center.Y - 14));
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
        context.DrawLine(new Pen(_hoverGuideBrush, 1, DashStyle.Dash),
            new Point(point.X, plot.Top), new Point(point.X, plot.Bottom));
        var color = ProviderColor(hovered.Provider);
        context.DrawEllipse(_canvasBrush, new Pen(color, 2.5), point, 5, 5);
        var lines = new[]
        {
            ProviderLabelFormatter?.Invoke(hovered.Provider) ?? hovered.Provider,
            $"{hovered.Point.RemainingPercent}% {RemainingLabel.ToLowerInvariant()}",
            FormatWeekdayDateTime(hovered.Point.UpdatedAt.ToLocalTime()),
            hovered.Point.ResetAt is null
                ? string.Empty
                : $"{ResetLabel}: {FormatDateTimeShort(hovered.Point.ResetAt.Value.ToLocalTime())}"
        }.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var tooltipText = _isLight ? Brushes.White : Brush("#FF1F2822");
        var width = Math.Max(166, lines.Max(line => Text(line, 11, tooltipText).Width) + 22);
        var height = lines.Length * 18 + 14;
        var left = point.X + 14;
        if (left + width > Bounds.Width - 8)
        {
            left = point.X - width - 14;
        }
        var top = Math.Clamp(point.Y - height - 12, 6, Math.Max(6, Bounds.Height - height - 6));
        var bounds = new Rect(left, top, width, height);
        context.DrawRectangle(_tooltipBrush, new Pen(_tooltipBorderBrush, 1), bounds, 9, 9);
        context.DrawRectangle(color, null, new Rect(left, top, 4, height), 2, 2);
        for (var index = 0; index < lines.Length; index++)
        {
            DrawText(context, lines[index], new Point(left + 12, top + 8 + index * 18), tooltipText, 11);
        }
    }

    private void DrawGapTooltip(
        DrawingContext context,
        Rect plot,
        UsageGap gap,
        DateTimeOffset minimumTime,
        DateTimeOffset maximumTime)
    {
        var lines = new[]
        {
            GapLabel,
            GapDurationFormatter(gap.Duration),
            $"{FormatDateTimeShort(gap.Start.ToLocalTime())} → " +
            gap.End.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
        };
        var tooltipText = _isLight ? Brushes.White : Brush("#FF1F2822");
        var width = Math.Max(150, lines.Max(line => Text(line, 11, tooltipText).Width) + 22);
        var height = lines.Length * 18 + 14;
        var center = X(gap.Start + (gap.End - gap.Start) / 2, minimumTime, maximumTime, plot);
        var left = Math.Clamp(center - width / 2, 6, Math.Max(6, Bounds.Width - width - 6));
        var bounds = new Rect(left, plot.Top + 10, width, height);
        context.DrawRectangle(_tooltipBrush, new Pen(_tooltipBorderBrush, 1), bounds, 9, 9);
        for (var index = 0; index < lines.Length; index++)
        {
            DrawText(context, lines[index], new Point(left + 12, bounds.Top + 8 + index * 18), tooltipText, 11);
        }
    }

    private UsageGap? GapAt(Point pointer, Rect plot, DateTimeOffset minimumTime, DateTimeOffset maximumTime)
    {
        foreach (var gap in _commonGaps)
        {
            var left = X(gap.Start, minimumTime, maximumTime, plot);
            var right = X(gap.End, minimumTime, maximumTime, plot);
            if (right - left >= 1.5 && pointer.X >= left && pointer.X <= right)
            {
                return gap;
            }
        }

        return null;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        var points = _series.SelectMany(series => series.Points).ToArray();
        var plot = GetPlotBounds();
        var pointer = eventArgs.GetPosition(this);
        if (points.Length == 0 || !plot.Contains(pointer))
        {
            if (_hoveredPoint is not null || _hoveredGap is not null)
            {
                _hoveredPoint = null;
                _hoveredGap = null;
                InvalidateVisual();
            }
            return;
        }

        var (minimumTime, maximumTime) = ResolveTimeBounds(points);
        // Inside a "no data" band there is nothing to read off a line; explain
        // the hole instead of snapping to the nearest sample beside it.
        _hoveredGap = GapAt(pointer, plot, minimumTime, maximumTime);
        if (_hoveredGap is not null)
        {
            _hoveredPoint = null;
            InvalidateVisual();
            return;
        }

        // Select by actual on-screen distance. Sorting by time first makes a
        // point on another provider win whenever both samples share (or nearly
        // share) a timestamp, even when the pointer is directly over a line.
        _hoveredPoint = _series
            .SelectMany(series => series.Points.Select(point => new
            {
                Hovered = new HoveredPoint(series.Provider, point),
                ScreenPoint = new Point(
                    X(point.UpdatedAt, minimumTime, maximumTime, plot),
                    Y(point.RemainingPercent, plot))
            }))
            .Where(candidate =>
                candidate.Hovered.Point.UpdatedAt >= minimumTime &&
                candidate.Hovered.Point.UpdatedAt <= maximumTime)
            .Select(candidate => new
            {
                candidate.Hovered,
                DistanceSquared =
                    Math.Pow(candidate.ScreenPoint.X - pointer.X, 2) +
                    Math.Pow(candidate.ScreenPoint.Y - pointer.Y, 2)
            })
            .OrderBy(candidate => candidate.DistanceSquared)
            .Select(candidate => candidate.Hovered)
            .FirstOrDefault();
        _cursor = _hoveredPoint is null
            ? null
            : UsageChartCursor.Find(_series, _hoveredPoint.Provider, _hoveredPoint.Point);
        InvalidateVisual();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        ChartMove? move = eventArgs.Key switch
        {
            Key.Left => ChartMove.PreviousPoint,
            Key.Right => ChartMove.NextPoint,
            Key.Up => ChartMove.PreviousSeries,
            Key.Down => ChartMove.NextSeries,
            Key.Home => ChartMove.FirstPoint,
            Key.End => ChartMove.LastPoint,
            _ => null
        };
        if (eventArgs.Key == Key.Escape && _hoveredPoint is not null)
        {
            _hoveredPoint = null;
            _cursor = null;
            AutomationProperties.SetItemStatus(this, string.Empty);
            InvalidateVisual();
            eventArgs.Handled = true;
            return;
        }

        if (move is null)
        {
            return;
        }

        eventArgs.Handled = true;
        _keyboardFocus = true;
        var next = UsageChartCursor.Move(_series, _cursor, move.Value);
        if (next is not { } cursor)
        {
            return;
        }

        _cursor = cursor;
        var series = _series[cursor.Series];
        var point = series.Points[cursor.Point];
        _hoveredPoint = new HoveredPoint(series.Provider, point);
        _hoveredGap = null;
        // The announcement carries what the tooltip shows, in one sentence.
        var provider = ProviderLabelFormatter?.Invoke(series.Provider) ?? series.Provider;
        AutomationProperties.SetItemStatus(
            this,
            $"{provider}, {point.RemainingPercent}% {RemainingLabel.ToLowerInvariant()}, " +
            FormatWeekdayDateTime(point.UpdatedAt.ToLocalTime()));
        InvalidateVisual();
    }

    private string FormatDateShort(DateTimeOffset local) =>
        Labels?.FormatDateShort(local) ?? local.ToString("MMM d", CultureInfo.InvariantCulture);

    private string FormatWeekdayShort(DateTimeOffset local) =>
        Labels?.FormatWeekday(local.DayOfWeek, abbreviated: true) ??
        local.ToString("ddd", CultureInfo.InvariantCulture);

    private string FormatDateTimeShort(DateTimeOffset local) =>
        Labels?.FormatDateTimeShort(local) ?? local.ToString("MMM d · HH:mm", CultureInfo.InvariantCulture);

    private string FormatWeekdayDateTime(DateTimeOffset local) =>
        Labels?.FormatWeekdayDateTime(local) ?? local.ToString("ddd, MMM d · HH:mm", CultureInfo.InvariantCulture);

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

    private IBrush ProviderColor(string provider) =>
        ProviderStyle.AccentBrush(provider, _isLight, _isLight ? "#66706A" : "#A8B1AA");

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record HoveredPoint(string Provider, UsageHistoryChartPoint Point);
}
