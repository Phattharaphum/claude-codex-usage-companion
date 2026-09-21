using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
    private readonly Grid _summaryGrid = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 10 };
    private readonly WrapPanel _providerFilters = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _periodLabel = new();
    private readonly TextBlock _timelineCaption = new();
    private readonly Button _timelineToggleButton;
    private readonly TextBlock _message = new();
    private readonly TextBlock _emptyStateText;
    private readonly Border _noPeriodData;
    private readonly ComboBox _rangeSelector;
    private readonly ComboBox _windowSelector;
    private readonly Border _rangeSegmentedSurface;
    private readonly Dictionary<int, Button> _rangeSegmentedButtons = [];
    private readonly Border _periodCalendarBadge;
    private readonly Avalonia.Controls.Shapes.Path _periodCalendarPath;
    private readonly Button _todayButton;
    private readonly Avalonia.Controls.Shapes.Path _todayIcon;
    private readonly TextBlock _todayLabel;
    private readonly Button _allProvidersButton;
    private readonly Avalonia.Controls.Shapes.Path _allProvidersIcon;
    private readonly TextBlock _allProvidersLabel;
    private readonly Button _fullPeriodToggle;
    private readonly Border _fullPeriodTrack;
    private readonly Border _fullPeriodThumb;
    private readonly TextBlock _fullPeriodLabel;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _refreshButton;
    private readonly Button _analyzeButton;
    private readonly UsageHistoryChart _chart;
    private readonly Border _root;
    private readonly Border _windowTitleBar;
    private readonly TextBlock _windowTitleText;
    private readonly Button _windowMinimizeButton;
    private readonly Button _windowMaximizeButton;
    private readonly Button _windowCloseButton;
    private readonly Border _headerCard;
    private readonly Border _filterCard;
    private readonly Border _chartSection;
    private readonly Border _chartFrame;
    private readonly Border _timelineCard;
    private readonly Border _periodNavigationSurface;
    private readonly Border _titleMark;
    private readonly Avalonia.Controls.Shapes.Ellipse _titleClockRing;
    private readonly Avalonia.Controls.Shapes.Path _titleClockHands;
    private readonly TextBlock _titleText;
    private readonly List<TextBlock> _mutedText = [];
    private readonly Dictionary<Button, HistoryButtonRole> _buttonRoles = [];
    private readonly List<Button> _providerButtons = [];
    private readonly HashSet<string> _selectedProviders = new(StringComparer.OrdinalIgnoreCase);
    private HistoryPalette _palette = HistoryPalette.Light;
    private bool _isLightTheme = true;
    private bool _showFullPeriod = true;
    private IReadOnlyList<UsageHistoryEntry> _entries = [];
    private IReadOnlyList<UsageHistoryEntry> _selection = [];
    private IReadOnlyList<UsageHistoryEntry> _timelineRecords = [];
    private DateTimeOffset _anchorDate = DateTimeOffset.Now;
    private UsageAnalysisWindow? _analysisWindow;
    private UsageTimelineWindow? _timelineWindow;
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
            IsVisible = false
        };

        _rangeSegmentedSurface = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3)
        };
        var rangePillsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3
        };
        _rangeSegmentedSurface.Child = rangePillsPanel;

        var rangeOptions = new (int Index, string Label, string Tooltip)[]
        {
            (0, "24H", text.UsageHistory24Hours),
            (1, "7D", text.UsageHistory7Days),
            (2, "30D", text.UsageHistory30Days),
            (3, text.UsageHistoryAll, text.UsageHistoryAll)
        };

        foreach (var (index, label, tooltip) in rangeOptions)
        {
            var pill = new Button
            {
                Content = label,
                Height = 28,
                MinWidth = 42,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(pill, tooltip);
            var pillIndex = index;
            pill.Click += (_, _) =>
            {
                if (_rangeSelector.SelectedIndex != pillIndex)
                {
                    _rangeSelector.SelectedIndex = pillIndex;
                }
                else
                {
                    _anchorDate = DateTimeOffset.Now;
                    ApplySelection();
                }
            };
            pill.PointerEntered += (_, _) => ApplyRangePillTheme(pill, pillIndex, hovered: true);
            pill.PointerExited += (_, _) => ApplyRangePillTheme(pill, pillIndex, hovered: false);
            _rangeSegmentedButtons[pillIndex] = pill;
            rangePillsPanel.Children.Add(pill);
        }

        _periodCalendarPath = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M19 4h-1V2h-2v2H8V2H6v2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V9h14v11zM7 11h2v2H7v-2zm4 0h2v2h-2v-2zm4 0h2v2h-2v-2z"),
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _periodCalendarBadge = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _periodCalendarPath
        };

        _todayIcon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M19 3h-1V1h-2v2H8V1H6v2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V8h14v11z"),
            Width = 11,
            Height = 11,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        _todayLabel = new TextBlock
        {
            Text = text.UsageHistoryToday,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _todayButton = new Button
        {
            Height = 34,
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _todayIcon, _todayLabel }
            }
        };
        ToolTip.SetTip(_todayButton, text.UsageHistoryToday);
        _todayButton.Click += (_, _) =>
        {
            _anchorDate = DateTimeOffset.Now;
            ApplySelection();
        };
        _todayButton.PointerEntered += (_, _) => ApplyTodayButtonTheme(hovered: true);
        _todayButton.PointerExited += (_, _) => ApplyTodayButtonTheme(hovered: false);

        _allProvidersIcon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M4 4h4v4H4zm6 0h4v4h-4zm6 0h4v4h-4zM4 10h4v4H4zm6 0h4v4h-4zm6 0h4v4h-4zM4 16h4v4H4zm6 0h4v4h-4zm6 0h4v4h-4z"),
            Width = 11,
            Height = 11,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        _allProvidersLabel = new TextBlock
        {
            Text = text.UsageHistoryAllProviders,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _allProvidersButton = new Button
        {
            Height = 30,
            Padding = new Thickness(10, 0, 12, 0),
            Margin = new Thickness(0, 0, 7, 3),
            CornerRadius = new CornerRadius(15),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _allProvidersIcon, _allProvidersLabel }
            }
        };
        ToolTip.SetTip(_allProvidersButton, text.UsageHistoryAllProviders);
        _allProvidersButton.Click += (_, _) =>
        {
            var allChartProviders = _entries
                .Where(e => string.Equals(e.Status, "success", StringComparison.OrdinalIgnoreCase))
                .Where(e => IsChartProvider(e.Provider))
                .Select(e => e.Provider)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (allChartProviders.Count == 0) return;

            if (_selectedProviders.Count < allChartProviders.Count)
            {
                foreach (var p in allChartProviders)
                {
                    _selectedProviders.Add(p);
                }
            }
            foreach (var btn in _providerButtons)
            {
                ApplyProviderButtonTheme(btn);
            }
            ApplyAllProvidersButtonTheme();
            ApplySelection();
        };
        _allProvidersButton.PointerEntered += (_, _) => ApplyAllProvidersButtonTheme(hovered: true);
        _allProvidersButton.PointerExited += (_, _) => ApplyAllProvidersButtonTheme(hovered: false);

        _windowSelector = new ComboBox
        {
            ItemsSource = new[]
            {
                text.UsageHistoryWeeklyWindow,
                text.UsageHistoryFiveHourWindow
            },
            SelectedIndex = 0,
            MinWidth = 118,
            Height = 34,
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(10, 0)
        };
        _fullPeriodThumb = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _fullPeriodTrack = new Border
        {
            Width = 34,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Child = _fullPeriodThumb
        };
        _fullPeriodLabel = new TextBlock
        {
            Text = text.UsageHistoryShowFullPeriod,
            FontSize = 11.5,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };
        _fullPeriodToggle = new Button
        {
            Height = 34,
            Padding = new Thickness(9, 0),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { _fullPeriodTrack, _fullPeriodLabel }
            }
        };
        _previousButton = NavigationButton(CreateChevronIcon(pointsRight: false), text.UsageHistoryPreviousPeriod);
        _nextButton = NavigationButton(CreateChevronIcon(pointsRight: true), text.UsageHistoryNextPeriod);
        _timelineToggleButton = new Button
        {
            Content = "↗  " + text.UsageHistoryShowTimeline,
            MinWidth = 126,
            Height = 36,
            Padding = new Thickness(13, 0),
            CornerRadius = new CornerRadius(10),
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _timelineToggleButton.Click += (_, _) => ShowTimelineWindow();
        _chart = new UsageHistoryChart
        {
            RemainingLabel = text.UsageHistoryRemaining,
            ResetLabel = text.UsageHistoryReset,
            NoDataText = text.UsageHistoryNoChartData,
            ProviderLabelFormatter = ChartProviderLabel,
            Height = 238,
            MinHeight = 220
        };
        _emptyStateText = new TextBlock
        {
            Text = text.UsageHistoryNoPeriodData,
            TextWrapping = TextWrapping.Wrap
        };
        _noPeriodData = new Border
        {
            Background = Brush("#FFF7E8"),
            BorderBrush = Brush("#F2D39B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 11),
            Margin = new Thickness(0, 10, 0, 0),
            IsVisible = false,
            Child = _emptyStateText
        };

        _rangeSelector.SelectionChanged += (_, _) =>
        {
            _anchorDate = DateTimeOffset.Now;
            UpdateRangePillsStyle();
            ApplySelection();
        };
        _windowSelector.SelectionChanged += (_, _) => ApplySelection();
        _fullPeriodToggle.Click += (_, _) =>
        {
            _showFullPeriod = !_showFullPeriod;
            ApplyFullPeriodToggleTheme();
            ApplySelection();
        };
        _fullPeriodToggle.PointerEntered += (_, _) => ApplyFullPeriodToggleTheme(hovered: true);
        _fullPeriodToggle.PointerExited += (_, _) => ApplyFullPeriodToggleTheme();
        _previousButton.Click += (_, _) => MovePeriod(-1);
        _nextButton.Click += (_, _) => MovePeriod(1);

        Title = text.UsageHistoryTitle;
        Width = 1120;
        Height = 836;
        MinWidth = 860;
        MinHeight = 660;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _windowTitleText = new TextBlock
        {
            Text = text.UsageHistoryTitle.Replace(" - Claude Codex Usage Companion", string.Empty),
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _windowMinimizeButton = WindowControlButton(
            CreateWindowIcon("M4 11H20V13H4Z"),
            text.MinimizeAction);
        _windowMaximizeButton = WindowControlButton(
            CreateWindowIcon("M5 5H19V7H5ZM5 17H19V19H5ZM5 7H7V17H5ZM17 7H19V17H17Z"),
            "Maximize");
        _windowCloseButton = WindowControlButton(
            CreateWindowIcon("M6.7 5.3L12 10.6L17.3 5.3L18.7 6.7L13.4 12L18.7 17.3L17.3 18.7L12 13.4L6.7 18.7L5.3 17.3L10.6 12L5.3 6.7Z"),
            text.CloseAction);
        _windowMinimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        _windowMaximizeButton.Click += (_, _) => ToggleMaximized();
        _windowCloseButton.Click += (_, _) => Close();
        RegisterWindowControlHover(_windowMinimizeButton, closeButton: false);
        RegisterWindowControlHover(_windowMaximizeButton, closeButton: false);
        RegisterWindowControlHover(_windowCloseButton, closeButton: true);
        var titleBarActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _windowMinimizeButton, _windowMaximizeButton, _windowCloseButton }
        };
        var titleBarLeft = new Border { Background = Brushes.Transparent };
        var titleBarCenter = new Border
        {
            Background = Brushes.Transparent,
            Child = _windowTitleText
        };
        titleBarLeft.PointerPressed += HandleTitleBarPointerPressed;
        titleBarCenter.PointerPressed += HandleTitleBarPointerPressed;
        var titleBarGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("100,*,100"),
            Height = 38
        };
        Grid.SetColumn(titleBarCenter, 1);
        Grid.SetColumn(titleBarActions, 2);
        titleBarGrid.Children.Add(titleBarLeft);
        titleBarGrid.Children.Add(titleBarCenter);
        titleBarGrid.Children.Add(titleBarActions);
        _windowTitleBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            Child = titleBarGrid
        };

        _refreshButton = ActionButton("↻  " + text.RefreshAction, 104);
        _refreshButton.Click += (_, _) => Reload();
        _analyzeButton = ActionButton("✦  " + text.UsageHistoryAnalyzeAction, 132);
        _analyzeButton.Click += (_, _) => ShowAnalysis();

        _titleText = new TextBlock
        {
            Text = text.UsageHistoryDashboardTitle,
            FontSize = 26,
            FontWeight = FontWeight.Bold
        };
        _subtitle.FontSize = 12;
        _mutedText.Add(_subtitle);
        var titleStack = new StackPanel { Spacing = 3, Children = { _titleText, _subtitle } };
        _titleClockRing = new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 18,
            Height = 18,
            StrokeThickness = 1.6
        };
        Canvas.SetLeft(_titleClockRing, 1);
        Canvas.SetTop(_titleClockRing, 1);
        _titleClockHands = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M10 5.2L10 10L14 12.2"),
            StrokeThickness = 1.7,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };
        var titleClock = new Canvas
        {
            Width = 20,
            Height = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleClockRing, _titleClockHands }
        };
        _titleMark = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(13),
            Child = titleClock
        };
        var titleArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleMark, titleStack }
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _analyzeButton, _refreshButton }
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(actions, 1);
        header.Children.Add(titleArea);
        header.Children.Add(actions);
        _headerCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16, 12),
            Child = header
        };
        _periodLabel.FontSize = 14;
        _periodLabel.FontWeight = FontWeight.SemiBold;
        _periodLabel.MinWidth = 210;
        _periodLabel.TextAlignment = TextAlignment.Center;
        _periodLabel.VerticalAlignment = VerticalAlignment.Center;
        var periodCenterStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _periodCalendarBadge, _periodLabel }
        };
        var periodNavigation = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _previousButton, periodCenterStack, _nextButton, _todayButton }
        };
        _periodNavigationSurface = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(4),
            Child = periodNavigation
        };
        var filterRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 12
        };
        var rangeGroup = FilterGroup(text.UsageHistoryTimeRange, _rangeSegmentedSurface);
        Grid.SetColumn(_periodNavigationSurface, 2);
        filterRow.Children.Add(rangeGroup);
        filterRow.Children.Add(_periodNavigationSurface);
        var providerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 12,
            Children = { FilterLabel(text.UsageHistoryProvider), _providerFilters }
        };
        Grid.SetColumn(_providerFilters, 1);
        _filterCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(15),
            Padding = new Thickness(14, 11),
            Child = new StackPanel
            {
                Spacing = 10,
                Children = { filterRow, providerRow }
            }
        };

        var overviewHeading = SectionHeading(text.UsageHistorySummaryTitle, text.UsageHistorySummarySubtitle);

        var chartControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { FilterGroup(text.UsageHistoryQuotaWindow, _windowSelector), _fullPeriodToggle }
        };
        var chartHeading = SectionHeading(text.UsageHistoryChartTitle, text.UsageHistoryChartSubtitle, chartControls);
        _chartFrame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            ClipToBounds = true,
            Child = _chart
        };
        _chartSection = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 12),
            Child = new StackPanel
            {
                Spacing = 10,
                Children = { chartHeading, _chartFrame, _noPeriodData }
            }
        };

        _message.TextWrapping = TextWrapping.Wrap;
        _message.IsVisible = false;
        _timelineCaption.FontSize = 11;
        _mutedText.Add(_timelineCaption);
        var timelineActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _timelineCaption, _timelineToggleButton }
        };
        var timelineHeading = SectionHeading(text.UsageHistoryRecords, string.Empty, timelineActions);
        _timelineCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 12),
            Child = timelineHeading
        };
        var layout = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(18),
            Children =
            {
                _headerCard,
                _filterCard,
                overviewHeading,
                _summaryGrid,
                _chartSection,
                _message,
                _timelineCard
            }
        };
        var bodyScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = layout
        };
        var windowLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("38,*"),
            Children = { _windowTitleBar, bodyScroll }
        };
        Grid.SetRow(bodyScroll, 1);
        _root = new Border
        {
            Margin = new Thickness(8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(17),
            ClipToBounds = true,
            Child = windowLayout
        };
        _root.SizeChanged += (_, _) => UpdateWindowClip();
        Content = _root;
        RegisterButton(_analyzeButton, HistoryButtonRole.Primary);
        RegisterButton(_refreshButton, HistoryButtonRole.Secondary);
        RegisterButton(_timelineToggleButton, HistoryButtonRole.AccentSoft);
        RegisterButton(_previousButton, HistoryButtonRole.Secondary);
        RegisterButton(_nextButton, HistoryButtonRole.Secondary);
        ActualThemeVariantChanged += (_, _) => ApplyVisualTheme();
        Opened += (_, _) => ApplyVisualTheme();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
            {
                UpdateWindowShape();
            }
        };
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        ApplyVisualTheme();
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

        var hadProviderChoices = _providerButtons.Count > 0;
        var selectedBefore = _selectedProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _providerFilters.Children.Clear();
        _providerButtons.Clear();
        _selectedProviders.Clear();
        var chartProviders = _entries
            .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Where(entry => IsChartProvider(entry.Provider))
            .Select(entry => entry.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ProviderOrder)
            .ThenBy(provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (chartProviders.Count > 0)
        {
            _allProvidersButton.IsVisible = true;
            _providerFilters.Children.Add(_allProvidersButton);
        }
        else
        {
            _allProvidersButton.IsVisible = false;
        }

        foreach (var provider in chartProviders)
        {
            var selected = !hadProviderChoices || selectedBefore.Contains(provider);
            if (selected)
            {
                _selectedProviders.Add(provider);
            }
            var icon = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(ProviderIconGeometry(provider)),
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = DisplayProvider(provider),
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var chipStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { icon, label }
            };
            var button = new Button
            {
                Content = chipStack,
                Tag = provider,
                Height = 30,
                Padding = new Thickness(10, 0, 12, 0),
                Margin = new Thickness(0, 0, 7, 3),
                CornerRadius = new CornerRadius(15),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Click += (_, _) =>
            {
                if (!_selectedProviders.Remove(provider))
                {
                    _selectedProviders.Add(provider);
                }
                ApplyProviderButtonTheme(button);
                ApplyAllProvidersButtonTheme();
                ApplySelection();
            };
            button.PointerEntered += (_, _) => ApplyProviderButtonTheme(button, hovered: true);
            button.PointerExited += (_, _) => ApplyProviderButtonTheme(button);
            _providerButtons.Add(button);
            ApplyProviderButtonTheme(button);
            _providerFilters.Children.Add(button);
        }
        ApplyAllProvidersButtonTheme();
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
        _chart.SetViewport(_showFullPeriod ? start : null,
            _showFullPeriod ? end : null);
        _chart.SetSeries(series);
        UpdatePeriodLabel(start, end);
        UpdateSummary(_selection);
        UpdateTimeline(start, end, providers);
        _emptyStateText.Text = _entries.Count == 0
            ? _text.UsageHistoryEmpty
            : _text.UsageHistoryNoPeriodData;
        _noPeriodData.IsVisible = _selection.Count == 0;
        _nextButton.IsEnabled = _rangeSelector.SelectedIndex != 3 && end < StartOfToday();
        _previousButton.IsEnabled = _rangeSelector.SelectedIndex != 3;
        _todayButton.IsEnabled = _rangeSelector.SelectedIndex != 3 && end < StartOfToday();
        ApplyTodayButtonTheme();
        UpdateRangePillsStyle();
        ApplyAllProvidersButtonTheme();
    }

    private void UpdateSummary(IReadOnlyList<UsageHistoryEntry> entries)
    {
        _summaryGrid.Children.Clear();
        var summary = UsageHistoryAnalytics.Summarize(entries);
        var resets = UsageHistoryAnalytics.FindResetEvents(entries).Count;

        // Card 1: Samples
        var samplesCard = CreateKpiCard(
            _text.UsageHistorySamples,
            summary.RecordCount.ToString(CultureInfo.CurrentCulture),
            _text.FormatUsageHistoryProviders(summary.ProviderCount),
            _isLightTheme ? "#0F8A5F" : "#4AD894",
            "M4 9h4v11H4zm6-5h4v16h-4zm6 8h4v8h-4z",
            statusColor: _isLightTheme ? "#0F8A5F" : "#4AD894");
        Grid.SetColumn(samplesCard, 0);
        _summaryGrid.Children.Add(samplesCard);

        // Card 2: Observed Time
        var observedCard = CreateKpiCard(
            _text.UsageHistoryObservedTime,
            FormatDuration(summary.ObservedDuration),
            _text.UsageHistoryDataCoverage,
            _isLightTheme ? "#4E6FAE" : "#83A9F2",
            "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7z",
            statusColor: _isLightTheme ? "#4E6FAE" : "#83A9F2");
        Grid.SetColumn(observedCard, 1);
        _summaryGrid.Children.Add(observedCard);

        // Card 3: 5-Hour Consumed
        var (fiveHourStatusText, fiveHourStatusColor) = FormatFiveHourBurnRateStatus(summary.FiveHour.ConsumptionPerHour);
        var fiveHourCard = CreateKpiCard(
            _text.UsageHistoryFiveHourConsumed,
            FormatPercent(summary.FiveHour.ConsumedPercent),
            fiveHourStatusText,
            _isLightTheme ? "#D46A45" : "#F08A66",
            "M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z",
            statusColor: fiveHourStatusColor);
        Grid.SetColumn(fiveHourCard, 2);
        _summaryGrid.Children.Add(fiveHourCard);

        // Card 4: Weekly Consumed
        var (weeklyStatusText, weeklyStatusColor) = FormatWeeklyBurnRateStatus(summary.Weekly.ConsumptionPerHour);
        var weeklyCard = CreateKpiCard(
            _text.UsageHistoryWeeklyConsumed,
            FormatPercent(summary.Weekly.ConsumedPercent),
            weeklyStatusText,
            _isLightTheme ? "#8A5CD7" : "#C38AF0",
            "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z",
            statusColor: weeklyStatusColor);
        Grid.SetColumn(weeklyCard, 3);
        _summaryGrid.Children.Add(weeklyCard);

        // Card 5: Resets Detected
        var resetsCard = CreateKpiCard(
            _text.UsageHistoryResetsDetected,
            resets.ToString(CultureInfo.CurrentCulture),
            _text.UsageHistoryResetMarkersHint,
            _isLightTheme ? "#D97706" : "#FFAD55",
            "M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46A7.93 7.93 0 0 0 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74A7.93 7.93 0 0 0 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z",
            statusColor: _isLightTheme ? "#D97706" : "#FFAD55");
        Grid.SetColumn(resetsCard, 4);
        _summaryGrid.Children.Add(resetsCard);
    }

    private void UpdateTimeline(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlySet<string> providers)
    {
        _timelineRecords = _entries
            .Where(entry => entry.UpdatedAt >= start && entry.UpdatedAt <= end)
            .Where(entry => providers.Contains(entry.Provider))
            .Take(MaximumVisibleRows)
            .ToArray();
        _timelineCaption.Text = _text.FormatUsageHistoryVisibleRecords(_timelineRecords.Count, _selection.Count);
        _timelineWindow?.UpdateRecords(_timelineRecords, start, end);
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

    private void ShowTimelineWindow()
    {
        var (start, end) = SelectedPeriod();
        if (_timelineWindow is not null)
        {
            _timelineWindow.UpdateRecords(_timelineRecords, start, end);
            _timelineWindow.Activate();
            return;
        }

        var dialog = new UsageTimelineWindow(_timelineRecords, start, end, _text)
        {
            ShowInTaskbar = _settings.ShowTaskbarIcon,
            Topmost = _settings.AlwaysOnTop
        };
        _timelineWindow = dialog;
        dialog.Closed += (_, _) =>
        {
            if (ReferenceEquals(_timelineWindow, dialog))
            {
                _timelineWindow = null;
            }
        };
        dialog.Show(this);
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

    private IReadOnlySet<string> SelectedProviders() =>
        _selectedProviders.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static DateTimeOffset StartOfToday()
    {
        var today = DateTime.Today;
        return new DateTimeOffset(today, TimeZoneInfo.Local.GetUtcOffset(today));
    }

    private Control FilterGroup(string label, Control control) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { FilterLabel(label), control }
    };

    private TextBlock FilterLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.55,
            VerticalAlignment = VerticalAlignment.Center
        };
        _mutedText.Add(label);
        return label;
    }

    private static Button NavigationButton(Control content, string tooltip)
    {
        var button = new Button
        {
            Content = content,
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Control CreateChevronIcon(bool pointsRight) => new PathIcon
    {
        Width = 13,
        Height = 13,
        Data = Geometry.Parse(pointsRight
            ? "M8.59 16.59L10 18L16 12L10 6L8.59 7.41L13.17 12L8.59 16.59Z"
            : "M15.41 7.41L14 6L8 12L14 18L15.41 16.59L10.83 12L15.41 7.41Z")
    };

    private static Button WindowControlButton(Control content, string tooltip)
    {
        var button = new Button
        {
            Content = content,
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Control CreateWindowIcon(string geometry) => new PathIcon
    {
        Width = 12,
        Height = 12,
        Data = Geometry.Parse(geometry)
    };

    private void RegisterWindowControlHover(Button button, bool closeButton)
    {
        button.PointerEntered += (_, _) => ApplyWindowControlTheme(button, closeButton, hovered: true);
        button.PointerExited += (_, _) => ApplyWindowControlTheme(button, closeButton, hovered: false);
    }

    private void ApplyWindowControlTheme(Button button, bool closeButton, bool hovered)
    {
        if (closeButton && hovered)
        {
            button.Background = Brush(_isLightTheme ? "#FFFFE9E7" : "#FF472725");
            button.BorderBrush = Brush(_isLightTheme ? "#FFFFC8C3" : "#FF74413D");
            button.Foreground = Brush(_palette.Error);
            return;
        }

        button.Background = Brush(hovered ? _palette.Soft : "#00000000");
        button.BorderBrush = Brush(hovered ? _palette.BorderStrong : "#00000000");
        button.Foreground = Brush(_palette.Secondary);
    }

    private void HandleTitleBarPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (eventArgs.ClickCount == 2)
        {
            ToggleMaximized();
            eventArgs.Handled = true;
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            BeginMoveDrag(eventArgs);
        }
    }

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void UpdateWindowShape()
    {
        var maximized = WindowState == WindowState.Maximized;
        _root.Margin = maximized ? new Thickness(0) : new Thickness(8);
        _root.CornerRadius = new CornerRadius(maximized ? 0 : 17);
        _root.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        _windowTitleBar.CornerRadius = maximized
            ? new CornerRadius(0)
            : new CornerRadius(16, 16, 0, 0);
        UpdateWindowClip();
    }

    private void UpdateWindowClip()
    {
        var radius = WindowState == WindowState.Maximized ? 0 : 17;
        _root.Clip = new RectangleGeometry(
            new Rect(0, 0, _root.Bounds.Width, _root.Bounds.Height),
            radius,
            radius);
    }

    private static Button ActionButton(string content, double minWidth) => new()
    {
        Content = content,
        MinWidth = minWidth,
        Height = 38,
        Padding = new Thickness(13, 0),
        CornerRadius = new CornerRadius(10),
        BorderThickness = new Thickness(1),
        FontWeight = FontWeight.SemiBold,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private Control SectionHeading(string title, string subtitle, Control? trailing = null)
    {
        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleText = new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _mutedText.Add(subtitleText);
            titleStack.Children.Add(subtitleText);
        }
        if (trailing is null)
        {
            return titleStack;
        }
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 18
        };
        Grid.SetColumn(trailing, 1);
        grid.Children.Add(titleStack);
        grid.Children.Add(trailing);
        return grid;
    }

    private Border CreateKpiCard(
        string label,
        string value,
        string detail,
        string accent,
        string iconGeometry,
        string statusColor)
    {
        var card = new Border
        {
            MinHeight = 104,
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12)
        };

        if (_isLightTheme)
        {
            card.BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 8,
                OffsetY = 2,
                Color = Color.Parse("#0D000000")
            });
        }

        // Header Row: Left = Label, Right = Icon Badge
        var labelText = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontSize = 9.5,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.4,
            Foreground = Brush(_palette.Secondary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconPath = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(iconGeometry),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Fill = Brush(accent),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBadge = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(8),
            Background = Tint(accent, _isLightTheme ? (byte)32 : (byte)45),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)90 : (byte)120),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = iconPath
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { labelText, iconBadge }
        };
        Grid.SetColumn(iconBadge, 1);

        // Value Row
        var valueText = new TextBlock
        {
            Text = value,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(_palette.Primary),
            Margin = new Thickness(0, 4, 0, 5),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        // Status / Detail Pill Badge
        var statusDot = new Border
        {
            Width = 6,
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = Brush(statusColor),
            VerticalAlignment = VerticalAlignment.Center
        };

        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 10,
            FontWeight = FontWeight.Medium,
            Foreground = Brush(statusColor),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var statusPill = new Border
        {
            Height = 22,
            Padding = new Thickness(7, 0),
            CornerRadius = new CornerRadius(6),
            Background = Tint(statusColor, _isLightTheme ? (byte)22 : (byte)35),
            BorderBrush = Tint(statusColor, _isLightTheme ? (byte)60 : (byte)80),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { statusDot, detailText }
            }
        };

        card.Child = new StackPanel
        {
            Spacing = 2,
            Children = { headerGrid, valueText, statusPill }
        };

        // Hover micro-interaction
        card.PointerEntered += (_, _) =>
        {
            card.Background = Brush(_isLightTheme ? "#FFFFFFFF" : _palette.Soft);
            card.BorderBrush = Tint(accent, _isLightTheme ? (byte)140 : (byte)180);
            if (_isLightTheme)
            {
                card.BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 14,
                    OffsetY = 4,
                    Color = Color.Parse("#18000000")
                });
            }
        };

        card.PointerExited += (_, _) =>
        {
            card.Background = Brush(_palette.Surface);
            card.BorderBrush = Brush(_palette.Border);
            if (_isLightTheme)
            {
                card.BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 8,
                    OffsetY = 2,
                    Color = Color.Parse("#0D000000")
                });
            }
            else
            {
                card.BoxShadow = default;
            }
        };

        return card;
    }

    private (string Text, string Color) FormatFiveHourBurnRateStatus(double rate)
    {
        if (rate <= 0)
        {
            return ($"0%/h • {_text.UsageHistoryBurnRateIdle}", _isLightTheme ? "#64748B" : "#94A3B8");
        }
        if (rate < 2.0)
        {
            return ($"{rate:0.#}%/h • {_text.UsageHistoryBurnRateLight}", _isLightTheme ? "#0F8A5F" : "#4AD894");
        }
        if (rate < 8.0)
        {
            return ($"{rate:0.#}%/h • {_text.UsageHistoryBurnRateModerate}", _isLightTheme ? "#D97706" : "#FBBF24");
        }
        return ($"{rate:0.#}%/h • {_text.UsageHistoryBurnRateHigh}", _isLightTheme ? "#DC2626" : "#F87171");
    }

    private (string Text, string Color) FormatWeeklyBurnRateStatus(double rate)
    {
        if (rate <= 0)
        {
            return ($"0%/h • {_text.UsageHistoryBurnRateIdle}", _isLightTheme ? "#64748B" : "#94A3B8");
        }
        if (rate < 0.6)
        {
            return ($"{rate:0.##}%/h • {_text.UsageHistoryBurnRateLight}", _isLightTheme ? "#0F8A5F" : "#4AD894");
        }
        if (rate < 1.8)
        {
            return ($"{rate:0.##}%/h • {_text.UsageHistoryBurnRateModerate}", _isLightTheme ? "#D97706" : "#FBBF24");
        }
        return ($"{rate:0.##}%/h • {_text.UsageHistoryBurnRateHigh}", _isLightTheme ? "#DC2626" : "#F87171");
    }

    private void ApplyVisualTheme()
    {
        _isLightTheme = ActualThemeVariant == ThemeVariant.Light;
        _palette = _isLightTheme ? HistoryPalette.Light : HistoryPalette.Dark;
        // The window surface itself must stay transparent. Painting the theme
        // color here fills the pixels outside the rounded root and makes all
        // four corners look square on Linux compositors.
        Background = Brushes.Transparent;
        _root.Background = Brush(_palette.Root);
        _root.BorderBrush = Brush(_palette.BorderStrong);
        _windowTitleBar.Background = Brush(_palette.Surface);
        _windowTitleBar.BorderBrush = Brush(_palette.Border);
        _windowTitleText.Foreground = Brush(_palette.Primary);
        ApplyWindowControlTheme(_windowMinimizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_windowMaximizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_windowCloseButton, closeButton: true, hovered: false);

        _headerCard.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(_palette.HeaderStart), 0),
                new GradientStop(Color.Parse(_palette.HeaderEnd), 1)
            }
        };
        _headerCard.BorderBrush = Brush(_palette.Border);
        _headerCard.BoxShadow = _isLightTheme
            ? new BoxShadows(new BoxShadow
            {
                Blur = 18,
                OffsetY = 4,
                Color = Color.Parse("#12000000")
            })
            : default;
        _titleText.Foreground = Brush(_palette.Primary);
        _titleMark.Background = Brush(_palette.AccentSoft);
        _titleMark.BorderBrush = Tint(_palette.Accent, _isLightTheme ? (byte)80 : (byte)120);
        _titleMark.BorderThickness = new Thickness(1);
        _titleClockRing.Stroke = Brush(_palette.Accent);
        _titleClockHands.Stroke = Brush(_palette.Accent);

        ApplySurfaceTheme(_filterCard, _palette.Surface);
        _filterCard.BoxShadow = _isLightTheme
            ? new BoxShadows(new BoxShadow
            {
                Blur = 10,
                OffsetY = 2,
                Color = Color.Parse("#0D000000")
            })
            : default;

        ApplySurfaceTheme(_chartSection, _palette.Surface);
        _chartSection.BoxShadow = _isLightTheme
            ? new BoxShadows(new BoxShadow
            {
                Blur = 12,
                OffsetY = 3,
                Color = Color.Parse("#0D000000")
            })
            : default;

        ApplySurfaceTheme(_timelineCard, _palette.Surface);
        _timelineCard.BoxShadow = _isLightTheme
            ? new BoxShadows(new BoxShadow
            {
                Blur = 10,
                OffsetY = 2,
                Color = Color.Parse("#0D000000")
            })
            : default;

        ApplySurfaceTheme(_periodNavigationSurface, _palette.Soft);
        _periodCalendarBadge.Background = Brush(_palette.AccentSoft);
        _periodCalendarBadge.BorderBrush = Brush(_palette.AccentBorder);
        _periodCalendarPath.Fill = Brush(_palette.Accent);

        _rangeSegmentedSurface.Background = Brush(_palette.Soft);
        _rangeSegmentedSurface.BorderBrush = Brush(_palette.Border);
        if (_isLightTheme)
        {
            _rangeSegmentedSurface.BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 4,
                OffsetY = 1,
                Color = Color.Parse("#08000000")
            });
        }
        else
        {
            _rangeSegmentedSurface.BoxShadow = default;
        }

        _chartFrame.Background = Brush(_palette.Surface);
        _chartFrame.BorderBrush = Brush(_palette.Border);
        _chart.SetTheme(_isLightTheme);

        foreach (var text in _mutedText)
        {
            text.Foreground = Brush(_palette.Secondary);
        }
        _periodLabel.Foreground = Brush(_palette.Primary);
        _message.Foreground = Brush(_palette.Error);
        _noPeriodData.Background = Brush(_palette.WarningSoft);
        _noPeriodData.BorderBrush = Brush(_palette.WarningBorder);
        if (_noPeriodData.Child is TextBlock warningText)
        {
            warningText.Foreground = Brush(_palette.WarningText);
        }

        foreach (var (button, role) in _buttonRoles)
        {
            ApplyButtonRole(button, role, hovered: false);
        }

        UpdateRangePillsStyle();
        ApplyTodayButtonTheme();
        ApplyAllProvidersButtonTheme();
        ApplySelectorTheme(_windowSelector);
        ApplyFullPeriodToggleTheme();
        foreach (var provider in _providerButtons)
        {
            ApplyProviderButtonTheme(provider);
        }
        UpdateSummary(_selection);
    }

    private void ApplySurfaceTheme(Border surface, string background)
    {
        surface.Background = Brush(background);
        surface.BorderBrush = Brush(_palette.Border);
    }

    private void RegisterButton(Button button, HistoryButtonRole role)
    {
        _buttonRoles[button] = role;
        button.PointerEntered += (_, _) => ApplyButtonRole(button, role, hovered: true);
        button.PointerExited += (_, _) => ApplyButtonRole(button, role, hovered: false);
    }

    private void ApplyButtonRole(Button button, HistoryButtonRole role, bool hovered)
    {
        var colors = (role, hovered) switch
        {
            (HistoryButtonRole.Primary, false) =>
                (_palette.AccentFill, "#FFFFFFFF", _palette.AccentFill),
            (HistoryButtonRole.Primary, true) =>
                (_palette.AccentFillHover, "#FFFFFFFF", _palette.AccentFillHover),
            (HistoryButtonRole.Secondary, false) =>
                (_palette.Surface, _palette.Accent, _palette.BorderStrong),
            (HistoryButtonRole.Secondary, true) =>
                (_palette.AccentSoft, _palette.Accent, _palette.AccentBorder),
            (HistoryButtonRole.AccentSoft, false) =>
                (_palette.AccentSoft, _palette.Accent, _palette.AccentBorder),
            (HistoryButtonRole.AccentSoft, true) =>
                (_palette.HeaderEnd, _palette.Accent, _palette.Accent),
            (HistoryButtonRole.Neutral, true) =>
                (_palette.Surface, _palette.Primary, _palette.BorderStrong),
            _ => (_palette.Soft, _palette.Secondary, _palette.Border)
        };
        button.Background = Brush(colors.Item1);
        button.Foreground = Brush(colors.Item2);
        button.BorderBrush = Brush(colors.Item3);
    }

    private void ApplySelectorTheme(ComboBox selector)
    {
        selector.Background = Brush(_palette.Soft);
        selector.Foreground = Brush(_palette.Primary);
        selector.BorderBrush = Brush(_palette.BorderStrong);
        selector.BorderThickness = new Thickness(1);
    }

    private void UpdateRangePillsStyle()
    {
        foreach (var (index, pill) in _rangeSegmentedButtons)
        {
            ApplyRangePillTheme(pill, index, hovered: false);
        }
    }

    private void ApplyRangePillTheme(Button pill, int index, bool hovered)
    {
        var isSelected = _rangeSelector.SelectedIndex == index;
        if (isSelected)
        {
            pill.Background = Brush(_palette.Surface);
            pill.Foreground = Brush(_palette.Accent);
            pill.BorderBrush = Brush(_palette.AccentBorder);
            pill.FontWeight = FontWeight.Bold;
        }
        else
        {
            pill.Background = Brush(hovered ? _palette.AccentSoft : "#00000000");
            pill.Foreground = Brush(hovered ? _palette.Accent : _palette.Secondary);
            pill.BorderBrush = Brush(hovered ? _palette.AccentBorder : "#00000000");
            pill.FontWeight = FontWeight.Medium;
        }
    }

    private void ApplyTodayButtonTheme(bool hovered = false)
    {
        if (!_todayButton.IsEnabled)
        {
            _todayButton.Background = Brush(_palette.Soft);
            _todayButton.BorderBrush = Brush(_palette.Border);
            _todayButton.Foreground = Brush(_palette.Secondary);
            _todayButton.Opacity = 0.42;
        }
        else
        {
            _todayButton.Opacity = 1.0;
            _todayButton.Background = Brush(hovered ? _palette.AccentSoft : _palette.Surface);
            _todayButton.BorderBrush = Brush(hovered ? _palette.AccentBorder : _palette.BorderStrong);
            _todayButton.Foreground = Brush(_palette.Accent);
        }
        _todayIcon.Fill = _todayButton.Foreground;
        _todayLabel.Foreground = _todayButton.Foreground;
    }

    private void ApplyAllProvidersButtonTheme(bool hovered = false)
    {
        var allChartProviders = _entries
            .Where(e => string.Equals(e.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Where(e => IsChartProvider(e.Provider))
            .Select(e => e.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var isAllSelected = allChartProviders.Count > 0 && _selectedProviders.Count == allChartProviders.Count;
        var accent = _palette.Accent;
        _allProvidersButton.Background = isAllSelected
            ? Tint(accent, _isLightTheme
                ? hovered ? (byte)42 : (byte)28
                : hovered ? (byte)58 : (byte)42)
            : Brush(hovered ? _palette.AccentSoft : _palette.Soft);
        _allProvidersButton.Foreground = Brush(isAllSelected || hovered ? accent : _palette.Secondary);
        _allProvidersButton.BorderBrush = isAllSelected
            ? Tint(accent, _isLightTheme ? (byte)125 : (byte)155)
            : Brush(hovered ? _palette.AccentBorder : _palette.Border);
        _allProvidersIcon.Fill = _allProvidersButton.Foreground;
        _allProvidersLabel.Foreground = _allProvidersButton.Foreground;
        _allProvidersLabel.FontWeight = isAllSelected ? FontWeight.Bold : FontWeight.Medium;
    }

    private void ApplyFullPeriodToggleTheme(bool hovered = false)
    {
        _fullPeriodLabel.Text = _showFullPeriod
            ? _text.UsageHistoryShowFullPeriod
            : _text.UsageHistoryDataOnly;
        _fullPeriodLabel.Foreground = Brush(_showFullPeriod ? _palette.Accent : _palette.Secondary);
        _fullPeriodToggle.Background = Brush(hovered ? _palette.AccentSoft : _palette.Soft);
        _fullPeriodToggle.BorderBrush = Brush(hovered || _showFullPeriod
            ? _palette.AccentBorder
            : _palette.Border);
        _fullPeriodTrack.Background = Brush(_showFullPeriod
            ? _palette.AccentFill
            : _palette.BorderStrong);
        _fullPeriodThumb.Background = Brush(_showFullPeriod ? "#FFFFFFFF" : _palette.Secondary);
        _fullPeriodThumb.HorizontalAlignment = _showFullPeriod
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
    }

    private void ApplyProviderButtonTheme(Button button, bool hovered = false)
    {
        var provider = button.Tag as string ?? string.Empty;
        var selected = _selectedProviders.Contains(provider);
        var accent = ProviderAccent(provider);
        button.Background = selected
            ? Tint(accent, _isLightTheme
                ? hovered ? (byte)42 : (byte)28
                : hovered ? (byte)58 : (byte)42)
            : Brush(hovered ? _palette.AccentSoft : _palette.Soft);
        button.Foreground = Brush(selected || hovered ? accent : _palette.Secondary);
        button.BorderBrush = selected
            ? Tint(accent, _isLightTheme ? (byte)125 : (byte)155)
            : Brush(hovered ? _palette.AccentBorder : _palette.Border);

        if (button.Content is StackPanel sp)
        {
            foreach (var child in sp.Children)
            {
                if (child is Avalonia.Controls.Shapes.Path path)
                {
                    path.Fill = button.Foreground;
                }
                else if (child is TextBlock tb)
                {
                    tb.Foreground = button.Foreground;
                    tb.FontWeight = selected ? FontWeight.Bold : FontWeight.Medium;
                }
            }
        }
    }

    private static string ProviderIconGeometry(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "M12 2L13.8 8.7L20.5 7L16.2 12L21.8 15.2L15.3 16.5L16.8 23L12 18.5L7.2 23L8.7 16.5L2.2 15.2L7.8 12L3.5 7L10.2 8.7L12 2Z",
        "codex" => "M12 2C6.48 2 2 6.48 2 12S6.48 22 12 22 22 17.52 22 12 17.52 2 12 2ZM12 4C16.42 4 20 7.58 20 12C20 13.91 19.33 15.66 18.21 17.03L6.97 5.79C8.34 4.67 10.09 4 12 4ZM4 12C4 10.09 4.67 8.34 5.79 6.97L17.03 18.21C15.66 19.33 13.91 20 12 20C7.58 20 4 16.42 4 12Z",
        "antigravity-gemini" => "M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z",
        "antigravity-claudeandchatgpt" => "M12 2L14.5 8.5L21 9.5L16 14L17.5 20.5L12 17L6.5 20.5L8 14L3 9.5L9.5 8.5L12 2Z",
        _ => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8z"
    };

    private string ProviderAccent(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => _isLightTheme ? "#C85F3D" : "#F08A66",
        "codex" => _isLightTheme ? "#0F8A68" : "#4AD894",
        "antigravity-gemini" => _isLightTheme ? "#3B82F6" : "#8794FF",
        "antigravity-claudeandchatgpt" => _isLightTheme ? "#8955C5" : "#C38AF0",
        _ => _palette.Secondary
    };

    private static SolidColorBrush Tint(string color, byte alpha)
    {
        var parsed = Color.Parse(color);
        return new SolidColorBrush(Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
    }

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
        else if (eventArgs.Key == Key.Home && (eventArgs.KeyModifiers == KeyModifiers.Alt || eventArgs.KeyModifiers == KeyModifiers.None))
        {
            if (_todayButton.IsEnabled)
            {
                _anchorDate = DateTimeOffset.Now;
                ApplySelection();
                eventArgs.Handled = true;
            }
        }
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

    private static string FormatDuration(TimeSpan duration) => duration.TotalDays >= 1
        ? $"{(int)duration.TotalDays}d {duration.Hours}h"
        : duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{Math.Max(0, duration.Minutes)}m";

    private static string FormatPercent(double value) =>
        $"{value:#,##0.#}%";

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private enum HistoryButtonRole
    {
        Primary,
        Secondary,
        Neutral,
        AccentSoft
    }

    private sealed record HistoryPalette(
        string Root,
        string Surface,
        string Soft,
        string HeaderStart,
        string HeaderEnd,
        string Border,
        string BorderStrong,
        string Primary,
        string Secondary,
        string Accent,
        string AccentFill,
        string AccentFillHover,
        string AccentSoft,
        string AccentBorder,
        string Error,
        string WarningSoft,
        string WarningBorder,
        string WarningText)
    {
        public static HistoryPalette Light { get; } = new(
            "#FFF2F5F3",
            "#FFFFFFFF",
            "#FFF7F9F7",
            "#FFFFFFFF",
            "#FFEDF8F3",
            "#FFDCE4DF",
            "#FFCAD6CE",
            "#FF1F2822",
            "#FF67736B",
            "#FF0F8A68",
            "#FF0F7B5C",
            "#FF0D6E53",
            "#FFE5F5EE",
            "#FFA8D8C5",
            "#FFB13A32",
            "#FFFFF5DF",
            "#FFF0D08C",
            "#FF7A5314");

        public static HistoryPalette Dark { get; } = new(
            "#FF151816",
            "#FF202421",
            "#FF292E2A",
            "#FF252A26",
            "#FF1C3028",
            "#FF394039",
            "#FF4A534C",
            "#FFF0F4F1",
            "#FFA9B3AB",
            "#FF4AD894",
            "#FF147A59",
            "#FF178C66",
            "#FF1D392E",
            "#FF376D58",
            "#FFFF8A80",
            "#FF3A3020",
            "#FF75582A",
            "#FFFFD18A");
    }
}
