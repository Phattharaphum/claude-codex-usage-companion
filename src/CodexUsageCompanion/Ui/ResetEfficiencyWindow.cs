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
/// Shows quota left unused immediately before each detected reset. This makes
/// reset cycles comparable without conflating remaining quota with consumption.
/// </summary>
public sealed class ResetEfficiencyWindow : Window
{
    private readonly CompanionSettings _settings;
    private readonly UiText _text;
    private readonly UsageHistoryReader _reader;
    private readonly ComboBox _rangeSelector;
    private readonly ComboBox _kindSelector;
    private readonly ComboBox _providerSelector;
    private readonly WrapPanel _summary = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _events = new() { Spacing = 8 };
    private readonly List<TextBlock> _mutedLabels = [];
    private readonly Border _root;
    private readonly Border _titleBar;
    private readonly TextBlock _windowTitle;
    private readonly Button _minimizeButton;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private readonly Border _heroCard;
    private readonly Border _heroBadge;
    private readonly PathIcon _heroIcon;
    private readonly TextBlock _pageTitle;
    private readonly TextBlock _subtitle = new();
    private readonly Button _refreshButton;
    private readonly Border _filterCard;
    private readonly Border _periodPill;
    private readonly TextBlock _period = new();
    private readonly Border _explanationCard;
    private readonly Border _explanationBadge;
    private readonly PathIcon _explanationIcon;
    private readonly TextBlock _explanationTitle;
    private readonly TextBlock _explanationText;
    private readonly Border _eventListCard;
    private readonly Border _eventHeaderSurface;
    private readonly TextBlock _eventHeading;
    private readonly TextBlock _eventCount = new();
    private readonly TextBlock _eventNote = new();
    private readonly ToggleSwitch _showUnverified = new()
    {
        OnContent = string.Empty,
        OffContent = string.Empty,
        VerticalAlignment = VerticalAlignment.Center
    };
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
    private IReadOnlyList<UsageHistoryEntry> _entries = [];
    private IReadOnlyList<string> _providerKeys = [];
    private EfficiencyPalette _palette = EfficiencyPalette.Light;
    private bool _isLightTheme = true;
    private bool _hasLoadError;

