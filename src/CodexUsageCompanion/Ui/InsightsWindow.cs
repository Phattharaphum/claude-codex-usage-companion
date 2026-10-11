using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
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

/// <summary>The pages of the Usage insights window.</summary>
public enum InsightsTab
{
    Overview,
    Analysis,
    Efficiency,
    Records
}

/// <summary>
/// Usage insights: one window for everything the optional usage-history CSV can
/// tell you. The period and provider filters are shared by every tab, so
/// switching between the overview, analysis, reset efficiency and the raw
/// records never loses your place.
/// </summary>
public sealed class InsightsWindow : Window
{
    private const int InlineActivityRows = 7;    private readonly CompanionSettings _settings;
    private readonly UiText _text;
    private readonly UsageHistoryReader _reader;
    private readonly Grid _summaryGrid = new() { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 10 };
    private readonly WrapPanel _providerFilters = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _periodLabel = new();
    private readonly TextBlock _timelineCaption = new();
    private readonly Button _viewAllRecordsButton;
    private readonly Dictionary<InsightsTab, Button> _tabButtons = [];
    private readonly Border _tabSurface;
    private readonly Border _coverageChip;
    private readonly TextBlock _coverageChipText = new();
    private readonly ScrollViewer _overviewScroll;
    private readonly AnalysisView _analysisView;
    private readonly EfficiencyView _efficiencyView;
    private readonly RecordsView _recordsView;
    private readonly Dictionary<InsightsTab, int> _tabVersions = [];
    private InsightsTab _activeTab = InsightsTab.Overview;
    private readonly TextBlock _message = new();
    private readonly ProgressBar _loadingBar = new()
    {
        IsIndeterminate = true,
        Height = 3,
        MinHeight = 3,
        VerticalAlignment = VerticalAlignment.Top,
        IsVisible = false
    };
    private int _loadVersion;
    private bool _closed;
    private readonly TextBlock _emptyStateText;
    private readonly Border _noPeriodData;
    private readonly ComboBox _rangeSelector;
    private readonly ComboBox _windowSelector;
    private readonly Border _rangeSegmentedSurface;
    private readonly Dictionary<int, Button> _rangeSegmentedButtons = [];
    private readonly Border _quotaSegmentedSurface;
    private readonly Dictionary<int, Button> _quotaSegmentedButtons = [];
    private readonly Border _inlineTableHeaderSurface;
    private readonly StackPanel _inlineActivityRows = new() { Spacing = 5 };
    private readonly Border _inlineEmptyState;
    private readonly TextBlock _inlineEmptyStateText;
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
    private readonly UsageHistoryChart _chart;
    private readonly Border _root;
    private readonly Border _windowTitleBar;
    private readonly TextBlock _windowTitleText;
    private readonly Button _windowMinimizeButton;
    private readonly Button _windowMaximizeButton;
    private readonly Button _windowCloseButton;
    private readonly Border _filterCard;
    private readonly Border _chartSection;
    private readonly Border _chartFrame;
    private readonly Border _timelineCard;
    private readonly Border _periodNavigationSurface;
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
    private int _selectionVersion;

    public InsightsWindow(
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
            A11y.Tip(pill, tooltip);
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
        A11y.Tip(_todayButton, text.UsageHistoryToday);
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
        A11y.Tip(_allProvidersButton, text.UsageHistoryAllProviders);
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
            IsVisible = false
        };

