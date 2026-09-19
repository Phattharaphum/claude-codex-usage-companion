using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// Calendar-navigable, local-only dashboard for the optional usage-history CSV.
/// </summary>
public sealed class UsageHistoryWindow : Window
{
    private const int MaximumVisibleRows = 240;
    private readonly CompanionSettings _settings;
    private readonly UiText _text;
    private readonly UsageHistoryReader _reader;
    private readonly WrapPanel _summaryCards = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly WrapPanel _providerFilters = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _periodLabel = new();
    private readonly TextBlock _timelineCaption = new();
    private readonly Button _timelineToggleButton;
    private readonly ScrollViewer _timelineScroll;
    private readonly TextBlock _message = new();
    private readonly Border _noPeriodData;
    private readonly ComboBox _rangeSelector;
    private readonly ComboBox _windowSelector;
    private readonly ToggleSwitch _fullPeriodToggle;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly UsageHistoryChart _chart;
    private IReadOnlyList<UsageHistoryEntry> _entries = [];
    private IReadOnlyList<UsageHistoryEntry> _selection = [];
    private DateTimeOffset _anchorDate = DateTimeOffset.Now;
    private UsageAnalysisWindow? _analysisWindow;
    private int _selectionVersion;
    private int _analysisSelectionVersion = -1;

