using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// Explains selected history data without claiming precision beyond the local
/// refresh samples from which the estimates are derived.
/// </summary>
public sealed class UsageAnalysisWindow : Window
{
    private readonly UiText _text;
    private readonly IReadOnlyList<UsageHistoryEntry> _entries;
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;
    private readonly UsageSelectionSummary _summary;
    private readonly IReadOnlyList<UsageResetEvent> _resetEvents;
    private readonly Border _root;
    private readonly Border _titleBar;
    private readonly TextBlock _windowTitle;
    private readonly Button _minimizeButton;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private readonly Border _bodyHost;
    private AnalysisPalette _palette = AnalysisPalette.Light;
    private bool _isLightTheme = true;

    public UsageAnalysisWindow(
        IReadOnlyList<UsageHistoryEntry> entries,
        DateTimeOffset start,
        DateTimeOffset end,
        UiText text)
    {
        _text = text;
        _entries = entries;
        _start = start;
        _end = end;
        _summary = UsageHistoryAnalytics.Summarize(entries);
        _resetEvents = UsageHistoryAnalytics.FindResetEvents(entries);

        Title = L("Usage analysis", "用量分析", "用量分析");
        Width = 920;
        Height = 760;
        MinWidth = 720;
        MinHeight = 560;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _windowTitle = new TextBlock
        {
            Text = L("Usage analysis", "用量分析", "用量分析"),
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

        _bodyHost = new Border();
        var bodyScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _bodyHost
        };
        var windowLayout = new Grid
        {
            RowDefinitions = new RowDefinitions("38,*"),
            Children = { _titleBar, bodyScroll }
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
    }

    private void RebuildBody()
    {
        var ratio = _summary.Weekly.ConsumedPercent <= 0
            ? "—"
            : $"{_summary.FiveHour.ConsumedPercent / _summary.Weekly.ConsumedPercent:0.##}×";
        var period = _start.ToLocalTime().Date == _end.ToLocalTime().Date
            ? $"{_start.ToLocalTime():dddd, MMM d, yyyy}  ·  {_start.ToLocalTime():HH:mm}–{_end.ToLocalTime():HH:mm}"
            : $"{_start.ToLocalTime():MMM d, yyyy HH:mm} — {_end.ToLocalTime():MMM d, yyyy HH:mm}";

        var titleBadge = new Border
        {
            Width = 44,
            Height = 44,
            Background = Brush(_palette.AccentSoft),
            BorderBrush = Brush(_palette.AccentBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Child = new PathIcon
            {
                Width = 21,
                Height = 21,
                Foreground = Brush(_palette.Accent),
                Data = Geometry.Parse("M3 17L8.5 11.5L12.5 15.5L21 7V11H23V3H15V5H19.6L12.5 12.1L8.5 8.1L1.6 15Z")
            }
        };
        var heading = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = L("Period analysis", "期間分析", "期间分析"),
                    FontSize = 24,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.Primary)
                },
                new TextBlock { Text = period, FontSize = 11.5, Foreground = Brush(_palette.Secondary) }
            }
        };
        var headingArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { titleBadge, heading }
        };
        var overviewPill = new Border
        {
            Background = Brush(_palette.AccentSoft),
            BorderBrush = Brush(_palette.AccentBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(12, 7),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = L(
                    $"{_summary.ProviderCount} providers  ·  {_resetEvents.Count} resets",
                    $"{_summary.ProviderCount} 個來源  ·  {_resetEvents.Count} 次重置",
                    $"{_summary.ProviderCount} 个来源  ·  {_resetEvents.Count} 次重置"),
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var heroGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            Children = { headingArea, overviewPill }
        };
        Grid.SetColumn(overviewPill, 1);
        var hero = Card(heroGrid, 16, new Thickness(17, 14));
        hero.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(Color.Parse(_palette.HeaderStart), 0),
                new GradientStop(Color.Parse(_palette.HeaderEnd), 1)
            }
        };
        AddShadow(hero, 18, 4);

        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        cards.Children.Add(MetricCard(
            L("5hr : week rate", "5 小時：每週比率", "5 小时：每周比率"), ratio,
            L("Consumed quota ratio", "已用額度比率", "已用额度比率"), _palette.Blue));
        cards.Children.Add(MetricCard(
            L("Observed span", "觀察時間", "观察时间"), FormatDuration(_summary.ObservedDuration),
            L($"{_summary.RecordCount:#,##0} valid samples", $"{_summary.RecordCount:#,##0} 筆有效樣本", $"{_summary.RecordCount:#,##0} 条有效样本"),
            _palette.Green));
        cards.Children.Add(MetricCard(
            L("5-hour consumed", "5 小時用量", "5 小时用量"), FormatPercent(_summary.FiveHour.ConsumedPercent),
            L($"{_summary.FiveHour.ConsumptionPerHour:0.##}% per hour", $"每小時 {_summary.FiveHour.ConsumptionPerHour:0.##}%", $"每小时 {_summary.FiveHour.ConsumptionPerHour:0.##}%"),
            _palette.Orange));
        cards.Children.Add(MetricCard(
            L("Weekly consumed", "每週用量", "每周用量"), FormatPercent(_summary.Weekly.ConsumedPercent),
            L($"{_summary.Weekly.ConsumptionPerHour:0.##}% per hour", $"每小時 {_summary.Weekly.ConsumptionPerHour:0.##}%", $"每小时 {_summary.Weekly.ConsumptionPerHour:0.##}%"),
            _palette.Purple));

        var detailGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        detailGrid.Children.Add(WindowDetailCard(
            _summary.FiveHour, L("5-hour window", "5 小時時段", "5 小时时段"), "5 HR", _palette.Orange));
        var weeklyCard = WindowDetailCard(
            _summary.Weekly, L("Weekly window", "每週時段", "每周时段"),
            _text.UsageHistoryWeek.ToUpperInvariant(), _palette.Purple);
        Grid.SetColumn(weeklyCard, 1);
        detailGrid.Children.Add(weeklyCard);

        var providers = new StackPanel { Spacing = 7 };
        foreach (var group in _entries
                     .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(entry => entry.Provider, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => ProviderOrder(group.Key)))
        {
            providers.Children.Add(ProviderRow(group.Key, UsageHistoryAnalytics.Summarize(group.ToArray())));
        }
        if (providers.Children.Count == 0)
        {
            providers.Children.Add(EmptyState());
        }

        var disclaimer = new Border
        {
            Background = Brush(_palette.Soft),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(13, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 9,
                Children =
                {
                    new PathIcon
                    {
                        Width = 15,
                        Height = 15,
                        Foreground = Brush(_palette.Secondary),
                        VerticalAlignment = VerticalAlignment.Center,
                        Data = Geometry.Parse("M11 17H13V11H11ZM12 2C6.48 2 2 6.48 2 12S6.48 22 12 22S22 17.52 22 12S17.52 2 12 2ZM12 20C7.59 20 4 16.41 4 12S7.59 4 12 4S20 7.59 20 12S16.41 20 12 20ZM11 7H13V9H11Z")
                    },
                    new TextBlock
                    {
                        Text = L(
                            "Estimates use local observations. Activity between refreshes may not be represented.",
                            "估算依據本機觀察資料，兩次更新之間的活動可能未被記錄。",
                            "估算依据本地观察数据，两次刷新之间的活动可能未被记录。"),
                        FontSize = 10.5,
                        Foreground = Brush(_palette.Secondary),
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        if (disclaimer.Child is Grid disclaimerGrid)
        {
            Grid.SetColumn(disclaimerGrid.Children[1], 1);
        }

        _bodyHost.Child = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(20),
            Children =
            {
                hero,
                SectionHeading(
                    L("Key metrics", "主要指標", "主要指标"),
                    L("A concise view of consumption in the selected period", "所選期間的用量摘要", "所选期间的用量摘要")),
                cards,
                InsightCard(),
                SectionHeading(
                    L("Quota detail", "額度明細", "额度明细"),
                    L("Remaining quota, resets, and projected runway", "剩餘額度、重置與預估可用時間", "剩余额度、重置与预计可用时间")),
                detailGrid,
                SectionHeading(
                    L("Provider breakdown", "來源明細", "来源明细"),
                    L("Consumption separated by provider", "依來源區分的用量", "按来源区分的用量")),
                providers,
                disclaimer
            }
        };
    }

    private Border InsightCard()
    {
        var icon = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(11),
            Background = Brush(_palette.Accent),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new PathIcon
            {
                Width = 17,
                Height = 17,
                Foreground = Brushes.White,
                Data = Geometry.Parse("M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z")
            }
        };
        var text = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = L("What this period suggests", "此期間分析", "此期间分析"),
                    FontSize = 13.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.InsightTitle)
                },
                new TextBlock
                {
                    Text = BuildInterpretation(_summary, _resetEvents),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19,
                    FontSize = 11.5,
                    Foreground = Brush(_palette.InsightText)
                }
            }
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 12,
            Children = { icon, text }
        };
        Grid.SetColumn(text, 1);
        return new Border
        {
            Background = Brush(_palette.InsightSoft),
            BorderBrush = Brush(_palette.InsightBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(15, 13),
            Child = grid
        };
    }

    private string BuildInterpretation(UsageSelectionSummary summary, IReadOnlyList<UsageResetEvent> resets)
    {
        if (summary.RecordCount == 0)
        {
            return _text.UsageHistoryNoPeriodData;
        }

        var ratio = summary.Weekly.ConsumedPercent <= 0
            ? L("not available because weekly consumption did not change", "因每週用量沒有變化而無法計算", "因每周用量没有变化而无法计算")
            : L(
                $"{summary.FiveHour.ConsumedPercent / summary.Weekly.ConsumedPercent:0.##} units of 5-hour quota were observed for every unit of weekly quota",
                $"每使用 1 單位每週額度，觀察到 {summary.FiveHour.ConsumedPercent / summary.Weekly.ConsumedPercent:0.##} 單位 5 小時額度",
                $"每使用 1 单位每周额度，观察到 {summary.FiveHour.ConsumedPercent / summary.Weekly.ConsumedPercent:0.##} 单位 5 小时额度");
        var pace = summary.FiveHour.ConsumptionPerHour switch
        {
            <= 0 => L("No measurable 5-hour quota decrease", "未測得 5 小時額度下降", "未测得 5 小时额度下降"),
            < 2 => L("The observed 5-hour burn rate was light", "觀察到的 5 小時消耗速度偏低", "观察到的 5 小时消耗速度较低"),
            < 8 => L("The observed 5-hour burn rate was moderate", "觀察到的 5 小時消耗速度中等", "观察到的 5 小时消耗速度中等"),
            _ => L("The observed 5-hour burn rate was high", "觀察到的 5 小時消耗速度偏高", "观察到的 5 小时消耗速度较高")
        };
        return L(
            $"The 5hr-to-week rate is {ratio}. {pace} ({summary.FiveHour.ConsumptionPerHour:0.##}%/h). {resets.Count} reset event(s) were detected across {summary.ProviderCount} provider(s).",
            $"5 小時與每週比率：{ratio}。{pace}（{summary.FiveHour.ConsumptionPerHour:0.##}%/小時）。在 {summary.ProviderCount} 個來源中偵測到 {resets.Count} 次重置。",
            $"5 小时与每周比率：{ratio}。{pace}（{summary.FiveHour.ConsumptionPerHour:0.##}%/小时）。在 {summary.ProviderCount} 个来源中检测到 {resets.Count} 次重置。");
    }

    private Control WindowDetailCard(UsageWindowMetrics metrics, string title, string badge, string accent)
    {
        var projected = metrics.LastRemainingPercent is int remaining && metrics.ConsumptionPerHour > 0
            ? FormatDuration(TimeSpan.FromHours(remaining / metrics.ConsumptionPerHour))
            : "—";
        var badgeControl = new Border
        {
            Background = Tint(accent, _isLightTheme ? (byte)24 : (byte)38),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)95 : (byte)130),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badge,
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.4,
                Foreground = Brush(accent),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(_palette.Primary),
            VerticalAlignment = VerticalAlignment.Center
        };
        var titleGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { titleText, badgeControl }
        };
        Grid.SetColumn(badgeControl, 1);
        var averageValue = new TextBlock
        {
            Text = $"{metrics.AverageRemainingPercent:0.#}%",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        };
        var average = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = L("Average remaining", "平均剩餘", "平均剩余"),
                    FontSize = 10.5,
                    Foreground = Brush(_palette.Secondary),
                    VerticalAlignment = VerticalAlignment.Bottom
                },
                averageValue
            }
        };
        Grid.SetColumn(averageValue, 1);

        var content = new StackPanel
        {
            Spacing = 9,
            Children =
            {
                titleGrid,
                new Border { Height = 1, Background = Brush(_palette.Border) },
                average,
                Progress(metrics.AverageRemainingPercent, accent),
                DetailRow(L("Start → latest", "開始 → 最新", "开始 → 最新"), $"{Percent(metrics.FirstRemainingPercent)} → {Percent(metrics.LastRemainingPercent)}"),
                DetailRow(L("Detected resets", "偵測到重置", "检测到重置"), metrics.ResetCount.ToString(CultureInfo.CurrentCulture)),
                DetailRow(L("Projected runway", "預估可用時間", "预计可用时间"), projected)
            }
        };
        var card = Card(content, 14, new Thickness(15, 13));
        AddShadow(card, 10, 2);
        return card;
    }

    private Control ProviderRow(string provider, UsageSelectionSummary summary)
    {
        var accent = ProviderColor(provider);
        var mark = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(11),
            Background = Tint(accent, _isLightTheme ? (byte)24 : (byte)38),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)90 : (byte)125),
            BorderThickness = new Thickness(1),
            Child = new PathIcon
            {
                Width = 16,
                Height = 16,
                Foreground = Brush(accent),
                Data = Geometry.Parse("M12 2A10 10 0 1 0 12 22A10 10 0 1 0 12 2ZM12 6A6 6 0 1 1 12 18A6 6 0 1 1 12 6Z")
            }
        };
        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                mark,
                new StackPanel
                {
                    Spacing = 1,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = DisplayProvider(provider),
                            FontWeight = FontWeight.SemiBold,
                            Foreground = Brush(_palette.Primary)
                        },
                        new TextBlock
                        {
                            Text = L($"{summary.RecordCount:#,##0} samples", $"{summary.RecordCount:#,##0} 筆樣本", $"{summary.RecordCount:#,##0} 条样本"),
                            FontSize = 10,
                            Foreground = Brush(_palette.Secondary)
                        }
                    }
                }
            }
        };
        var values = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ValuePill("5 HR", FormatPercent(summary.FiveHour.ConsumedPercent), _palette.Orange),
                ValuePill(_text.UsageHistoryWeek.ToUpperInvariant(), FormatPercent(summary.Weekly.ConsumedPercent), _palette.Purple)
            }
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children = { identity, values }
        };
        Grid.SetColumn(values, 1);
        var row = Card(grid, 12, new Thickness(13, 10));
        row.PointerEntered += (_, _) => row.Background = Brush(_palette.SurfaceHover);
        row.PointerExited += (_, _) => row.Background = Brush(_palette.Surface);
        return row;
    }

    private Control ValuePill(string label, string value, string accent) => new Border
    {
        Background = Tint(accent, _isLightTheme ? (byte)20 : (byte)34),
        BorderBrush = Tint(accent, _isLightTheme ? (byte)74 : (byte)110),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(11),
        Padding = new Thickness(9, 6),
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 8.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(accent),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = value,
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.Primary),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        }
    };

    private Border MetricCard(string label, string value, string detail, string accent)
    {
        var card = Card(
            new StackPanel
            {
                Spacing = 4,
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
            }, 14, new Thickness(14, 12));
        card.Width = 198;
        card.MinHeight = 94;
        card.Margin = new Thickness(0, 0, 9, 6);
        AddShadow(card, 10, 2);
        return card;
    }

    private Control DetailRow(string label, string value)
    {
        var result = new TextBlock
        {
            Text = value,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(_palette.Primary),
            VerticalAlignment = VerticalAlignment.Center
        };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    FontSize = 10.5,
                    Foreground = Brush(_palette.Secondary),
                    VerticalAlignment = VerticalAlignment.Center
                },
                result
            }
        };
        Grid.SetColumn(result, 1);
        return grid;
    }

    private Control Progress(double value, string accent)
    {
        var fill = new Border
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(3),
            Background = Brush(accent)
        };
        var track = new Border
        {
            Height = 6,
            Background = Brush(_palette.Track),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) =>
            fill.Width = Math.Round(track.Bounds.Width * Math.Clamp(value, 0, 100) / 100d);
        return track;
    }

    private Control SectionHeading(string title, string subtitle) => new StackPanel
    {
        Spacing = 2,
        Margin = new Thickness(0, 3, 0, 0),
        Children =
        {
            new TextBlock
            {
                Text = title,
                FontSize = 15.5,
                FontWeight = FontWeight.Bold,
                Foreground = Brush(_palette.Primary)
            },
            new TextBlock { Text = subtitle, FontSize = 10.5, Foreground = Brush(_palette.Secondary) }
        }
    };

    private Control EmptyState() => new Border
    {
        Background = Brush(_palette.Soft),
        BorderBrush = Brush(_palette.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(28, 30),
        Child = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new PathIcon
                {
                    Width = 23,
                    Height = 23,
                    Foreground = Brush(_palette.Secondary),
                    Data = Geometry.Parse("M5 4H19V6H5ZM5 11H19V13H5ZM5 18H19V20H5Z")
                },
                new TextBlock
                {
                    Text = _text.UsageHistoryNoPeriodData,
                    Foreground = Brush(_palette.Secondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                }
            }
        }
    };

    private Border Card(Control child, double radius, Thickness padding) => new()
    {
        Background = Brush(_palette.Surface),
        BorderBrush = Brush(_palette.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(radius),
        Padding = padding,
        Child = child
    };

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

    private void ApplyVisualTheme()
    {
        _isLightTheme = ActualThemeVariant == ThemeVariant.Light;
        _palette = _isLightTheme ? AnalysisPalette.Light : AnalysisPalette.Dark;
        // The native surface must stay transparent so all four rounded corners
        // remain visible on Linux compositors.
        Background = Brushes.Transparent;
        _root.Background = Brush(_palette.Root);
        _root.BorderBrush = Brush(_palette.BorderStrong);
        _titleBar.Background = Brush(_palette.Surface);
        _titleBar.BorderBrush = Brush(_palette.Border);
        _windowTitle.Foreground = Brush(_palette.Primary);
        ApplyWindowControlTheme(_minimizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_maximizeButton, closeButton: false, hovered: false);
        ApplyWindowControlTheme(_closeButton, closeButton: true, hovered: false);
        RebuildBody();
        UpdateWindowClip();
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

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        eventArgs.Handled = true;
        Close();
    }

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

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

    private static string FormatDuration(TimeSpan duration) => duration.TotalDays >= 1
        ? $"{(int)duration.TotalDays}d {duration.Hours}h"
        : duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{Math.Max(0, duration.Minutes)}m";

    private static string FormatPercent(double value) => $"{value:#,##0.#}%";

    private static string Percent(int? value) => value is null ? "—" : $"{value}%";

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

    private static SolidColorBrush Tint(string color, byte alpha)
    {
        var parsed = Color.Parse(color);
        return new SolidColorBrush(Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    private sealed record AnalysisPalette(
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
        string AccentSoft,
        string AccentBorder,
        string InsightSoft,
        string InsightBorder,
        string InsightTitle,
        string InsightText,
        string Track,
        string Error,
        string Green,
        string Blue,
        string Orange,
        string Purple)
    {
        public static AnalysisPalette Light { get; } = new(
            "#FFF2F5F3", "#FFFFFFFF", "#FFF4F8F6", "#FFF7F9F7",
            "#FFFFFFFF", "#FFEDF8F3", "#FFDCE4DF", "#FFCAD6CE",
            "#FF1F2822", "#FF67736B", "#FF0F8A68", "#FFE5F5EE",
            "#FFA8D8C5", "#FFEEF7F2", "#FFC8E3D5", "#FF176345",
            "#FF385447", "#FFDDE4DF", "#FFB13A32", "#FF0F8A5F",
            "#FF5368DC", "#FFD46A45", "#FF8A5CD7");

        public static AnalysisPalette Dark { get; } = new(
            "#FF151816", "#FF202421", "#FF29352F", "#FF292E2A",
            "#FF252A26", "#FF1C3028", "#FF394039", "#FF4A534C",
            "#FFF0F4F1", "#FFA9B3AB", "#FF4AD894", "#FF1D392E",
            "#FF376D58", "#FF1C3028", "#FF315C49", "#FF72E0A9",
            "#FFC0D4C7", "#FF3A413B", "#FFFF8A80", "#FF4AD894",
            "#FF8794FF", "#FFFF9847", "#FFC38AF0");
    }
}