        _quotaSegmentedSurface = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3)
        };
        var quotaPillsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3
        };
        _quotaSegmentedSurface.Child = quotaPillsPanel;

        var quotaOptions = new (int Index, string Label, string Tooltip)[]
        {
            (0, text.UsageHistoryWeeklyWindow, text.UsageHistoryWeeklyWindow),
            (1, text.UsageHistoryFiveHourWindow, text.UsageHistoryFiveHourWindow)
        };

        foreach (var (index, label, tooltip) in quotaOptions)
        {
            var pill = new Button
            {
                Content = label,
                Height = 28,
                MinWidth = 66,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            A11y.Tip(pill, tooltip);
            var pillIndex = index;
            pill.Click += (_, _) =>
            {
                if (_windowSelector.SelectedIndex != pillIndex)
                {
                    _windowSelector.SelectedIndex = pillIndex;
                }
            };
            pill.PointerEntered += (_, _) => ApplyQuotaPillTheme(pill, pillIndex, hovered: true);
            pill.PointerExited += (_, _) => ApplyQuotaPillTheme(pill, pillIndex, hovered: false);
            _quotaSegmentedButtons[pillIndex] = pill;
            quotaPillsPanel.Children.Add(pill);
        }

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
        _viewAllRecordsButton = new Button
        {
            Content = text.InsightsViewAllRecords + "  →",
            MinWidth = 142,
            Height = 34,
            Padding = new Thickness(13, 0),
            CornerRadius = new CornerRadius(10),
            FontWeight = FontWeight.SemiBold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _viewAllRecordsButton.Click += (_, _) => SelectTab(InsightsTab.Records);
        _chart = new UsageHistoryChart
        {
            RemainingLabel = text.UsageHistoryRemaining,
            ResetLabel = text.UsageHistoryReset,
            NoDataText = text.UsageHistoryNoChartData,
            Labels = text,
            AccessibleLabel = L(
                "Remaining quota chart. Use the arrow keys to read each sample: left and right move in time, up and down switch provider.",
                "剩餘額度趨勢圖。使用方向鍵逐點讀取：左右移動時間，上下切換來源。",
                "剩余额度趋势图。使用方向键逐点读取：左右移动时间，上下切换来源。"),
            GapLabel = L("No data", "無資料", "无数据"),
            GapDurationFormatter = FormatDuration,
            ProviderLabelFormatter = ChartProviderLabel,
            Height = 300,
            MinHeight = 280
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
        _windowSelector.SelectionChanged += (_, _) =>
        {
            UpdateQuotaPillsStyle();
            ApplySelection();
        };
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

        Title = text.InsightsTitle;
        Width = 1160;
        Height = 860;
        MinWidth = 880;
        MinHeight = 680;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _windowTitleText = new TextBlock
        {
            Text = text.InsightsTitle,
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
        _refreshButton.Height = 34;
        _refreshButton.Click += (_, _) => Reload();

        // One row for navigation: the tabs, how much of the period was really
        // recorded, and refresh. Nothing here repeats the title bar.
        _tabSurface = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(3)
        };
        var tabPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        foreach (var (tab, label, shortcut) in new[]
                 {
                     (InsightsTab.Overview, text.InsightsTabOverview, "Ctrl+1"),
                     (InsightsTab.Analysis, text.InsightsTabAnalysis, "Ctrl+2"),
                     (InsightsTab.Efficiency, text.InsightsTabEfficiency, "Ctrl+3"),
                     (InsightsTab.Records, text.InsightsTabRecords, "Ctrl+4")
                 })
        {
            var tabButton = new Button
            {
                Content = label,
                Height = 30,
                MinWidth = 92,
                Padding = new Thickness(14, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            A11y.Tip(tabButton, $"{label} ({shortcut})");
            var captured = tab;
            // Announced as "tab 2 of 4", not as four unrelated buttons.
            AutomationProperties.SetControlTypeOverride(tabButton, AutomationControlType.TabItem);
            AutomationProperties.SetPositionInSet(tabButton, (int)tab + 1);
            AutomationProperties.SetSizeOfSet(tabButton, 4);
            tabButton.Click += (_, _) => SelectTab(captured);
            tabButton.PointerEntered += (_, _) => ApplyTabTheme(captured, hovered: true);
            tabButton.PointerExited += (_, _) => ApplyTabTheme(captured, hovered: false);
            _tabButtons[tab] = tabButton;
            tabPanel.Children.Add(tabButton);
        }

        _tabSurface.Child = tabPanel;
        _subtitle.FontSize = 11.5;
        _subtitle.VerticalAlignment = VerticalAlignment.Center;
        _subtitle.HorizontalAlignment = HorizontalAlignment.Right;
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _mutedText.Add(_subtitle);
        _coverageChipText.FontSize = 11;
        _coverageChipText.FontWeight = FontWeight.SemiBold;
        _coverageChip = new Border
        {
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _coverageChipText
        };
        var headerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(18, 12, 18, 0),
            Children = { _tabSurface, _subtitle, _coverageChip, _refreshButton }
        };
        Grid.SetColumn(_subtitle, 1);
        Grid.SetColumn(_coverageChip, 2);
        Grid.SetColumn(_refreshButton, 3);

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
            Padding = new Thickness(14, 9),
            Child = new StackPanel
            {
                Spacing = 7,
                Children = { filterRow, providerRow }
            }
        };

        var overviewHeading = SectionHeading(text.UsageHistorySummaryTitle, text.UsageHistorySummarySubtitle);

        var chartControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { FilterGroup(text.UsageHistoryQuotaWindow, _quotaSegmentedSurface), _fullPeriodToggle }
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
            Children = { _timelineCaption, _viewAllRecordsButton }
        };
        var timelineHeading = SectionHeading(text.UsageHistoryRecentActivity, string.Empty, timelineActions);

        _inlineTableHeaderSurface = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 9),
            Margin = new Thickness(0, 6, 0, 2),
            Child = CreateInlineTableHeader()
        };

        _inlineEmptyStateText = new TextBlock
        {
            Text = text.UsageHistoryNoPeriodData,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _inlineEmptyState = new Border
        {
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 24),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _inlineEmptyStateText,
            IsVisible = false
        };

        var timelineContent = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                timelineHeading,
                _inlineTableHeaderSurface,
                _inlineActivityRows,
                _inlineEmptyState
            }
        };

        _timelineCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16, 14),
            Child = timelineContent
        };
        var overviewContent = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 18),
            Children = { overviewHeading, _summaryGrid, _chartSection, _timelineCard }
        };
        _overviewScroll = new ScrollViewer
        {
            Focusable = true,
            IsTabStop = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = overviewContent
        };
        _analysisView = new AnalysisView(text) { IsVisible = false };
        _efficiencyView = new EfficiencyView(text) { IsVisible = false };
        _recordsView = new RecordsView(text) { IsVisible = false };
        var pageHost = new Grid
        {
            Margin = new Thickness(18, 0, 18, 16),
            Children = { _overviewScroll, _analysisView, _efficiencyView, _recordsView }
        };
        _message.Margin = new Thickness(18, 0, 18, 8);
        _filterCard.Margin = new Thickness(18, 10, 18, 10);
        var windowLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("38,Auto,Auto,Auto,*"),
            Children = { _windowTitleBar, headerRow, _filterCard, _message, pageHost, _loadingBar }
        };
        Grid.SetRow(headerRow, 1);
        Grid.SetRow(_filterCard, 2);
        Grid.SetRow(_message, 3);
        Grid.SetRow(pageHost, 4);
        Grid.SetRow(_loadingBar, 1);
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
        RegisterButton(_refreshButton, HistoryButtonRole.Secondary);
        RegisterButton(_viewAllRecordsButton, HistoryButtonRole.AccentSoft);
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
        Closed += (_, _) => _closed = true;
        ApplyVisualTheme();
        Reload();
    }

    /// <summary>Re-reads the log in the background; the window stays responsive meanwhile.</summary>
    public void Reload() => _ = ReloadAsync();

    public async Task ReloadAsync()
    {
        var version = ++_loadVersion;
        var path = _settings.UsageLogFilePath;
        var format = _settings.UsageLogFormat;
        _loadingBar.IsVisible = true;
        if (_entries.Count == 0)
        {
            _subtitle.Text = L("Loading history…", "正在載入記錄…", "正在加载记录…");
        }

        UsageHistoryReadResult result;
        try
        {
            result = await Task.Run(() => _reader.Read(path, format));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = new UsageHistoryReadResult([], $"Unable to read usage history: {exception.Message}");
        }

        // A newer reload superseded this one, or the window was closed.
        if (version != _loadVersion || _closed)
        {
            return;
        }

        _loadingBar.IsVisible = false;
        ApplyReadResult(result);
    }

    private void ApplyReadResult(UsageHistoryReadResult result)
    {
        _message.IsVisible = !string.IsNullOrWhiteSpace(result.Error);
        _message.Text = _text.LocalizeError(result.Error) ?? string.Empty;
        _entries = result.Entries;
        _subtitle.Text = _entries.Count == 0
            ? _text.UsageHistoryEmptySubtitle
            : _text.FormatUsageHistoryCount(_entries.Count, int.MaxValue);

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
        UpdateQuotaPillsStyle();
        ApplyAllProvidersButtonTheme();
        InvalidateTabs();
    }

    private void UpdateSummary(IReadOnlyList<UsageHistoryEntry> entries)
    {
        _summaryGrid.Children.Clear();
        var summary = UsageHistoryAnalytics.Summarize(entries);
        var providerCount = Math.Max(1, summary.ProviderCount);

        // Card 1: Samples
        var samplesCard = CreateKpiCard(
            _text.UsageHistorySamples,
            summary.RecordCount.ToString("N0", CultureInfo.CurrentCulture),
            _text.FormatUsageHistoryProviders(summary.ProviderCount),
            StatusStyle.Good(_isLightTheme),
            "M4 9h4v11H4zm6-5h4v16h-4zm6 8h4v8h-4z",
            statusColor: StatusStyle.Good(_isLightTheme));
        Grid.SetColumn(samplesCard, 0);
        _summaryGrid.Children.Add(samplesCard);

        // Card 2: Observed time, with how much of it was really recorded. The
        // app is not always running, so quota moves unobserved; a number that
        // hides that would look more certain than it is.
        var quality = summary.Quality;
        var (coverageText, coverageColor) = FormatCoverageStatus(quality);
        UpdateCoverageChip(summary, coverageText, coverageColor);
        var observedCard = CreateKpiCard(
            _text.UsageHistoryObservedTime,
            FormatDuration(summary.ObservedDuration),
            coverageText,
            StatusStyle.Info(_isLightTheme),
            "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7z",
            statusColor: coverageColor,
            tooltip: quality.GapCount == 0
                ? L("Samples cover the whole observed span.", "樣本涵蓋整個觀察期間。", "样本覆盖整个观察期间。")
                : L(
                    $"Nothing was recorded for {FormatDuration(summary.UnobservedPerProvider)} of this span (the app was not running). Quota may have changed during those gaps.",
                    $"此期間有 {FormatDuration(summary.UnobservedPerProvider)} 沒有記錄（程式未執行），這段時間內額度可能已變動。",
                    $"此期间有 {FormatDuration(summary.UnobservedPerProvider)} 没有记录（程序未运行），这段时间内额度可能已变动。"));
        Grid.SetColumn(observedCard, 1);
        _summaryGrid.Children.Add(observedCard);

        // Card 3: 5-hour quota used, in whole quotas rather than a raw sum of
        // percentage points that is meaningless above 100%.
        var (fiveHourStatusText, fiveHourStatusColor) = FormatFiveHourBurnRateStatus(
            summary.FiveHour.ConsumptionPerProviderHour, summary.ProviderCount > 1);
        var fiveHourCard = CreateKpiCard(
            _text.UsageHistoryFiveHourConsumed,
            FormatQuotaMultiple(summary.FiveHour.EquivalentWindows),
            fiveHourStatusText,
            CadenceStyle.FiveHour(_isLightTheme),
            "M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z",
            statusColor: fiveHourStatusColor,
            tooltip: ConsumptionTooltip(summary.FiveHour, providerCount, "5-hour", "5 小時", "5 小时"));
        Grid.SetColumn(fiveHourCard, 2);
        _summaryGrid.Children.Add(fiveHourCard);

        // Card 4: Weekly
        var (weeklyStatusText, weeklyStatusColor) = FormatWeeklyBurnRateStatus(
            summary.Weekly.ConsumptionPerProviderHour, summary.ProviderCount > 1);
        var weeklyCard = CreateKpiCard(
            _text.UsageHistoryWeeklyConsumed,
            FormatQuotaMultiple(summary.Weekly.EquivalentWindows),
            weeklyStatusText,
            CadenceStyle.Weekly(_isLightTheme),
            "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z",
            statusColor: weeklyStatusColor,
            tooltip: ConsumptionTooltip(summary.Weekly, providerCount, "weekly", "每週", "每周"));
        Grid.SetColumn(weeklyCard, 3);
        _summaryGrid.Children.Add(weeklyCard);

        // Card 5: Resets. Only some can be read as "how fully was this cycle used".
        var events = summary.ResetEvents;
        var measured = events.Count(item => item.Quality == UsageResetQuality.Measured);
        var unverified = events.Count - measured;
        var resetsCard = CreateKpiCard(
            _text.UsageHistoryResetsDetected,
            events.Count.ToString("N0", CultureInfo.CurrentCulture),
            events.Count == 0
                ? _text.UsageHistoryResetMarkersHint
                : L($"{measured:N0} measured · {unverified:N0} unverified",
                    $"{measured:N0} 次已量測 · {unverified:N0} 次未驗證",
                    $"{measured:N0} 次已测量 · {unverified:N0} 次未验证"),
            StatusStyle.Caution(_isLightTheme),
            "M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46A7.93 7.93 0 0 0 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74A7.93 7.93 0 0 0 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z",
            statusColor: StatusStyle.Caution(_isLightTheme),
            tooltip: L(
                "Measured: sampled closely enough around the reset to judge how much was used. Unverified: the app was not sampling at the time, or the window had never been used.",
                "已量測：重置前後取樣緊密，可判斷使用程度。未驗證：重置時程式未取樣，或該時段從未使用。",
                "已测量：重置前后采样紧密，可判断使用程度。未验证：重置时程序未采样，或该时段从未使用。"));
        Grid.SetColumn(resetsCard, 4);
        _summaryGrid.Children.Add(resetsCard);
    }

    private (string Text, string Color) FormatCoverageStatus(UsageDataQuality quality)
    {
        if (quality.GapCount == 0)
        {
            return (
                L("Fully covered", "完整涵蓋", "完整覆盖"),
                StatusStyle.Good(_isLightTheme));
        }

        var gaps = quality.GapCount == 1
            ? L("1 gap", "1 處中斷", "1 处中断")
            : L($"{quality.GapCount} gaps", $"{quality.GapCount} 處中斷", $"{quality.GapCount} 处中断");
        var text = L(
            $"{quality.CoveragePercent:0}% covered · {gaps}",
            $"涵蓋 {quality.CoveragePercent:0}% · {gaps}",
            $"覆盖 {quality.CoveragePercent:0}% · {gaps}");
        var color = quality.CoveragePercent >= 90
            ? StatusStyle.Good(_isLightTheme)
            : quality.CoveragePercent >= 60
                ? StatusStyle.Caution(_isLightTheme)
                : StatusStyle.Warn(_isLightTheme);
        return (text, color);
    }

    private static string FormatQuotaMultiple(double windows) =>
        windows >= 100 ? $"{windows:#,##0}×" : $"{windows:0.#}×";

    private string ConsumptionTooltip(
        UsageWindowMetrics metrics,
        int providerCount,
        string english,
        string traditional,
        string simplified)
    {
        var points = metrics.ConsumedPercent.ToString("#,##0", CultureInfo.CurrentCulture);
        var rate = metrics.ConsumptionPerProviderHour.ToString("0.##", CultureInfo.CurrentCulture);
        return L(
            $"{metrics.EquivalentWindows:0.#} full {english} quotas used: {points} percentage points added up across {providerCount} provider(s), averaging {rate}%/h each.",
            $"共使用 {metrics.EquivalentWindows:0.#} 個完整{traditional}額度：{providerCount} 個來源合計 {points} 個百分點，平均每個來源每小時 {rate}%。",
            $"共使用 {metrics.EquivalentWindows:0.#} 个完整{simplified}额度：{providerCount} 个来源合计 {points} 个百分点，平均每个来源每小时 {rate}%。");
    }

    private void UpdateTimeline(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlySet<string> providers)
    {
        // Failures stay in: the Records tab can filter them, and the overview
        // only shows the newest few anyway.
        _timelineRecords = _entries
            .Where(entry => entry.UpdatedAt >= start && entry.UpdatedAt <= end)
            .Where(entry => providers.Contains(entry.Provider))
            .ToArray();
        _timelineCaption.Text = L(
            $"Latest {Math.Min(_timelineRecords.Count, InlineActivityRows):N0} of {_timelineRecords.Count:N0} records",
            $"最新 {Math.Min(_timelineRecords.Count, InlineActivityRows):N0} / {_timelineRecords.Count:N0} 筆",
            $"最新 {Math.Min(_timelineRecords.Count, InlineActivityRows):N0} / {_timelineRecords.Count:N0} 条");
        RebuildInlineActivityRows();
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
                : _text.FormatPeriod(start.ToLocalTime(), end.ToLocalTime());
            return;
        }
        _periodLabel.Text = _text.FormatPeriod(start.ToLocalTime(), end.ToLocalTime());
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
        A11y.Tip(button, tooltip);
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
        A11y.Tip(button, tooltip);
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
        string statusColor,
        string? tooltip = null)
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
            FontSize = 10,
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
        if (tooltip is not null)
        {
            A11y.Tip(card, tooltip);
        }

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

        A11y.Name(card, $"{label}: {value}, {detail}");
        return card;
    }

    // The rate is the average per provider so it does not grow with how many
    // providers are tracked; "each" says so when several are combined.
    private (string Text, string Color) FormatFiveHourBurnRateStatus(double rate, bool perProvider)
    {
        var each = perProvider ? $" {L("each", "每來源", "每来源")}" : string.Empty;
        if (rate <= 0)
        {
            return ($"0%/h • {_text.UsageHistoryBurnRateIdle}", StatusStyle.Neutral(_isLightTheme));
        }
        if (rate < 2.0)
        {
            return ($"{rate:0.#}%/h{each} • {_text.UsageHistoryBurnRateLight}", StatusStyle.Good(_isLightTheme));
        }
        if (rate < 8.0)
        {
            return ($"{rate:0.#}%/h{each} • {_text.UsageHistoryBurnRateModerate}", StatusStyle.Caution(_isLightTheme));
        }
        return ($"{rate:0.#}%/h{each} • {_text.UsageHistoryBurnRateHigh}", StatusStyle.Critical(_isLightTheme));
    }

    private (string Text, string Color) FormatWeeklyBurnRateStatus(double rate, bool perProvider)
    {
        var each = perProvider ? $" {L("each", "每來源", "每来源")}" : string.Empty;
        if (rate <= 0)
        {
            return ($"0%/h • {_text.UsageHistoryBurnRateIdle}", StatusStyle.Neutral(_isLightTheme));
        }
        if (rate < 0.6)
        {
            return ($"{rate:0.##}%/h{each} • {_text.UsageHistoryBurnRateLight}", StatusStyle.Good(_isLightTheme));
        }
        if (rate < 1.8)
        {
            return ($"{rate:0.##}%/h{each} • {_text.UsageHistoryBurnRateModerate}", StatusStyle.Caution(_isLightTheme));
        }
        return ($"{rate:0.##}%/h{each} • {_text.UsageHistoryBurnRateHigh}", StatusStyle.Critical(_isLightTheme));
    }

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

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

        // Match the app's accent and let only the moving segment show.
        _loadingBar.Foreground = Brush(_palette.Accent);
        _loadingBar.Background = Brushes.Transparent;
        // Switches in the tabs (Records, Efficiency) use the same accent
        // instead of Fluent's system blue.
        var accentBrush = Brush(_palette.Accent);
        foreach (var key in new[]
                 {
                     "ToggleSwitchFillOn", "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed",
                     "ToggleSwitchStrokeOn", "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchStrokeOnPressed"
                 })
        {
            Resources[key] = accentBrush;
        }

        var knob = _isLightTheme ? Brushes.White : Brush("#FF0F1A15");
        Resources["ToggleSwitchKnobFillOn"] = knob;
        Resources["ToggleSwitchKnobFillOnPointerOver"] = knob;
        Resources["ToggleSwitchKnobFillOnPressed"] = knob;
        _tabSurface.Background = Brush(_palette.Soft);
        _tabSurface.BorderBrush = Brush(_palette.Border);
        UpdateTabStyles();

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

        _quotaSegmentedSurface.Background = Brush(_palette.Soft);
        _quotaSegmentedSurface.BorderBrush = Brush(_palette.Border);
        if (_isLightTheme)
        {
            _quotaSegmentedSurface.BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 4,
                OffsetY = 1,
                Color = Color.Parse("#08000000")
            });
        }
        else
        {
            _quotaSegmentedSurface.BoxShadow = default;
        }

        _inlineTableHeaderSurface.Background = Brush(_palette.Soft);
        _inlineTableHeaderSurface.BorderBrush = Brush(_palette.Border);
        if (_inlineTableHeaderSurface.Child is Grid header)
        {
            foreach (var child in header.Children)
            {
                if (child is TextBlock tb)
                {
                    tb.Foreground = Brush(_palette.Secondary);
                }
            }
        }

        _inlineEmptyState.Background = Brush(_palette.Soft);
        _inlineEmptyState.BorderBrush = Brush(_palette.Border);
        _inlineEmptyStateText.Foreground = Brush(_palette.Secondary);

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
        UpdateQuotaPillsStyle();
        ApplyTodayButtonTheme();
        ApplyAllProvidersButtonTheme();
        ApplySelectorTheme(_windowSelector);
        ApplyFullPeriodToggleTheme();
        foreach (var provider in _providerButtons)
        {
            ApplyProviderButtonTheme(provider);
        }
        UpdateSummary(_selection);
        RebuildInlineActivityRows();
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
        AutomationProperties.SetItemStatus(pill, isSelected ? L("selected", "已選取", "已选中") : string.Empty);
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

    private void UpdateQuotaPillsStyle()
    {
        foreach (var (index, pill) in _quotaSegmentedButtons)
        {
            ApplyQuotaPillTheme(pill, index, hovered: false);
        }
    }

    private void ApplyQuotaPillTheme(Button pill, int index, bool hovered)
    {
        var isSelected = _windowSelector.SelectedIndex == index;
        AutomationProperties.SetItemStatus(pill, isSelected ? L("selected", "已選取", "已选中") : string.Empty);
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
        AutomationProperties.SetItemStatus(_allProvidersButton, isAllSelected ? L("selected", "已選取", "已选中") : string.Empty);
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
        // Colour alone says "on"; say it in words too.
        AutomationProperties.SetItemStatus(button, selected ? L("selected", "已選取", "已选中") : string.Empty);
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

    private static string ProviderIconGeometry(string provider) => ProviderStyle.IconGeometry(provider);

    private string ProviderAccent(string provider) => ProviderStyle.Accent(provider, _isLightTheme, _palette.Secondary);

    private static SolidColorBrush Tint(string color, byte alpha)
    {
        var parsed = Color.Parse(color);
        return new SolidColorBrush(Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var control = (eventArgs.KeyModifiers & KeyModifiers.Control) != 0;
        if (eventArgs.Key == Key.F1)
        {
            ShortcutsRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
        else if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            Close();
        }
        else if (control && eventArgs.Key is >= Key.D1 and <= Key.D4)
        {
            SelectTab((InsightsTab)(eventArgs.Key - Key.D1));
            eventArgs.Handled = true;
        }
        else if (control && eventArgs.Key == Key.Tab)
        {
            var step = (eventArgs.KeyModifiers & KeyModifiers.Shift) != 0 ? 3 : 1;
            SelectTab((InsightsTab)(((int)_activeTab + step) % 4));
            eventArgs.Handled = true;
        }
        else if ((control && eventArgs.Key == Key.R) || eventArgs.Key == Key.F5)
        {
            Reload();
            eventArgs.Handled = true;
        }
        else if (control && eventArgs.Key == Key.F)
        {
            SelectTab(InsightsTab.Records);
            _recordsView.FocusSearch();
            eventArgs.Handled = true;
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
        else if (eventArgs.Key == Key.Home && eventArgs.KeyModifiers == KeyModifiers.Alt)
        {
            if (_todayButton.IsEnabled)
            {
                _anchorDate = DateTimeOffset.Now;
                ApplySelection();
                eventArgs.Handled = true;
            }
        }
    }

    // ---- Tabs ---------------------------------------------------------------

    public event EventHandler? ShortcutsRequested;

    /// <summary>Shows the given tab (used by the main window's menu to deep-link).</summary>
    public void ShowTab(InsightsTab tab) => SelectTab(tab);

    private void SelectTab(InsightsTab tab)
    {
        _activeTab = tab;
        _overviewScroll.IsVisible = tab == InsightsTab.Overview;
        _analysisView.IsVisible = tab == InsightsTab.Analysis;
        _efficiencyView.IsVisible = tab == InsightsTab.Efficiency;
        _recordsView.IsVisible = tab == InsightsTab.Records;
        UpdateTabStyles();
        RefreshActiveTab();
    }

    // The selection changed: every tab's data is stale, but only the visible
    // one is rebuilt now. The others catch up when they are opened.
    private void InvalidateTabs()
    {
        _tabVersions.Clear();
        RefreshActiveTab();
    }

    private void RefreshActiveTab()
    {
        if (_tabVersions.TryGetValue(_activeTab, out var built) && built == _selectionVersion)
        {
            return;
        }

        var (start, end) = SelectedPeriod();
        switch (_activeTab)
        {
            case InsightsTab.Analysis:
                _analysisView.Update(_selection, start, end);
                break;
            case InsightsTab.Efficiency:
                _efficiencyView.Update(_entries, start, end, SelectedProviders());
                break;
            case InsightsTab.Records:
                _recordsView.UpdateRecords(_timelineRecords, start, end);
                break;
        }

        _tabVersions[_activeTab] = _selectionVersion;
    }

    private void UpdateTabStyles()
    {
        foreach (var tab in _tabButtons.Keys)
        {
            ApplyTabTheme(tab, hovered: false);
        }
    }

    private void ApplyTabTheme(InsightsTab tab, bool hovered)
    {
        var pill = _tabButtons[tab];
        AutomationProperties.SetItemStatus(
            pill,
            tab == _activeTab ? L("selected", "已選取", "已选中") : string.Empty);
        if (tab == _activeTab)
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
            pill.FontWeight = FontWeight.SemiBold;
        }
    }

    private void UpdateCoverageChip(UsageSelectionSummary summary, string text, string color)
    {
        _coverageChip.IsVisible = summary.RecordCount > 0;
        _coverageChipText.Text = text;
        _coverageChipText.Foreground = Brush(color);
        _coverageChip.Background = Tint(color, _isLightTheme ? (byte)22 : (byte)35);
        _coverageChip.BorderBrush = Tint(color, _isLightTheme ? (byte)60 : (byte)80);
        A11y.Tip(_coverageChip, L(
            "How much of this period was actually recorded. The app only samples while it runs.",
            "此期間實際有記錄的比例。程式只會在執行時取樣。",
            "此期间实际有记录的比例。程序只会在运行时采样。"));
    }

    private string DisplayProvider(string provider) => ProviderStyle.DisplayName(provider);

    private string ChartProviderLabel(string provider) => ProviderStyle.DisplayName(provider);

    private static bool IsChartProvider(string provider) => ProviderStyle.IsKnown(provider);

    private static int ProviderOrder(string provider) => ProviderStyle.Order(provider);

    private string FormatDuration(TimeSpan duration) => _text.FormatDuration(duration);

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

    private static ColumnDefinitions InlineTableColumns() => new("180,180,*,*");

    private Control CreateInlineTableHeader()
    {
        var grid = new Grid { ColumnDefinitions = InlineTableColumns() };
        AddInlineHeaderCell(grid, _text.UsageHistoryTimestamp, 0);
        AddInlineHeaderCell(grid, _text.UsageHistoryProvider, 1);
        AddInlineHeaderCell(grid, _text.UsageHistoryFiveHour, 2);
        AddInlineHeaderCell(grid, _text.UsageHistoryWeek, 3);
        return grid;
    }

    private void AddInlineHeaderCell(Grid grid, string text, int column)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.5,
            Foreground = Brush(_palette.Secondary),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private void RebuildInlineActivityRows()
    {
        _inlineActivityRows.Children.Clear();

        if (_timelineRecords.Count == 0)
        {
            _inlineTableHeaderSurface.IsVisible = false;
            _inlineActivityRows.IsVisible = false;
            _inlineEmptyState.IsVisible = true;
            return;
        }

        _inlineTableHeaderSurface.IsVisible = true;
        _inlineActivityRows.IsVisible = true;
        _inlineEmptyState.IsVisible = false;

        var recent = _timelineRecords
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(InlineActivityRows)
            .ToList();

        for (var index = 0; index < recent.Count; index++)
        {
            _inlineActivityRows.Children.Add(CreateInlineActivityRow(recent[index], index));
        }
    }

    private Control CreateInlineActivityRow(UsageHistoryEntry entry, int index)
    {
        var success = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase);
        var rowBg = success
            ? index % 2 == 0 ? _palette.Row : _palette.RowAlternate
            : (_isLightTheme ? "#FFF5F5" : "#2D1D1D");
        var rowBorder = success
            ? _palette.Border
            : (_isLightTheme ? "#F87171" : "#EF4444");
        var rowHoverBg = success
            ? _palette.RowHover
            : (_isLightTheme ? "#FFEAEA" : "#382323");

        var row = new Border
        {
            Background = Brush(rowBg),
            BorderBrush = Brush(rowBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 8)
        };
        row.PointerEntered += (_, _) => row.Background = Brush(rowHoverBg);
        row.PointerExited += (_, _) => row.Background = Brush(rowBg);

        var grid = new Grid { ColumnDefinitions = InlineTableColumns() };
        grid.Children.Add(CreateInlineTimestampCell(entry.UpdatedAt));

        var provider = CreateInlineProviderPill(entry);
        Grid.SetColumn(provider, 1);
        grid.Children.Add(provider);

        if (!success && !string.IsNullOrWhiteSpace(entry.Error))
        {
            var error = CreateInlineErrorCell(_text.LocalizeError(entry.Error)!);
            Grid.SetColumn(error, 2);
            Grid.SetColumnSpan(error, 2);
            grid.Children.Add(error);
        }
        else
        {
            var fiveHour = CreateInlineQuotaCell(entry.FiveHourRemainingPercent, entry.FiveHourResetAt);
            var week = CreateInlineQuotaCell(entry.WeeklyRemainingPercent, entry.WeeklyResetAt);
            Grid.SetColumn(fiveHour, 2);
            Grid.SetColumn(week, 3);
            grid.Children.Add(fiveHour);
            grid.Children.Add(week);
        }

        row.Child = grid;
        return row;
    }

    private Control CreateInlineTimestampCell(DateTimeOffset updatedAt)
    {
        var local = updatedAt.ToLocalTime();
        return new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = _text.FormatDate(local),
                    FontSize = 11.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(_palette.Primary)
                },
                new TextBlock
                {
                    Text = local.ToString("HH:mm:ss", CultureInfo.CurrentCulture),
                    FontSize = 10,
                    Foreground = Brush(_palette.Secondary)
                }
            }
        };
    }

    private Control CreateInlineProviderPill(UsageHistoryEntry entry)
    {
        var accent = ProviderAccent(entry.Provider);
        var iconPath = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(ProviderIconGeometry(entry.Provider)),
            Fill = Brush(accent),
            Width = 11,
            Height = 11,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        var isSuccess = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase);
        var label = new TextBlock
        {
            Text = isSuccess
                ? DisplayProvider(entry.Provider)
                : $"{DisplayProvider(entry.Provider)} · {_text.UsageHistoryError}",
            FontSize = 10.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        };
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { iconPath, label }
        };
        return new Border
        {
            Background = Tint(accent, _isLightTheme ? (byte)22 : (byte)36),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)90 : (byte)130),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack
        };
    }

    private Control CreateInlineQuotaCell(int? remainingPercent, DateTimeOffset? resetAt)
    {
        var signal = SignalColor(remainingPercent);
        var statusDot = new Avalonia.Controls.Shapes.Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = Brush(signal),
            VerticalAlignment = VerticalAlignment.Center
        };
        var percent = new TextBlock
        {
            Text = remainingPercent is null ? "—" : $"{Math.Clamp(remainingPercent.Value, 0, 100)}%",
            FontWeight = FontWeight.Bold,
            Foreground = Brush(signal),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        var percentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { statusDot, percent }
        };
        var reset = new TextBlock
        {
            Text = resetAt is null
                ? _text.ResetUnavailable
                : _text.FormatUsageHistoryReset(resetAt.Value.ToLocalTime()),
            FontSize = 10,
            Foreground = Brush(_palette.Secondary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(reset, 1);
        heading.Children.Add(percentStack);
        heading.Children.Add(reset);

        return new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { heading, CreateInlineProgress(remainingPercent, signal) }
        };
    }

    private Control CreateInlineProgress(int? remainingPercent, string signal)
    {
        var fill = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Brush(signal),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var track = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Tint(_palette.Secondary, _isLightTheme ? (byte)30 : (byte)45),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) =>
        {
            var value = Math.Clamp(remainingPercent ?? 0, 0, 100);
            fill.Width = Math.Max(0, track.Bounds.Width * (value / 100.0));
        };
        return track;
    }

    private Control CreateInlineErrorCell(string error)
    {
        return new Border
        {
            Background = Tint(_palette.Error, _isLightTheme ? (byte)18 : (byte)35),
            BorderBrush = Tint(_palette.Error, _isLightTheme ? (byte)80 : (byte)120),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 5),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = error,
                Foreground = Brush(_palette.Error),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private string SignalColor(int? remainingPercent) => (remainingPercent ?? 0) switch
    {
        < 20 => _palette.Red,
        < 50 => _palette.Orange,
        < 80 => _palette.Yellow,
        _ => _palette.Green
    };

    internal sealed record HistoryPalette(
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
        string WarningText,
        string Row,
        string RowAlternate,
        string RowHover,
        string Green,
        string Yellow,
        string Orange,
        string Red)
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
            "#FF0B7A5A",
            "#FF0F7B5C",
            "#FF0D6E53",
            "#FFE5F5EE",
            "#FFA8D8C5",
            "#FFB13A32",
            "#FFFFF5DF",
            "#FFF0D08C",
            "#FF7A5314",
            "#FFFFFFFF",
            "#FFFAFBFA",
            "#FFF1F7F4",
            "#FF0F8A5F",
            "#FFA98700",
            "#FFD97706",
            "#FFD84A42");

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
            "#FFFFD18A",
            "#FF202421",
            "#FF232824",
            "#FF29352F",
            "#FF4AD894",
            "#FFF2CF5B",
            "#FFFF9847",
            "#FFFF6666");
    }
}