    public UsageHistoryWindow(
        CompanionSettings settings,
        UiText text,
        UsageHistoryReader? reader = null)
    {
        _settings = settings;
        _text = text;
        _reader = reader ?? new UsageHistoryReader();
        _rangeSelector = new ComboBox
        {
            ItemsSource = new[]
            {
                text.UsageHistory24Hours,
                text.UsageHistory7Days,
                text.UsageHistory30Days,
                text.UsageHistoryAll
            },
            SelectedIndex = 1,
            MinWidth = 112
        };
        _windowSelector = new ComboBox
        {
            ItemsSource = new[]
            {
                text.UsageHistoryWeeklyWindow,
                text.UsageHistoryFiveHourWindow
            },
            SelectedIndex = 0,
            MinWidth = 112
        };
        _fullPeriodToggle = new ToggleSwitch
        {
            OnContent = text.UsageHistoryShowFullPeriod,
            OffContent = text.UsageHistoryDataOnly,
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center
        };
        _previousButton = NavigationButton("‹", text.UsageHistoryPreviousPeriod);
        _nextButton = NavigationButton("›", text.UsageHistoryNextPeriod);
        _timelineToggleButton = new Button
        {
            Content = "▾  " + text.UsageHistoryShowTimeline,
            MinWidth = 118,
            Padding = new Thickness(12, 6)
        };
        _timelineToggleButton.Click += (_, _) => ToggleTimeline();
        _chart = new UsageHistoryChart
        {
            RemainingLabel = text.UsageHistoryRemaining,
            ResetLabel = text.UsageHistoryReset,
            NoDataText = text.UsageHistoryNoChartData,
            ProviderLabelFormatter = ChartProviderLabel
        };
        _noPeriodData = new Border
        {
            Background = Brush("#FFF7E8"),
            BorderBrush = Brush("#F2D39B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13, 10),
            IsVisible = false,
            Child = new TextBlock
            {
                Text = text.UsageHistoryNoPeriodData,
                Foreground = Brush("#865B19"),
                TextWrapping = TextWrapping.Wrap
            }
        };

        _rangeSelector.SelectionChanged += (_, _) =>
        {
            _anchorDate = DateTimeOffset.Now;
            ApplySelection();
        };
        _windowSelector.SelectionChanged += (_, _) => ApplySelection();
        _fullPeriodToggle.IsCheckedChanged += (_, _) => ApplySelection();
        _previousButton.Click += (_, _) => MovePeriod(-1);
        _nextButton.Click += (_, _) => MovePeriod(1);

        Title = text.UsageHistoryTitle;
        Width = 1080;
        Height = 820;
        MinWidth = 820;
        MinHeight = 620;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var refresh = PrimaryButton("↻  " + text.RefreshAction);
        refresh.Click += (_, _) => Reload();
        var analyze = PrimaryButton("✦  " + text.UsageHistoryAnalyzeAction);
        analyze.Click += (_, _) => ShowAnalysis();
        var close = new Button { Content = text.CloseAction, MinWidth = 88, IsCancel = true };
        close.Click += (_, _) => Close();

        var title = new TextBlock
        {
            Text = text.UsageHistoryDashboardTitle,
            FontSize = 25,
            FontWeight = FontWeight.Bold
        };
        _subtitle.FontSize = 12;
        _subtitle.Foreground = Brush("#68756D");
        var titleStack = new StackPanel { Spacing = 3, Children = { title, _subtitle } };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { analyze, refresh, close }
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(actions, 1);
        header.Children.Add(titleStack);
        header.Children.Add(actions);

        _periodLabel.FontSize = 15;
        _periodLabel.FontWeight = FontWeight.SemiBold;
        _periodLabel.MinWidth = 210;
        _periodLabel.TextAlignment = TextAlignment.Center;
        _periodLabel.VerticalAlignment = VerticalAlignment.Center;
        var periodNavigation = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _previousButton, _periodLabel, _nextButton }
        };
        var filterRow = new WrapPanel { Orientation = Orientation.Horizontal };
        filterRow.Children.Add(FilterGroup(text.UsageHistoryTimeRange, _rangeSelector));
        filterRow.Children.Add(periodNavigation);
        var filterCard = new Border
        {
            Background = Brush("#F5F8F6"),
            BorderBrush = Brush("#DCE4DF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 11),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    filterRow,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        ColumnSpacing = 10,
                        Children = { FilterLabel(text.UsageHistoryProvider), _providerFilters }
                    }
                }
            }
        };
        Grid.SetColumn(_providerFilters, 1);

        var overviewHeading = SectionHeading(text.UsageHistorySummaryTitle, text.UsageHistorySummarySubtitle);
        var summaryScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _summaryCards
        };

        var chartControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { FilterGroup(text.UsageHistoryQuotaWindow, _windowSelector), _fullPeriodToggle }
        };
        var chartHeading = SectionHeading(text.UsageHistoryChartTitle, text.UsageHistoryChartSubtitle, chartControls);
        var chartFrame = new Border
        {
            Background = Brush("#FCFDFC"),
            BorderBrush = Brush("#D8E2DC"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = _chart
        };

        _message.TextWrapping = TextWrapping.Wrap;
        _message.Foreground = Brush("#A13C34");
        _message.IsVisible = false;
        _timelineCaption.Foreground = Brush("#68756D");
        _timelineCaption.FontSize = 11;
        var timelineActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _timelineCaption, _timelineToggleButton }
        };
        var timelineHeading = SectionHeading(text.UsageHistoryRecords, string.Empty, timelineActions);
        var tableContent = new StackPanel { Spacing = 7 };
        tableContent.Children.Add(CreateTableHeader());
        tableContent.Children.Add(_rows);
        _timelineScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = tableContent,
            IsVisible = false
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(24),
            RowSpacing = 12
        };
        AddRow(layout, header, 0);
        AddRow(layout, filterCard, 1);
        AddRow(layout, overviewHeading, 2);
        AddRow(layout, summaryScroll, 3);
        AddRow(layout, chartHeading, 4);
        AddRow(layout, chartFrame, 5);
        AddRow(layout, _noPeriodData, 6);
        AddRow(layout, _message, 7);
        AddRow(layout, timelineHeading, 8);
        AddRow(layout, _timelineScroll, 9);
        Content = layout;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Reload();
    }

    public void Reload()
    {
        var result = _reader.Read(_settings.UsageLogFilePath, _settings.UsageLogFormat);
        _message.IsVisible = !string.IsNullOrWhiteSpace(result.Error);
        _message.Text = result.Error ?? string.Empty;
        _entries = result.Entries;
        _subtitle.Text = _entries.Count == 0
            ? _text.UsageHistoryEmptySubtitle
            : _text.FormatUsageHistoryCount(_entries.Count, MaximumVisibleRows);

        var selectedBefore = _providerFilters.Children
            .OfType<CheckBox>()
            .Where(item => item.IsChecked == true && item.Tag is string)
            .Select(item => (string)item.Tag!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _providerFilters.Children.Clear();
        foreach (var provider in _entries
                     .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
                     .Where(entry => IsChartProvider(entry.Provider))
                     .Select(entry => entry.Provider)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(ProviderOrder)
                     .ThenBy(provider => provider, StringComparer.OrdinalIgnoreCase))
        {
            var checkBox = new CheckBox
            {
                Content = DisplayProvider(provider),
                IsChecked = selectedBefore.Count == 0 || selectedBefore.Contains(provider),
                Tag = provider,
                FontSize = 12,
                Margin = new Thickness(0, 0, 14, 2)
            };
            checkBox.IsCheckedChanged += (_, _) => ApplySelection();
            _providerFilters.Children.Add(checkBox);
        }
        ApplySelection();
    }

    private void ApplySelection()
    {
        _selectionVersion++;
        var providers = SelectedProviders();
        var (start, end) = SelectedPeriod();
        _selection = UsageHistoryAnalytics.SelectEntries(_entries, start, end, providers);
        var quotaWindow = _windowSelector.SelectedIndex == 1
            ? UsageHistoryQuotaWindow.FiveHour
            : UsageHistoryQuotaWindow.Weekly;
        var series = UsageHistorySeriesBuilder.BuildForPeriod(_entries, quotaWindow, start, end, providers);
        _chart.SetViewport(_fullPeriodToggle.IsChecked == true ? start : null,
            _fullPeriodToggle.IsChecked == true ? end : null);
        _chart.SetSeries(series);
        UpdatePeriodLabel(start, end);
        UpdateSummary(_selection);
        UpdateTimeline(start, end, providers);
        _noPeriodData.IsVisible = _selection.Count == 0 && _entries.Count > 0;
        _nextButton.IsEnabled = _rangeSelector.SelectedIndex != 3 && end < StartOfToday();
        _previousButton.IsEnabled = _rangeSelector.SelectedIndex != 3;
    }

    private void UpdateSummary(IReadOnlyList<UsageHistoryEntry> entries)
    {
        _summaryCards.Children.Clear();
        var summary = UsageHistoryAnalytics.Summarize(entries);
        var resets = UsageHistoryAnalytics.FindResetEvents(entries).Count;
        _summaryCards.Children.Add(SummaryCard(
            _text.UsageHistorySamples,
            summary.RecordCount.ToString(CultureInfo.CurrentCulture),
            _text.FormatUsageHistoryProviders(summary.ProviderCount),
            "#0F8A5F"));
        _summaryCards.Children.Add(SummaryCard(
            _text.UsageHistoryObservedTime,
            FormatDuration(summary.ObservedDuration),
            _text.UsageHistoryDataCoverage,
            "#4E6FAE"));
        _summaryCards.Children.Add(SummaryCard(
            _text.UsageHistoryFiveHourConsumed,
            $"{summary.FiveHour.ConsumedPercent:0.#}%",
            _text.FormatUsageHistoryBurnRate(summary.FiveHour.ConsumptionPerHour),
            "#D46A45"));
        _summaryCards.Children.Add(SummaryCard(
            _text.UsageHistoryWeeklyConsumed,
            $"{summary.Weekly.ConsumedPercent:0.#}%",
            _text.FormatUsageHistoryBurnRate(summary.Weekly.ConsumptionPerHour),
            "#8A5CD7"));
        _summaryCards.Children.Add(SummaryCard(
            _text.UsageHistoryResetsDetected,
            resets.ToString(CultureInfo.CurrentCulture),
            _text.UsageHistoryResetMarkersHint,
            "#D97706"));
    }

    private void UpdateTimeline(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlySet<string> providers)
    {
        _rows.Children.Clear();
        var records = _entries
            .Where(entry => entry.UpdatedAt >= start && entry.UpdatedAt <= end)
            .Where(entry => providers.Contains(entry.Provider))
            .Take(MaximumVisibleRows)
            .ToArray();
        _timelineCaption.Text = _text.FormatUsageHistoryVisibleRecords(records.Length, _selection.Count);
        if (records.Length == 0)
        {
            _rows.Children.Add(CreateEmptyState(
                _entries.Count == 0 ? _text.UsageHistoryEmpty : _text.UsageHistoryNoPeriodData));
            return;
        }

        foreach (var entry in records)
        {
            _rows.Children.Add(CreateRow(entry));
        }
    }

    private void MovePeriod(int direction)
    {
        var days = _rangeSelector.SelectedIndex switch
        {
            0 => 1,
            1 => 7,
            2 => 30,
            _ => 0
        };
        if (days == 0)
        {
            return;
        }
        _anchorDate = _anchorDate.AddDays(days * direction);
        ApplySelection();
    }

    private void ToggleTimeline()
    {
        _timelineScroll.IsVisible = !_timelineScroll.IsVisible;
        _timelineToggleButton.Content = _timelineScroll.IsVisible
            ? "▴  " + _text.UsageHistoryHideTimeline
            : "▾  " + _text.UsageHistoryShowTimeline;
    }

    private (DateTimeOffset Start, DateTimeOffset End) SelectedPeriod()
    {
        if (_rangeSelector.SelectedIndex == 3)
        {
            var available = _entries.Where(entry => IsChartProvider(entry.Provider)).ToArray();
            return available.Length == 0
                ? (DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now)
                : (available.Min(entry => entry.UpdatedAt), available.Max(entry => entry.UpdatedAt));
        }

        var localDate = _anchorDate.ToLocalTime().Date;
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDate);
        var end = new DateTimeOffset(localDate.AddDays(1), offset).AddTicks(-1);
        var days = _rangeSelector.SelectedIndex switch { 0 => 1, 2 => 30, _ => 7 };
        var startDate = localDate.AddDays(-(days - 1));
        return (new DateTimeOffset(startDate, TimeZoneInfo.Local.GetUtcOffset(startDate)), end);
    }

    private void UpdatePeriodLabel(DateTimeOffset start, DateTimeOffset end)
    {
        if (_rangeSelector.SelectedIndex == 3)
        {
            _periodLabel.Text = _entries.Count == 0
                ? _text.UsageHistoryAll
                : $"{start.ToLocalTime():MMM d, yyyy} — {end.ToLocalTime():MMM d, yyyy}";
            return;
        }
        _periodLabel.Text = start.ToLocalTime().Date == end.ToLocalTime().Date
            ? start.ToLocalTime().ToString("dddd, MMM d, yyyy", CultureInfo.CurrentCulture)
            : $"{start.ToLocalTime():MMM d, yyyy} — {end.ToLocalTime():MMM d, yyyy}";
    }

    private void ShowAnalysis()
    {
        if (_analysisWindow is not null && _analysisSelectionVersion == _selectionVersion)
        {
            _analysisWindow.Activate();
            return;
        }
        _analysisWindow?.Close();
        var (start, end) = SelectedPeriod();
        var dialog = new UsageAnalysisWindow(_selection, start, end, _text)
        {
            ShowInTaskbar = _settings.ShowTaskbarIcon,
            Topmost = _settings.AlwaysOnTop
        };
        _analysisWindow = dialog;
        _analysisSelectionVersion = _selectionVersion;
        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_analysisWindow, dialog))
            {
                _analysisWindow = null;
            }
        };
        dialog.Show(this);
    }

    private IReadOnlySet<string> SelectedProviders() => _providerFilters.Children
        .OfType<CheckBox>()
        .Where(item => item.IsChecked == true && item.Tag is string)
        .Select(item => (string)item.Tag!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static DateTimeOffset StartOfToday()
    {
        var today = DateTime.Today;
        return new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));
    }

    private static Control FilterGroup(string label, Control control) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Margin = new Thickness(0, 0, 18, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Children = { FilterLabel(label), control }
    };

    private static TextBlock FilterLabel(string text) => new()
    {
        Text = text + ":",
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brush("#68756D"),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Button NavigationButton(string content, string tooltip)
    {
        var button = new Button
        {
            Content = content,
            Width = 34,
            Height = 32,
            Padding = new Thickness(0),
            FontSize = 21,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Button PrimaryButton(string content) => new()
    {
        Content = content,
        MinWidth = 112,
        Padding = new Thickness(13, 7)
    };

    private static Control SectionHeading(string title, string subtitle, Control? trailing = null)
    {
        var titleStack = new StackPanel { Spacing = 1 };
        titleStack.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeight.SemiBold });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            titleStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = Brush("#718078") });
        }
        if (trailing is null)
        {
            return titleStack;
        }
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(trailing, 1);
        grid.Children.Add(titleStack);
        grid.Children.Add(trailing);
        return grid;
    }

    private static Border SummaryCard(string label, string value, string detail, string accent)
    {
        var card = new Border
        {
            Width = 192,
            MinHeight = 88,
            Background = Brush("#F8FAF9"),
            BorderBrush = Brush("#DCE4DF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(13),
            Margin = new Thickness(0, 0, 9, 6)
        };
        card.Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4,*"),
            ColumnSpacing = 10,
            Children =
            {
                new Border { Background = Brush(accent), CornerRadius = new CornerRadius(2) },
                new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#68756D") },
                        new TextBlock { Text = value, FontSize = 21, FontWeight = FontWeight.Bold },
                        new TextBlock { Text = detail, FontSize = 10, Foreground = Brush("#718078"), TextTrimming = TextTrimming.CharacterEllipsis }
                    }
                }
            }
        };
        Grid.SetColumn(((Grid)card.Child).Children[1], 1);
        return card;
    }

    private Control CreateTableHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("165,210,*,*"),
            Margin = new Thickness(13, 0)
        };
        AddHeaderCell(grid, _text.UsageHistoryTimestamp, 0);
        AddHeaderCell(grid, _text.UsageHistoryProvider, 1);
        AddHeaderCell(grid, _text.UsageHistoryFiveHour, 2);
        AddHeaderCell(grid, _text.UsageHistoryWeek, 3);
        return grid;
    }

    private static void AddHeaderCell(Grid grid, string text, int column)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#68756D")
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private Control CreateRow(UsageHistoryEntry entry)
    {
        var row = new Border
        {
            Background = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)
                ? Brush("#FCFDFC") : Brush("#FFF5F3"),
            BorderBrush = Brush("#E0E6E2"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13, 9)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("165,210,*,*") };
        grid.Children.Add(new TextBlock
        {
            Text = entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm", CultureInfo.CurrentCulture),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        });
        var provider = CreateProviderPill(entry);
        var fiveHour = CreateLimitCell(entry.FiveHourRemainingPercent, entry.FiveHourResetAt, entry.Error);
        var week = CreateLimitCell(entry.WeeklyRemainingPercent, entry.WeeklyResetAt, entry.Error);
        Grid.SetColumn(provider, 1);
        Grid.SetColumn(fiveHour, 2);
        Grid.SetColumn(week, 3);
        grid.Children.Add(provider);
        grid.Children.Add(fiveHour);
        grid.Children.Add(week);
        row.Child = grid;
        return row;
    }

    private Control CreateProviderPill(UsageHistoryEntry entry) => new Border
    {
        Background = ProviderBrush(entry.Provider, 0.13),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(9, 4),
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock
        {
            Text = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)
                ? DisplayProvider(entry.Provider)
                : $"{DisplayProvider(entry.Provider)} · {_text.UsageHistoryError}",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ProviderBrush(entry.Provider, 1)
        }
    };

    private Control CreateLimitCell(int? remainingPercent, DateTimeOffset? resetAt, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            return new TextBlock
            {
                Text = error,
                Foreground = Brush("#A13C34"),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 12, 0) };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new TextBlock
        {
            Text = resetAt is null ? _text.ResetUnavailable : _text.FormatUsageHistoryReset(resetAt.Value.ToLocalTime()),
            FontSize = 10,
            Foreground = Brush("#68756D"),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var percent = new TextBlock
        {
            Text = Percent(remainingPercent),
            FontWeight = FontWeight.Bold,
            Foreground = SignalBrush(remainingPercent),
            FontSize = 13
        };
        Grid.SetColumn(percent, 1);
        heading.Children.Add(percent);
        stack.Children.Add(heading);
        stack.Children.Add(CreateProgress(remainingPercent));
        return stack;
    }

    private static Control CreateProgress(int? remainingPercent)
    {
        var fill = new Border
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(3),
            Background = SignalBrush(remainingPercent)
        };
        var track = new Border
        {
            Height = 6,
            Background = Brush("#DDE4DF"),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) =>
            fill.Width = Math.Round(track.Bounds.Width * Math.Clamp(remainingPercent ?? 0, 0, 100) / 100d);
        return track;
    }

    private static Control CreateEmptyState(string text) => new Border
    {
        Background = Brush("#F6F8F6"),
        BorderBrush = Brush("#D9DFD9"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(22),
        Child = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#68756D"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        }
    };

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            Close();
        }
        else if (eventArgs.Key == Key.Left && eventArgs.KeyModifiers == KeyModifiers.Alt)
        {
            MovePeriod(-1);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Right && eventArgs.KeyModifiers == KeyModifiers.Alt)
        {
            MovePeriod(1);
            eventArgs.Handled = true;
        }
    }

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private string DisplayProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · Claude + ChatGPT",
        _ => provider
    };

    private string ChartProviderLabel(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · C+GPT",
        _ => provider
    };

    private static bool IsChartProvider(string provider) => provider.ToLowerInvariant() is
        "claude" or "codex" or "antigravity-gemini" or "antigravity-claudeandchatgpt";

    private static int ProviderOrder(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => 0,
        "codex" => 1,
        "antigravity-gemini" => 2,
        "antigravity-claudeandchatgpt" => 3,
        _ => 4
    };

    private static string Percent(int? value) => value is null ? "—" : $"{Math.Clamp(value.Value, 0, 100)}%";

    private static string FormatDuration(TimeSpan duration) => duration.TotalDays >= 1
        ? $"{(int)duration.TotalDays}d {duration.Hours}h"
        : duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{Math.Max(0, duration.Minutes)}m";

    private static IBrush SignalBrush(int? remainingPercent) => (remainingPercent ?? 0) switch
    {
        < 40 => Brush("#D84A42"),
        < 60 => Brush("#D97706"),
        < 80 => Brush("#A98700"),
        _ => Brush("#0F8A5F")
    };

    private static IBrush ProviderBrush(string provider, double opacity)
    {
        var color = provider.ToLowerInvariant() switch
        {
            "claude" => Color.Parse("#C65D3B"),
            "codex" => Color.Parse("#0F8A5F"),
            "antigravity-gemini" => Color.Parse("#5B6FEF"),
            "antigravity-claudeandchatgpt" => Color.Parse("#8A5CD7"),
            _ => Color.Parse("#66706A")
        };
        return new SolidColorBrush(color, opacity);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