    public ResetEfficiencyWindow(
        CompanionSettings settings,
        UiText text,
        UsageHistoryReader? reader = null)
    {
        _settings = settings;
        _text = text;
        _reader = reader ?? new UsageHistoryReader();

        _rangeSelector = Selector(
            new[]
            {
                L("7 days", "7 天", "7 天"),
                L("30 days", "30 天", "30 天"),
                L("90 days", "90 天", "90 天"),
                text.UsageHistoryAll
            }, 1, 112);
        _kindSelector = Selector(
            new[] { L("All resets", "所有重置", "所有重置"), "5hr", text.UsageHistoryWeek },
            0, 126);
        _providerSelector = Selector(
            new[] { L("All providers", "所有來源", "所有来源") },
            0, 158);

        Title = text.ResetEfficiencyTitle;
        Width = 1000;
        Height = 800;
        MinWidth = 800;
        MinHeight = 600;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;

        _windowTitle = new TextBlock
        {
            Text = L("Reset efficiency", "重置效率", "重置效率"),
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _minimizeButton = WindowControlButton(WindowIcon("M4 11H20V13H4Z"), text.MinimizeAction);
        _maximizeButton = WindowControlButton(
            WindowIcon("M5 5H19V7H5ZM5 17H19V19H5ZM5 7H7V17H5ZM17 7H19V17H17Z"),
            "Maximize");
        _closeButton = WindowControlButton(
            WindowIcon("M6.7 5.3L12 10.6L17.3 5.3L18.7 6.7L13.4 12L18.7 17.3L17.3 18.7L12 13.4L6.7 18.7L5.3 17.3L10.6 12L5.3 6.7Z"),
            text.CloseAction);
        _minimizeButton.Click += (_, _) => WindowState = WindowState.Minimized;
        _maximizeButton.Click += (_, _) => ToggleMaximized();
        _closeButton.Click += (_, _) => Close();
        RegisterWindowControlHover(_minimizeButton, closeButton: false);
        RegisterWindowControlHover(_maximizeButton, closeButton: false);
        RegisterWindowControlHover(_closeButton, closeButton: true);

        var titleActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _minimizeButton, _maximizeButton, _closeButton }
        };
        var titleLeft = new Border { Background = Brushes.Transparent };
        var titleCenter = new Border { Background = Brushes.Transparent, Child = _windowTitle };
        titleLeft.PointerPressed += HandleTitleBarPointerPressed;
        titleCenter.PointerPressed += HandleTitleBarPointerPressed;
        var titleGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("100,*,100"),
            Height = 38,
            Children = { titleLeft, titleCenter, titleActions }
        };
        Grid.SetColumn(titleCenter, 1);
        Grid.SetColumn(titleActions, 2);
        _titleBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            Child = titleGrid
        };

        _heroIcon = new PathIcon
        {
            Width = 22,
            Height = 22,
            Data = Geometry.Parse("M11 2V12H21C21 6.48 16.52 2 11 2ZM9 4.07C4.94 4.56 2 8.03 2 12C2 16.42 5.58 20 10 20C13.97 20 17.44 17.06 17.93 13H9V4.07Z")
        };
        _heroBadge = new Border
        {
            Width = 46,
            Height = 46,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Child = _heroIcon
        };
        _pageTitle = new TextBlock
        {
            Text = L("Reset efficiency", "重置效率", "重置效率"),
            FontSize = 25,
            FontWeight = FontWeight.Bold
        };
        _subtitle.Text = L(
            "See how fully each quota cycle was used before it reset",
            "查看每次重置前的額度週期使用程度",
            "查看每次重置前的额度周期使用程度");
        _subtitle.FontSize = 11.5;
        _subtitle.TextTrimming = TextTrimming.CharacterEllipsis;
        var heroHeading = new StackPanel { Spacing = 2, Children = { _pageTitle, _subtitle } };
        var heroIdentity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _heroBadge, heroHeading }
        };
        _refreshButton = ActionButton(
            ActionContent(
                "M12 4V1L8 5L12 9V6C15.31 6 18 8.69 18 12C18 15.31 15.31 18 12 18C9.24 18 6.92 16.14 6.22 13.6L4.29 14.12C5.22 17.5 8.32 20 12 20C16.42 20 20 16.42 20 12C20 7.58 16.42 4 12 4Z",
                text.RefreshAction),
            118);
        _refreshButton.Click += (_, _) => Reload();
        RegisterActionHover(_refreshButton);
        // The period sits beside the refresh action so the filter row has room
        // for every control on one line.
        var heroActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _refreshButton }
        };
        var heroGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children = { heroIdentity, heroActions }
        };
        Grid.SetColumn(heroActions, 1);
        _heroCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(17, 14),
            Child = heroGrid
        };

        _period.FontSize = 10.5;
        _period.FontWeight = FontWeight.SemiBold;
        _period.VerticalAlignment = VerticalAlignment.Center;
        _periodPill = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 7),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _period
        };
        var filters = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                FilterGroup(text.UsageHistoryTimeRange, _rangeSelector),
                FilterGroup(L("Reset type", "重置類型", "重置类型"), _kindSelector),
                FilterGroup(text.UsageHistoryProvider, _providerSelector),
                FilterGroup(L("Unverified cycles", "未驗證週期", "未验证周期"), _showUnverified)
            }
        };
        heroActions.Children.Insert(0, _periodPill);
        _filterCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 10),
            Child = filters
        };

        _explanationIcon = new PathIcon
        {
            Width = 18,
            Height = 18,
            Data = Geometry.Parse("M11 17H13V11H11ZM12 2C6.48 2 2 6.48 2 12S6.48 22 12 22S22 17.52 22 12S17.52 2 12 2ZM12 20C7.59 20 4 16.41 4 12S7.59 4 12 4S20 7.59 20 12S16.41 20 12 20ZM11 7H13V9H11Z")
        };
        _explanationBadge = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(11),
            Child = _explanationIcon
        };
        _explanationTitle = new TextBlock
        {
            Text = L("How to read utilization", "如何判讀使用率", "如何解读使用率"),
            FontSize = 13,
            FontWeight = FontWeight.Bold
        };
        _explanationText = new TextBlock
        {
            Text = L(
                "Utilization is how much of a quota window was used before it reset; higher means less went to waste. Only measured cycles count. A cycle is unverified when the app was not sampling around the reset (usage just before it is unknown) or the window was never used, so it is left out of the averages.",
                "使用率是額度週期在重置前被用掉的比例，越高代表浪費越少。僅計算「已量測」的週期；若重置時程式未取樣（無法得知重置前的用量）或該時段從未使用，則視為「未驗證」，不納入平均。",
                "使用率是额度周期在重置前被用掉的比例，越高代表浪费越少。仅计算“已测量”的周期；若重置时程序未采样（无法得知重置前的用量）或该时段从未使用，则视为“未验证”，不纳入平均。"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        var explanationCopy = new StackPanel
        {
            Spacing = 3,
            Children = { _explanationTitle, _explanationText }
        };
        var explanationGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 11,
            Children = { _explanationBadge, explanationCopy }
        };
        Grid.SetColumn(explanationCopy, 1);
        _explanationCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 12),
            Child = explanationGrid
        };

        _eventHeading = new TextBlock
        {
            Text = L("Reset cycles", "重置週期", "重置周期"),
            FontSize = 15.5,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _eventCount.FontSize = 10.5;
        _eventCount.FontWeight = FontWeight.SemiBold;
        _eventCount.VerticalAlignment = VerticalAlignment.Center;
        _eventNote.FontSize = 10.5;
        _eventNote.TextWrapping = TextWrapping.Wrap;
        _eventNote.IsVisible = false;
        var eventTitle = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _eventHeading, _eventNote }
        };
        var eventHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { eventTitle, _eventCount }
        };
        Grid.SetColumn(_eventCount, 1);
        _eventHeaderSurface = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(15, 12),
            Child = eventHeader
        };
        var eventScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(10), Child = _events }
        };
        var eventGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { _eventHeaderSurface, eventScroll }
        };
        Grid.SetRow(eventScroll, 1);
        _eventListCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Child = eventGrid
        };
        _eventListCard.SizeChanged += (_, _) => ApplyRoundedClip(_eventListCard, 16);

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            RowSpacing = 12,
            Margin = new Thickness(20),
            Children = { _heroCard, _filterCard, _explanationCard, _summary, _eventListCard }
        };
        Grid.SetRow(_filterCard, 1);
        Grid.SetRow(_explanationCard, 2);
        Grid.SetRow(_summary, 3);
        Grid.SetRow(_eventListCard, 4);
        var windowLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("38,*"),
            Children = { _titleBar, body, _loadingBar }
        };
        Grid.SetRow(body, 1);
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

        _rangeSelector.SelectionChanged += (_, _) => ApplyFilters();
        _kindSelector.SelectionChanged += (_, _) => ApplyFilters();
        _providerSelector.SelectionChanged += (_, _) => ApplyFilters();
        _showUnverified.IsCheckedChanged += (_, _) => ApplyFilters();
        Closed += (_, _) => _closed = true;
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

        if (version != _loadVersion || _closed)
        {
            return;
        }

        _loadingBar.IsVisible = false;
        ApplyReadResult(result);
    }

    private void ApplyReadResult(UsageHistoryReadResult result)
    {
        _entries = result.Entries;
        _hasLoadError = !string.IsNullOrWhiteSpace(result.Error);
        var selectedProvider = _providerSelector.SelectedIndex > 0 &&
            _providerSelector.SelectedIndex - 1 < _providerKeys.Count
                ? _providerKeys[_providerSelector.SelectedIndex - 1]
                : null;
        _providerKeys = _entries
            .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ProviderOrder)
            .ThenBy(provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _providerSelector.ItemsSource = new[] { L("All providers", "所有來源", "所有来源") }
            .Concat(_providerKeys.Select(DisplayProvider))
            .ToArray();
        var selectedIndex = selectedProvider is null
            ? 0
            : _providerKeys
                .Select((provider, index) => new { provider, index })
                .FirstOrDefault(item => string.Equals(item.provider, selectedProvider, StringComparison.OrdinalIgnoreCase))
                ?.index + 1 ?? 0;
        _providerSelector.SelectedIndex = selectedIndex;
        _subtitle.Text = result.Error ?? L(
            "See how fully each quota cycle was used before it reset",
            "查看每次重置前的額度週期使用程度",
            "查看每次重置前的额度周期使用程度");
        _subtitle.Foreground = Brush(_hasLoadError ? _palette.Error : _palette.Secondary);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var end = DateTimeOffset.Now;
        DateTimeOffset? start = _rangeSelector.SelectedIndex switch
        {
            0 => end.AddDays(-7),
            1 => end.AddDays(-30),
            2 => end.AddDays(-90),
            _ => null
        };
        IReadOnlySet<string>? providers = _providerSelector.SelectedIndex > 0 &&
            _providerSelector.SelectedIndex - 1 < _providerKeys.Count
                ? new HashSet<string>([_providerKeys[_providerSelector.SelectedIndex - 1]], StringComparer.OrdinalIgnoreCase)
                : null;
        var allEvents = UsageHistoryAnalytics.FindResetEvents(_entries, start, end, providers)
            .Where(item => _kindSelector.SelectedIndex switch
            {
                1 => item.Kind is UsageResetKind.FiveHour or UsageResetKind.Both,
                2 => item.Kind is UsageResetKind.Weekly or UsageResetKind.Both,
                _ => true
            })
            .ToArray();
        _period.Text = start is null
            ? L("All recorded history", "所有記錄", "所有记录")
            : $"{start.Value.ToLocalTime():MMM d, yyyy} — {end.ToLocalTime():MMM d, yyyy}";
        // Averages only ever use measured cycles; the switch decides whether
        // the unverified ones are also listed (dimmed) for inspection.
        var shown = _showUnverified.IsChecked == true
            ? allEvents
            : allEvents.Where(item => item.Quality == UsageResetQuality.Measured).ToArray();
        UpdateSummary(allEvents);
        UpdateEvents(shown, allEvents);
    }

    private void UpdateSummary(IReadOnlyList<UsageResetEvent> events)
    {
        _summary.Children.Clear();
        var measured = events.Where(item => item.Quality == UsageResetQuality.Measured).ToArray();
        var sampledGaps = events.Count(item => item.Quality == UsageResetQuality.Estimated);
        var idle = events.Count(item => item.Quality == UsageResetQuality.Idle);

        // Utilization is the share of a window used before it reset.
        var fiveHourUsed = measured
            .Where(item => item.Kind is UsageResetKind.FiveHour or UsageResetKind.Both)
            .Where(item => item.FiveHourRemainingBefore is not null)
            .Select(item => 100 - Math.Clamp(item.FiveHourRemainingBefore!.Value, 0, 100))
            .ToArray();
        var weeklyUsed = measured
            .Where(item => item.Kind is UsageResetKind.Weekly or UsageResetKind.Both)
            .Where(item => item.WeeklyRemainingBefore is not null)
            .Select(item => 100 - Math.Clamp(item.WeeklyRemainingBefore!.Value, 0, 100))
            .ToArray();
        var combined = fiveHourUsed.Concat(weeklyUsed).ToArray();

        _summary.Children.Add(SummaryCard(
            L("Measured cycles", "已量測週期", "已测量周期"),
            measured.Length.ToString("N0", CultureInfo.CurrentCulture),
            L($"of {events.Count:N0} detected", $"共偵測到 {events.Count:N0} 次", $"共检测到 {events.Count:N0} 次"),
            _palette.Blue));
        _summary.Children.Add(SummaryCard(
            L("5hr utilization", "5 小時使用率", "5 小时使用率"),
            AveragePercent(fiveHourUsed),
            fiveHourUsed.Length == 0
                ? L("No measured cycles", "沒有已量測週期", "没有已测量周期")
                : L("Higher means less wasted", "越高代表浪費越少", "越高代表浪费越少"),
            _palette.Orange));
        _summary.Children.Add(SummaryCard(
            L("Week utilization", "每週使用率", "每周使用率"),
            AveragePercent(weeklyUsed),
            weeklyUsed.Length == 0
                ? L("No measured cycles", "沒有已量測週期", "没有已测量周期")
                : L("Higher means less wasted", "越高代表浪費越少", "越高代表浪费越少"),
            _palette.Purple));
        _summary.Children.Add(SummaryCard(
            L("Overall utilization", "整體使用率", "整体使用率"),
            AveragePercent(combined),
            L("Across measured cycles", "所有已量測週期", "所有已测量周期"),
            _palette.Green));
        _summary.Children.Add(SummaryCard(
            L("Unverified", "未驗證", "未验证"),
            (sampledGaps + idle).ToString("N0", CultureInfo.CurrentCulture),
            L($"{sampledGaps:N0} not sampled · {idle:N0} unused", $"{sampledGaps:N0} 次未取樣 · {idle:N0} 次未使用", $"{sampledGaps:N0} 次未采样 · {idle:N0} 次未使用"),
            _palette.Yellow));
    }

    private void UpdateEvents(IReadOnlyList<UsageResetEvent> resetEvents, IReadOnlyList<UsageResetEvent> allEvents)
    {
        _events.Children.Clear();
        var measuredCount = allEvents.Count(item => item.Quality == UsageResetQuality.Measured);
        var unverifiedCount = allEvents.Count - measuredCount;
        var visibleCount = Math.Min(resetEvents.Count, 250);
        _eventCount.Text = _showUnverified.IsChecked == true
            ? L(
                $"{allEvents.Count:N0} detected · {measuredCount:N0} measured",
                $"偵測到 {allEvents.Count:N0} 次 · {measuredCount:N0} 次已量測",
                $"检测到 {allEvents.Count:N0} 次 · {measuredCount:N0} 次已测量")
            : resetEvents.Count > visibleCount
                ? L(
                    $"{resetEvents.Count:N0} measured · newest {visibleCount:N0} shown",
                    $"已量測 {resetEvents.Count:N0} 次 · 顯示最新 {visibleCount:N0} 次",
                    $"已测量 {resetEvents.Count:N0} 次 · 显示最新 {visibleCount:N0} 次")
                : L(
                    $"{resetEvents.Count:N0} measured",
                    $"已量測 {resetEvents.Count:N0} 次",
                    $"已测量 {resetEvents.Count:N0} 次");
        _eventNote.IsVisible = unverifiedCount > 0;
        _eventNote.Text = _showUnverified.IsChecked == true
            ? L(
                "Unverified cycles are dimmed and are not part of the averages above.",
                "未驗證的週期以淡色顯示，不計入上方平均。",
                "未验证的周期以淡色显示，不计入上方平均。")
            : L(
                $"{unverifiedCount:N0} unverified cycle(s) hidden: the app wasn't sampling at the time, or the window was never used. Turn on \"Unverified cycles\" to inspect them.",
                $"已隱藏 {unverifiedCount:N0} 個未驗證週期：重置時程式未取樣，或該時段從未使用。開啟「未驗證週期」即可檢視。",
                $"已隐藏 {unverifiedCount:N0} 个未验证周期：重置时程序未采样，或该时段从未使用。开启“未验证周期”即可查看。");
        if (resetEvents.Count == 0)
        {
            _events.Children.Add(EmptyState());
            return;
        }

        foreach (var resetEvent in resetEvents.Take(250))
        {
            _events.Children.Add(EventCard(resetEvent));
        }
    }

    private Control EventCard(UsageResetEvent item)
    {
        var providerAccent = ProviderColor(item.Provider);
        var provider = Pill(DisplayProvider(item.Provider), providerAccent);
        var kind = Pill(
            item.Kind switch
            {
                UsageResetKind.FiveHour => L("5hr reset", "5 小時重置", "5 小时重置"),
                UsageResetKind.Weekly => L("Week reset", "每週重置", "每周重置"),
                _ => L("5hr + week reset", "5 小時 + 每週重置", "5 小时 + 每周重置")
            },
            _palette.KindAccent);
        var quality = QualityPill(item);
        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { provider, kind, quality }
        };
        var time = new TextBlock
        {
            Text = item.ObservedAt.ToLocalTime().ToString("ddd, MMM d · HH:mm", CultureInfo.CurrentCulture),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(_palette.Primary)
        };
        var subtime = new TextBlock
        {
            Text = L(
                $"Expected {item.ExpectedAt.ToLocalTime():MMM d HH:mm}  ·  Observation gap {FormatDuration(item.ObservationGap)}",
                $"預計 {item.ExpectedAt.ToLocalTime():MMM d HH:mm}  ·  觀察間隔 {FormatDuration(item.ObservationGap)}",
                $"预计 {item.ExpectedAt.ToLocalTime():MMM d HH:mm}  ·  观察间隔 {FormatDuration(item.ObservationGap)}"),
            FontSize = 10,
            Foreground = Brush(_palette.Secondary),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel { Spacing = 3, Children = { time, subtime } },
                badges
            }
        };
        Grid.SetColumn(badges, 1);

        // Only the window(s) that actually reset have a cycle to judge. The
        // other one is shown as context, not as a result.
        var fiveHourReset = item.Kind is UsageResetKind.FiveHour or UsageResetKind.Both;
        var weeklyReset = item.Kind is UsageResetKind.Weekly or UsageResetKind.Both;
        var quotaGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        quotaGrid.Children.Add(fiveHourReset
            ? QuotaPanel(
                L("5-hour quota", "5 小時額度", "5 小时额度"),
                item.FiveHourRemainingBefore,
                _palette.Orange,
                item.Quality)
            : NotResetPanel(L("5-hour quota", "5 小時額度", "5 小时额度"), item.FiveHourRemainingBefore));
        var week = weeklyReset
            ? QuotaPanel(
                L("Weekly quota", "每週額度", "每周额度"),
                item.WeeklyRemainingBefore,
                _palette.Purple,
                item.Quality)
            : NotResetPanel(L("Weekly quota", "每週額度", "每周额度"), item.WeeklyRemainingBefore);
        Grid.SetColumn(week, 1);
        quotaGrid.Children.Add(week);

        var card = new Border
        {
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(14, 12),
            Opacity = item.Quality == UsageResetQuality.Measured ? 1 : 0.72,
            Child = new StackPanel { Spacing = 11, Children = { header, quotaGrid } }
        };
        ToolTip.SetTip(card, QualityExplanation(item));
        card.PointerEntered += (_, _) => card.Background = Brush(_palette.SurfaceHover);
        card.PointerExited += (_, _) => card.Background = Brush(_palette.Surface);
        return card;
    }

    private string QualityExplanation(UsageResetEvent item) => item.Quality switch
    {
        UsageResetQuality.Estimated => L(
            $"Not sampled for {FormatDuration(item.ObservationGap)} before this reset, so how much was used just before it is unknown. Excluded from the averages.",
            $"此次重置前有 {FormatDuration(item.ObservationGap)} 未取樣，無法得知重置前的實際用量。不計入平均。",
            $"此次重置前有 {FormatDuration(item.ObservationGap)} 未采样，无法得知重置前的实际用量。不计入平均。"),
        UsageResetQuality.Idle => L(
            "The window expired without being used, so there is no cycle to judge. Excluded from the averages.",
            "此時段到期時完全未使用，沒有可判斷的週期。不計入平均。",
            "此时段到期时完全未使用，没有可判断的周期。不计入平均。"),
        _ => L(
            "Sampled closely around the reset; the pre-reset value reflects how much was used.",
            "重置前後取樣緊密，重置前的數值可反映實際使用程度。",
            "重置前后采样紧密，重置前的数值可反映实际使用程度。")
    };

    private Border QualityPill(UsageResetEvent item) => item.Quality switch
    {
        UsageResetQuality.Estimated => Pill(L("Not sampled", "未取樣", "未采样"), _palette.Yellow),
        UsageResetQuality.Idle => Pill(L("Unused", "未使用", "未使用"), _palette.Secondary),
        _ => Pill(L("Measured", "已量測", "已测量"), _palette.Green)
    };

    private Control NotResetPanel(string label, int? remaining) => new Border
    {
        Background = Brush(_palette.Soft),
        BorderBrush = Brush(_palette.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(11),
        Padding = new Thickness(11, 9),
        Child = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 10.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(_palette.Secondary)
                },
                new TextBlock
                {
                    Text = remaining is null
                        ? L("Did not reset", "未重置", "未重置")
                        : L($"{remaining}% left · did not reset", $"剩餘 {remaining}% · 未重置", $"剩余 {remaining}% · 未重置"),
                    FontSize = 10,
                    Foreground = Brush(_palette.Secondary)
                }
            }
        }
    };

    private Control QuotaPanel(string label, int? unused, string accent, UsageResetQuality quality)
    {
        var measured = quality == UsageResetQuality.Measured;
        var used = unused is null ? (int?)null : 100 - Math.Clamp(unused.Value, 0, 100);
        // Only a measured cycle earns a judgement colour; an idle or unsampled
        // one is not "bad", it is simply not informative.
        var valueColor = measured ? UtilizationColor(used) : _palette.Secondary;
        var caption = quality switch
        {
            UsageResetQuality.Estimated => L("Last reading before the gap", "未取樣前的最後讀數", "未采样前的最后读数"),
            UsageResetQuality.Idle => L("Never used", "從未使用", "从未使用"),
            _ => L("Used before reset", "重置前已使用", "重置前已使用")
        };
        var value = new TextBlock
        {
            Text = used is null ? "—" : $"{used}%",
            FontSize = 19,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(valueColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = label,
                            FontSize = 10.5,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brush(_palette.Primary)
                        },
                        new TextBlock
                        {
                            Text = caption,
                            FontSize = 9.5,
                            Foreground = Brush(_palette.Secondary)
                        }
                    }
                },
                value
            }
        };
        Grid.SetColumn(value, 1);
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = L("Unused", "未使用", "未使用"),
                    FontSize = 9.5,
                    Foreground = Brush(_palette.Secondary)
                },
                new TextBlock
                {
                    Text = unused is null ? "—" : $"{unused}%",
                    FontSize = 9.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(_palette.Secondary)
                }
            }
        };
        Grid.SetColumn(footer.Children[1], 1);
        return new Border
        {
            Background = Brush(_palette.Soft),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(11, 9),
            Child = new StackPanel
            {
                Spacing = 7,
                Children = { heading, QuotaBar(used, measured ? accent : _palette.Secondary), footer }
            }
        };
    }

    private Control QuotaBar(int? used, string accent)
    {
        var fill = new Border
        {
            Background = Brush(accent),
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4)
        };
        var track = new Border
        {
            Background = Brush(_palette.Track),
            Height = 7,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) =>
            fill.Width = Math.Round(track.Bounds.Width * Math.Clamp(used ?? 0, 0, 100) / 100d);
        return track;
    }

    private Border SummaryCard(string label, string value, string detail, string accent)
    {
        var card = new Border
        {
            Width = 178,
            MinHeight = 92,
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 11),
            Margin = new Thickness(0, 0, 9, 5),
            Child = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 7,
                        Children =
                        {
                            new Border
                            {
                                Width = 8,
                                Height = 8,
                                CornerRadius = new CornerRadius(4),
                                Background = Brush(accent),
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = label.ToUpperInvariant(),
                                FontSize = 9.5,
                                FontWeight = FontWeight.Bold,
                                LetterSpacing = 0.35,
                                Foreground = Brush(_palette.Secondary),
                                TextTrimming = TextTrimming.CharacterEllipsis
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = value,
                        FontSize = 22,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush(_palette.Primary)
                    },
                    new TextBlock
                    {
                        Text = detail,
                        FontSize = 10,
                        Foreground = Brush(_palette.Secondary),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
        AddShadow(card, 10, 2);
        return card;
    }

    private Control EmptyState() => new Border
    {
        Background = Brush(_palette.Soft),
        BorderBrush = Brush(_palette.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(13),
        Padding = new Thickness(30, 36),
        Child = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new Border
                {
                    Width = 42,
                    Height = 42,
                    CornerRadius = new CornerRadius(14),
                    Background = Brush(_palette.AccentSoft),
                    Child = new PathIcon
                    {
                        Width = 21,
                        Height = 21,
                        Foreground = Brush(_palette.Accent),
                        Data = Geometry.Parse("M12 4V1L8 5L12 9V6C15.31 6 18 8.69 18 12C18 15.31 15.31 18 12 18C9.24 18 6.92 16.14 6.22 13.6L4.29 14.12C5.22 17.5 8.32 20 12 20C16.42 20 20 16.42 20 12C20 7.58 16.42 4 12 4Z")
                    }
                },
                new TextBlock
                {
                    Text = L("No reset cycles detected in this period", "此期間未偵測到重置週期", "此期间未检测到重置周期"),
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.Primary),
                    TextAlignment = TextAlignment.Center
                },
                new TextBlock
                {
                    Text = L(
                        "Keep usage logging enabled. A cycle appears after observations exist on both sides of a reset.",
                        "請保持啟用用量記錄；重置前後都有觀察資料時才會顯示週期。",
                        "请保持启用用量日志；重置前后都有观察数据时才会显示周期。"),
                    MaxWidth = 520,
                    FontSize = 10.5,
                    Foreground = Brush(_palette.Secondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                }
            }
        }
    };

    private Border Pill(string text, string accent) => new()
    {
        Background = Tint(accent, _isLightTheme ? (byte)24 : (byte)38),
        BorderBrush = Tint(accent, _isLightTheme ? (byte)88 : (byte)128),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(11),
        Padding = new Thickness(9, 4),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 9.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    private Control FilterGroup(string label, Control control)
    {
        var labelText = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.35,
            VerticalAlignment = VerticalAlignment.Center
        };
        _mutedLabels.Add(labelText);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { labelText, control }
        };
    }

    private void ApplyVisualTheme()
    {
        _isLightTheme = ActualThemeVariant == ThemeVariant.Light;
        _palette = _isLightTheme ? EfficiencyPalette.Light : EfficiencyPalette.Dark;
        Background = Brushes.Transparent;
        _root.Background = Brush(_palette.Root);
        _root.BorderBrush = Brush(_palette.BorderStrong);
        _titleBar.Background = Brush(_palette.Surface);
        _titleBar.BorderBrush = Brush(_palette.Border);
        _windowTitle.Foreground = Brush(_palette.Primary);
        ApplyWindowControlTheme(_minimizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_maximizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_closeButton, closeButton: true, hovered: false);

        _heroCard.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(_palette.HeaderStart), 0),
                new GradientStop(Color.Parse(_palette.HeaderEnd), 1)
            }
        };
        _heroCard.BorderBrush = Brush(_palette.Border);
        _heroCard.BoxShadow = default;
        AddShadow(_heroCard, 18, 4);
        _heroBadge.Background = Brush(_palette.AccentSoft);
        _heroBadge.BorderBrush = Brush(_palette.AccentBorder);
        _heroIcon.Foreground = Brush(_palette.Accent);
        _pageTitle.Foreground = Brush(_palette.Primary);
        _subtitle.Foreground = Brush(_hasLoadError ? _palette.Error : _palette.Secondary);
        ApplyActionTheme(_refreshButton, hovered: false);

        _filterCard.Background = Brush(_palette.Surface);
        _filterCard.BorderBrush = Brush(_palette.Border);
        foreach (var label in _mutedLabels)
        {
            label.Foreground = Brush(_palette.Secondary);
        }
        foreach (var selector in new[] { _rangeSelector, _kindSelector, _providerSelector })
        {
            selector.Background = Brush(_palette.Soft);
            selector.BorderBrush = Brush(_palette.BorderStrong);
            selector.Foreground = Brush(_palette.Primary);
        }
        // Match the app's accent and let only the moving segment show.
        _loadingBar.Foreground = Brush(_palette.Accent);
        _loadingBar.Background = Brushes.Transparent;
        _eventNote.Foreground = Brush(_palette.Secondary);
        _periodPill.Background = Brush(_palette.AccentSoft);
        _periodPill.BorderBrush = Brush(_palette.AccentBorder);
        _period.Foreground = Brush(_palette.Accent);

        _explanationCard.Background = Brush(_palette.InfoSoft);
        _explanationCard.BorderBrush = Brush(_palette.InfoBorder);
        _explanationBadge.Background = Brush(_palette.Accent);
        _explanationIcon.Foreground = Brushes.White;
        _explanationTitle.Foreground = Brush(_palette.InfoTitle);
        _explanationText.Foreground = Brush(_palette.InfoText);

        _eventListCard.Background = Brush(_palette.Surface);
        _eventListCard.BorderBrush = Brush(_palette.Border);
        _eventHeaderSurface.Background = Brush(_palette.Soft);
        _eventHeaderSurface.BorderBrush = Brush(_palette.Border);
        _eventHeading.Foreground = Brush(_palette.Primary);
        _eventCount.Foreground = Brush(_palette.Secondary);
        ApplyFilters();
        UpdateWindowClip();
    }

    private void RegisterActionHover(Button button)
    {
        button.PointerEntered += (_, _) => ApplyActionTheme(button, hovered: true);
        button.PointerExited += (_, _) => ApplyActionTheme(button, hovered: false);
    }

    private void ApplyActionTheme(Button button, bool hovered)
    {
        button.Background = Brush(hovered ? _palette.AccentHover : _palette.Accent);
        button.BorderBrush = Brush(hovered ? _palette.AccentHover : _palette.Accent);
        button.Foreground = Brushes.White;
    }

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
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateWindowShape()
    {
        var maximized = WindowState == WindowState.Maximized;
        _root.Margin = maximized ? new Thickness(0) : new Thickness(8);
        _root.CornerRadius = new CornerRadius(maximized ? 0 : 17);
        _root.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        _titleBar.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(16, 16, 0, 0);
        UpdateWindowClip();
    }

    private void UpdateWindowClip()
    {
        var radius = WindowState == WindowState.Maximized ? 0 : 17;
        _root.Clip = new RectangleGeometry(
            new Rect(0, 0, _root.Bounds.Width, _root.Bounds.Height), radius, radius);
    }

    private static void ApplyRoundedClip(Control control, double radius) =>
        control.Clip = new RectangleGeometry(
            new Rect(0, 0, control.Bounds.Width, control.Bounds.Height), radius, radius);

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        eventArgs.Handled = true;
        Close();
    }

    private void AddShadow(Border border, double blur, double offsetY)
    {
        if (!_isLightTheme)
        {
            return;
        }

        border.BoxShadow = new BoxShadows(new BoxShadow
        {
            Blur = blur,
            OffsetY = offsetY,
            Color = Color.Parse("#10000000")
        });
    }

    private static ComboBox Selector(IEnumerable<string> items, int selectedIndex, double minWidth) => new()
    {
        ItemsSource = items.ToArray(),
        SelectedIndex = selectedIndex,
        MinWidth = minWidth,
        Height = 36,
        Padding = new Thickness(10, 0),
        VerticalContentAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(9)
    };

    private static Button ActionButton(Control content, double minWidth) => new()
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

    private static Control ActionContent(string geometry, string text) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 7,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new PathIcon
            {
                Width = 14,
                Height = 14,
                Data = Geometry.Parse(geometry),
                VerticalAlignment = VerticalAlignment.Center
            },
            new TextBlock
            {
                Text = text,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            }
        }
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

    private static Control WindowIcon(string geometry) => new PathIcon
    {
        Width = 12,
        Height = 12,
        Data = Geometry.Parse(geometry)
    };

    private static string AveragePercent(IReadOnlyList<int> values) =>
        values.Count == 0 ? "—" : $"{values.Average():0.#}%";

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
        : $"{Math.Max(0, duration.Minutes)}m";

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

    private static string DisplayProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · Claude + ChatGPT",
        _ => provider
    };

    private static int ProviderOrder(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => 0,
        "codex" => 1,
        "antigravity-gemini" => 2,
        "antigravity-claudeandchatgpt" => 3,
        _ => 4
    };

    private string ProviderColor(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => _isLightTheme ? "#C65D3B" : "#F08A66",
        "codex" => _isLightTheme ? "#0F8A68" : "#4AD894",
        "antigravity-gemini" => _isLightTheme ? "#5368DC" : "#8794FF",
        "antigravity-claudeandchatgpt" => _isLightTheme ? "#8955C5" : "#C38AF0",
        _ => _palette.Secondary
    };

    // Utilization is "higher is better", matching the overlay where more
    // remaining reads as healthy. Low utilization is wasted capacity, not an
    // emergency, so it is amber rather than red.
    private string UtilizationColor(int? used) => used switch
    {
        null => _palette.Secondary,
        >= 70 => _palette.Green,
        >= 35 => _palette.Yellow,
        _ => _palette.Orange
    };

    private static SolidColorBrush Tint(string color, byte alpha)
    {
        var parsed = Color.Parse(color);
        return new SolidColorBrush(Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record EfficiencyPalette(
        string Root,
        string Surface,
        string SurfaceHover,
        string Soft,
        string HeaderStart,
        string HeaderEnd,
        string Border,
        string BorderStrong,
        string Primary,
        string Secondary,
        string Accent,
        string AccentHover,
        string AccentSoft,
        string AccentBorder,
        string InfoSoft,
        string InfoBorder,
        string InfoTitle,
        string InfoText,
        string Track,
        string Error,
        string Green,
        string Yellow,
        string Red,
        string Blue,
        string Orange,
        string Purple,
        string KindAccent)
    {
        public static EfficiencyPalette Light { get; } = new(
            "#FFF2F5F3", "#FFFFFFFF", "#FFF4F8F6", "#FFF7F9F7",
            "#FFFFFFFF", "#FFEDF8F3", "#FFDCE4DF", "#FFCAD6CE",
            "#FF1F2822", "#FF67736B", "#FF0F8A68", "#FF0B765A",
            "#FFE5F5EE", "#FFA8D8C5", "#FFEEF7F2", "#FFC8E3D5",
            "#FF176345", "#FF385447", "#FFDDE4DF", "#FFB13A32",
            "#FF0F8A5F", "#FFA98700", "#FFD84A42", "#FF5368DC",
            "#FFD46A45", "#FF8A5CD7", "#FFA95818");

        public static EfficiencyPalette Dark { get; } = new(
            "#FF151816", "#FF202421", "#FF29352F", "#FF292E2A",
            "#FF252A26", "#FF1C3028", "#FF394039", "#FF4A534C",
            "#FFF0F4F1", "#FFA9B3AB", "#FF4AD894", "#FF3CC184",
            "#FF1D392E", "#FF376D58", "#FF1C3028", "#FF315C49",
            "#FF72E0A9", "#FFC0D4C7", "#FF3A413B", "#FFFF8A80",
            "#FF4AD894", "#FFF2CF5B", "#FFFF6666", "#FF8794FF",
            "#FFFF9847", "#FFC38AF0", "#FFFFB36B");
    }
}
