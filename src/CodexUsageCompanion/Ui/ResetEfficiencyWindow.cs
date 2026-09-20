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
        var heroGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children = { heroIdentity, _refreshButton }
        };
        Grid.SetColumn(_refreshButton, 1);
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
                _periodPill
            }
        };
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
            Text = L("How to read efficiency", "如何判讀效率", "如何解读效率"),
            FontSize = 13,
            FontWeight = FontWeight.Bold
        };
        _explanationText = new TextBlock
        {
            Text = L(
                "Lower unused quota means a cycle was used more fully. Values come from the last observation before the reset time changed; activity between refreshes may not be captured.",
                "未使用額度越低，代表該週期使用得越充分。數值取自重置時間變更前最後一次觀察；更新間隔中的活動可能未被記錄。",
                "未使用额度越低，代表该周期使用得越充分。数值取自重置时间变更前最后一次观察；刷新间隔中的活动可能未被记录。"),
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
        var eventHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { _eventHeading, _eventCount }
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
            Children = { _titleBar, body }
        };
        Grid.SetRow(body, 1);
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
        var resetEvents = UsageHistoryAnalytics.FindResetEvents(_entries, start, end, providers)
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
        UpdateSummary(resetEvents);
        UpdateEvents(resetEvents);
    }

    private void UpdateSummary(IReadOnlyList<UsageResetEvent> events)
    {
        _summary.Children.Clear();
        var fiveValues = events
            .Where(item => item.Kind is UsageResetKind.FiveHour or UsageResetKind.Both)
            .Where(item => item.FiveHourRemainingBefore is not null)
            .Select(item => item.FiveHourRemainingBefore!.Value)
            .ToArray();
        var weeklyValues = events
            .Where(item => item.Kind is UsageResetKind.Weekly or UsageResetKind.Both)
            .Where(item => item.WeeklyRemainingBefore is not null)
            .Select(item => item.WeeklyRemainingBefore!.Value)
            .ToArray();
        var combined = fiveValues.Concat(weeklyValues).ToArray();
        _summary.Children.Add(SummaryCard(
            L("Detected cycles", "偵測週期", "检测周期"),
            events.Count.ToString("N0", CultureInfo.CurrentCulture),
            L("Reset events in this view", "此檢視中的重置事件", "此视图中的重置事件"),
            _palette.Blue));
        _summary.Children.Add(SummaryCard(
            L("5hr avg unused", "5 小時平均未使用", "5 小时平均未使用"),
            AveragePercent(fiveValues),
            L("Lower is more efficient", "越低代表效率越高", "越低代表效率越高"),
            _palette.Orange));
        _summary.Children.Add(SummaryCard(
            L("Week avg unused", "每週平均未使用", "每周平均未使用"),
            AveragePercent(weeklyValues),
            L("Lower is more efficient", "越低代表效率越高", "越低代表效率越高"),
            _palette.Purple));
        _summary.Children.Add(SummaryCard(
            L("Overall utilization", "整體使用率", "整体使用率"),
            combined.Length == 0 ? "—" : $"{100 - combined.Average():0.#}%",
            L("Across observed resets", "所有觀察到的重置", "所有观察到的重置"),
            _palette.Green));
    }

    private void UpdateEvents(IReadOnlyList<UsageResetEvent> resetEvents)
    {
        _events.Children.Clear();
        var visibleCount = Math.Min(resetEvents.Count, 250);
        _eventCount.Text = resetEvents.Count > visibleCount
            ? L(
                $"{resetEvents.Count:N0} detected · newest {visibleCount:N0} shown",
                $"偵測到 {resetEvents.Count:N0} 次 · 顯示最新 {visibleCount:N0} 次",
                $"检测到 {resetEvents.Count:N0} 次 · 显示最新 {visibleCount:N0} 次")
            : L(
                $"{resetEvents.Count:N0} detected",
                $"偵測到 {resetEvents.Count:N0} 次",
                $"检测到 {resetEvents.Count:N0} 次");
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
        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { provider, kind }
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

        var quotaGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 16
        };
        quotaGrid.Children.Add(QuotaPanel(
            L("5-hour quota", "5 小時額度", "5 小时额度"),
            item.FiveHourRemainingBefore,
            _palette.Orange));
        var week = QuotaPanel(
            L("Weekly quota", "每週額度", "每周额度"),
            item.WeeklyRemainingBefore,
            _palette.Purple);
        Grid.SetColumn(week, 1);
        quotaGrid.Children.Add(week);

        var card = new Border
        {
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(14, 12),
            Child = new StackPanel { Spacing = 11, Children = { header, quotaGrid } }
        };
        card.PointerEntered += (_, _) => card.Background = Brush(_palette.SurfaceHover);
        card.PointerExited += (_, _) => card.Background = Brush(_palette.Surface);
        return card;
    }

    private Control QuotaPanel(string label, int? unused, string accent)
    {
        var used = unused is null ? (int?)null : 100 - Math.Clamp(unused.Value, 0, 100);
        var value = new TextBlock
        {
            Text = unused is null ? "—" : $"{unused}%",
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(accent),
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
                            Text = L("Unused before reset", "重置前未使用", "重置前未使用"),
                            FontSize = 9.5,
                            Foreground = Brush(_palette.Secondary)
                        }
                    }
                },
                value
            }
        };
        Grid.SetColumn(value, 1);
        var utilization = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = L("Cycle utilization", "週期使用率", "周期使用率"),
                    FontSize = 9.5,
                    Foreground = Brush(_palette.Secondary)
                },
                new TextBlock
                {
                    Text = used is null ? "—" : $"{used}%",
                    FontSize = 9.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(EfficiencyColor(unused))
                }
            }
        };
        Grid.SetColumn(utilization.Children[1], 1);
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
                Children = { heading, QuotaBar(unused, accent), utilization }
            }
        };
    }

    private Control QuotaBar(int? unused, string accent)
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
            fill.Width = Math.Round(track.Bounds.Width * Math.Clamp(unused ?? 0, 0, 100) / 100d);
        return track;
    }

    private Border SummaryCard(string label, string value, string detail, string accent)
    {
        var card = new Border
        {
            Width = 216,
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

    private string EfficiencyColor(int? unused) => unused switch
    {
        null => _palette.Secondary,
        <= 20 => _palette.Green,
        <= 45 => _palette.Yellow,
        _ => _palette.Red
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
