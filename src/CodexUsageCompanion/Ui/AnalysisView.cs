using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// The "Analysis" tab of Usage insights. It explains the selected period
/// without claiming precision beyond the local refresh samples from which the
/// estimates are derived.
/// </summary>
public sealed class AnalysisView : UserControl
{
    private readonly UiText _text;
    private IReadOnlyList<UsageHistoryEntry> _entries = [];
    private DateTimeOffset _start;
    private DateTimeOffset _end;
    private UsageSelectionSummary _summary = UsageHistoryAnalytics.Summarize([]);
    private IReadOnlyList<UsageResetEvent> _resetEvents = [];
    private readonly Border _bodyHost;
    private AnalysisPalette _palette = AnalysisPalette.Light;
    private bool _isLightTheme = true;

    public AnalysisView(UiText text)
    {
        _text = text;
        _bodyHost = new Border();
        Content = new ScrollViewer
        {
            Focusable = true,
            IsTabStop = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _bodyHost
        };
        ActualThemeVariantChanged += (_, _) => ApplyVisualTheme();
        AttachedToVisualTree += (_, _) => ApplyVisualTheme();
        ApplyVisualTheme();
    }

    /// <summary>Analyzes the successful records of the selected period.</summary>
    public void Update(
        IReadOnlyList<UsageHistoryEntry> entries,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _entries = entries;
        _start = start;
        _end = end;
        _summary = UsageHistoryAnalytics.Summarize(entries);
        _resetEvents = _summary.ResetEvents;
        RebuildBody();
    }

    private void RebuildBody()
    {
        var period = _start.ToLocalTime().Date == _end.ToLocalTime().Date
            ? $"{_text.FormatWeekdayDate(_start.ToLocalTime())}  ·  {_start.ToLocalTime():HH:mm}–{_end.ToLocalTime():HH:mm}"
            : $"{_text.FormatDate(_start.ToLocalTime())} {_start.ToLocalTime():HH:mm} — {_text.FormatDate(_end.ToLocalTime())} {_end.ToLocalTime():HH:mm}";

        var overviewPill = new Border
        {
            Background = Brush(_palette.AccentSoft),
            BorderBrush = Brush(_palette.AccentBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(11, 6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = L(
                    $"{_summary.RecordCount:N0} samples  ·  {_resetEvents.Count} resets",
                    $"{_summary.RecordCount:N0} 筆樣本  ·  {_resetEvents.Count} 次重置",
                    $"{_summary.RecordCount:N0} 条样本  ·  {_resetEvents.Count} 次重置"),
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var copyButton = new Button
        {
            Height = 31,
            Padding = new Thickness(11, 0),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.BorderStrong),
            Foreground = Brush(_palette.Primary),
            FontWeight = FontWeight.SemiBold,
            FontSize = 11,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Avalonia.Controls.Shapes.Path
                    {
                        Data = Geometry.Parse("M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"),
                        Fill = Brush(_palette.Primary),
                        Width = 11,
                        Height = 11,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = _text.UsageAnalysisCopyReport,
                        FontSize = 10.5,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
        A11y.Tip(copyButton, _text.UsageAnalysisCopyReport);
        copyButton.Click += async (_, _) => await CopyReportToClipboardAsync(copyButton);

        var heroActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { overviewPill, copyButton }
        };

        // A slim header: the period itself is already shown by the shared
        // filters above, so this only labels the report and offers "Copy".
        var heroHeading = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = L("Period analysis", "期間分析", "期间分析"),
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.Primary)
                },
                new TextBlock { Text = period, FontSize = 11.5, Foreground = Brush(_palette.Secondary) }
            }
        };
        var hero = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14,
            Children = { heroHeading, heroActions }
        };
        Grid.SetColumn(heroActions, 1);

        var metricGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 10
        };

        var quality = _summary.Quality;
        var coverageAccent = quality.CoveragePercent >= 90
            ? (StatusStyle.Good(_isLightTheme))
            : quality.CoveragePercent >= 60
                ? (StatusStyle.Caution(_isLightTheme))
                : (StatusStyle.Warn(_isLightTheme));
        var rateCard = MetricCard(
            L("Data coverage", "資料涵蓋率", "数据覆盖率"),
            $"{quality.CoveragePercent:0}%",
            quality.GapCount == 0
                ? L("Recorded without gaps", "完整記錄、無中斷", "完整记录、无中断")
                : L(
                    $"{quality.GapCount} gap(s) · {FormatDuration(_summary.UnobservedPerProvider)} unobserved",
                    $"{quality.GapCount} 處中斷 · {FormatDuration(_summary.UnobservedPerProvider)} 未記錄",
                    $"{quality.GapCount} 处中断 · {FormatDuration(_summary.UnobservedPerProvider)} 未记录"),
            coverageAccent,
            "M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");
        A11y.Tip(rateCard, L(
            "How much of the observed span is backed by samples. While the app is not running nothing is recorded, so quota can change unobserved; consumption and reset figures only reflect what was seen.",
            "觀察期間中有多少時間有取樣記錄。程式未執行時不會記錄，額度可能在這段時間內變動；用量與重置數據只反映實際觀察到的部分。",
            "观察期间中有多少时间有采样记录。程序未运行时不会记录，额度可能在这段时间内变动；用量与重置数据只反映实际观察到的部分。"));
        Grid.SetColumn(rateCard, 0);
        metricGrid.Children.Add(rateCard);

        var spanCard = MetricCard(
            L("Observed span", "觀察時間", "观察时间"),
            FormatDuration(_summary.ObservedDuration),
            L($"{_summary.RecordCount:#,##0} valid samples", $"{_summary.RecordCount:#,##0} 筆有效樣本", $"{_summary.RecordCount:#,##0} 条有效样本"),
            _palette.Green,
            "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7z");
        Grid.SetColumn(spanCard, 1);
        metricGrid.Children.Add(spanCard);

        var fiveHourCard = MetricCard(
            L("5-hour quota used", "5 小時額度用量", "5 小时额度用量"),
            FormatQuotaMultiple(_summary.FiveHour.EquivalentWindows),
            RateDetail(_summary.FiveHour),
            _palette.Orange,
            "M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67zM11.71 19c-1.78 0-3.22-1.4-3.22-3.14 0-1.62 1.05-2.76 2.81-3.12 1.77-.36 3.6-1.21 4.62-2.58.39 1.29.59 2.65.59 4.04 0 2.65-2.15 4.8-4.8 4.8z");
        A11y.Tip(fiveHourCard, ConsumptionTooltip(_summary.FiveHour, "5-hour", "5 小時", "5 小时"));
        Grid.SetColumn(fiveHourCard, 2);
        metricGrid.Children.Add(fiveHourCard);

        var weeklyMetricCard = MetricCard(
            L("Weekly quota used", "每週額度用量", "每周额度用量"),
            FormatQuotaMultiple(_summary.Weekly.EquivalentWindows),
            RateDetail(_summary.Weekly),
            _palette.Purple,
            "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z");
        A11y.Tip(weeklyMetricCard, ConsumptionTooltip(_summary.Weekly, "weekly", "每週", "每周"));
        Grid.SetColumn(weeklyMetricCard, 3);
        metricGrid.Children.Add(weeklyMetricCard);

        var detailGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        detailGrid.Children.Add(WindowDetailCard(
            UsageHistoryQuotaWindow.FiveHour,
            L("5-hour window", "5 小時時段", "5 小时时段"), _text.QuotaFiveHourLabel.ToUpperInvariant(), _palette.Orange));
        var weeklyCard = WindowDetailCard(
            UsageHistoryQuotaWindow.Weekly,
            L("Weekly window", "每週時段", "每周时段"),
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
                        Text = _summary.Quality.GapCount > 0 && _summary.Quality.CoveragePercent < 95
                            ? L(
                                $"Based on the {_summary.Quality.CoveragePercent:0}% of this period that was recorded. Activity during gaps (when the app was not running) and between refreshes is not represented.",
                                $"依據此期間中有記錄的 {_summary.Quality.CoveragePercent:0}%。程式未執行的中斷期間以及兩次更新之間的活動不會反映在內。",
                                $"依据此期间中有记录的 {_summary.Quality.CoveragePercent:0}%。程序未运行的中断期间以及两次刷新之间的活动不会反映在内。")
                            : L(
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
            Margin = new Thickness(0, 6, 0, 16),
            Children =
            {
                hero,
                SectionHeading(
                    L("Key metrics", "主要指標", "主要指标"),
                    L("A concise view of consumption in the selected period", "所選期間的用量摘要", "所选期间的用量摘要")),
                metricGrid,
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
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(11),
            Background = Brush(_palette.Accent),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 17,
                Height = 17,
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Data = Geometry.Parse("M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z")
            }
        };
        var headingText = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = L("What this period suggests", "此期間分析", "此期间分析"),
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.InsightTitle)
                },
                new TextBlock
                {
                    Text = _text.UsageAnalysisKeyTakeaways,
                    FontSize = 10.5,
                    Foreground = Brush(_palette.Secondary)
                }
            }
        };
        var topRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, headingText }
        };

        if (_summary.RecordCount == 0)
        {
            return new Border
            {
                Background = Brush(_palette.InsightSoft),
                BorderBrush = Brush(_palette.InsightBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 14),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        topRow,
                        new TextBlock
                        {
                            Text = _text.UsageHistoryNoPeriodData,
                            FontSize = 11.5,
                            Foreground = Brush(_palette.Secondary),
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
        }

        // Callout 1: Burn Rate & Pace
        var (paceLabel, paceColor, paceDesc) = GetBurnRateDetails(_summary.FiveHour.ConsumptionPerProviderHour);
        var callout1 = CreateInsightCallout(
            "M13.5.67s.74 2.65.74 4.8c0 2.06-1.35 3.73-3.41 3.73-2.07 0-3.63-1.67-3.63-3.73l.03-.36C5.21 7.51 4 10.62 4 14c0 4.42 3.58 8 8 8s8-3.58 8-8C20 8.61 17.41 3.8 13.5.67z",
            paceColor,
            _text.UsageAnalysisBurnRatePace,
            paceLabel,
            Tint(paceColor, _isLightTheme ? (byte)24 : (byte)38),
            paceColor,
            $"{_summary.FiveHour.ConsumptionPerProviderHour:0.##}% / h" +
                (_summary.ProviderCount > 1 ? " " + L("each", "每來源", "每来源") : string.Empty),
            paceDesc);

        // Callout 2: Quota Balance
        var ratio = _summary.Weekly.ConsumedPercent <= 0
            ? "—"
            : $"{_summary.FiveHour.ConsumedPercent / _summary.Weekly.ConsumedPercent:0.##}×";
        var (ratioDesc, ratioAccent) = GetQuotaBalanceDetails(_summary.FiveHour.ConsumedPercent, _summary.Weekly.ConsumedPercent, ratio);
        var callout2 = CreateInsightCallout(
            "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z",
            ratioAccent,
            _text.UsageAnalysisQuotaBalance,
            ratio,
            Tint(ratioAccent, _isLightTheme ? (byte)24 : (byte)38),
            ratioAccent,
            ratio == "—" ? "—" : $"{ratio} " + L("ratio", "比率", "比率"),
            ratioDesc);

        // Callout 3: Replenishment & Resets
        var resetAccent = _resetEvents.Count > 0 ? (StatusStyle.Caution(_isLightTheme)) : _palette.Secondary;
        var measuredResets = _resetEvents.Count(item => item.Quality == UsageResetQuality.Measured);
        var unverifiedResets = _resetEvents.Count - measuredResets;
        var resetDesc = _resetEvents.Count == 0
            ? L("No quota resets observed within this observation timeframe.", "此觀察期間內未偵測到額度重置事件。", "此观察期间内未检测到额度重置事件。")
            : L(
                $"{_resetEvents.Count} reset(s) across {_summary.ProviderCount} provider(s): {measuredResets} measured, {unverifiedResets} unverified (not sampled at the time, or never used).",
                $"在 {_summary.ProviderCount} 個來源中偵測到 {_resetEvents.Count} 次重置：{measuredResets} 次已量測、{unverifiedResets} 次未驗證（當時未取樣或從未使用）。",
                $"在 {_summary.ProviderCount} 个来源中检测到 {_resetEvents.Count} 次重置：{measuredResets} 次已测量、{unverifiedResets} 次未验证（当时未采样或从未使用）。");
        var callout3 = CreateInsightCallout(
            "M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46A7.93 7.93 0 0 0 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74A7.93 7.93 0 0 0 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z",
            resetAccent,
            _text.UsageAnalysisResetRecovery,
            $"{_resetEvents.Count} " + L("resets", "次重置", "次重置"),
            Tint(resetAccent, _isLightTheme ? (byte)24 : (byte)38),
            resetAccent,
            $"{_resetEvents.Count} " + L("events", "次事件", "次事件"),
            resetDesc);

        var calloutsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 10,
            Children = { callout1, callout2, callout3 }
        };
        Grid.SetColumn(callout2, 1);
        Grid.SetColumn(callout3, 2);

        return new Border
        {
            Background = Brush(_palette.InsightSoft),
            BorderBrush = Brush(_palette.InsightBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16, 14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children = { topRow, calloutsGrid }
            }
        };
    }

    private Border CreateInsightCallout(
        string iconGeometry,
        string accentColor,
        string categoryTitle,
        string badgeText,
        IBrush badgeBg,
        string badgeFg,
        string mainValue,
        string description)
    {
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(iconGeometry),
            Fill = Brush(accentColor),
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconContainer = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(7),
            Background = Tint(accentColor, _isLightTheme ? (byte)26 : (byte)42),
            BorderBrush = Tint(accentColor, _isLightTheme ? (byte)90 : (byte)130),
            BorderThickness = new Thickness(1),
            Child = icon
        };
        var catText = new TextBlock
        {
            Text = categoryTitle,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(_palette.Primary),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var badge = new Border
        {
            Background = badgeBg,
            BorderBrush = Tint(badgeFg, _isLightTheme ? (byte)80 : (byte)120),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = Brush(badgeFg),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 7
        };
        var leftStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { iconContainer, catText }
        };
        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(badge, 2);
        header.Children.Add(leftStack);
        header.Children.Add(badge);

        var valueText = new TextBlock
        {
            Text = mainValue,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(accentColor),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var descText = new TextBlock
        {
            Text = description,
            FontSize = 10.5,
            LineHeight = 15,
            Foreground = Brush(_palette.InsightText),
            TextWrapping = TextWrapping.Wrap
        };

        var cardContent = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                header,
                valueText,
                descText
            }
        };

        var calloutBorder = new Border
        {
            Background = Brush(_palette.Surface),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(12, 10),
            Child = cardContent
        };
        if (_isLightTheme)
        {
            calloutBorder.BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 6,
                OffsetY = 1,
                Color = Color.Parse("#08000000")
            });
        }
        return calloutBorder;
    }

    private (string Label, string Color, string Description) GetBurnRateDetails(double rate)
    {
        if (rate <= 0)
        {
            return (
                _text.UsageHistoryBurnRateIdle,
                StatusStyle.Neutral(_isLightTheme),
                L("No measurable 5-hour quota decrease in this observation span.", "在此觀察期間未測得 5 小時額度下降。", "在此观察期间未测得 5 小时额度下降。"));
        }
        if (rate < 2.0)
        {
            return (
                _text.UsageHistoryBurnRateLight,
                StatusStyle.Good(_isLightTheme),
                L("Light burn rate; quota is conserved and sustainable for prolonged sessions.", "用量消耗偏低；額度節省且適合長時間持續開發。", "用量消耗较低；额度节省且适合长时间持续开发。"));
        }
        if (rate < 8.0)
        {
            return (
                _text.UsageHistoryBurnRateModerate,
                StatusStyle.Caution(_isLightTheme),
                L("Moderate steady pace; active development with balanced quota usage.", "穩健中等的消耗速度；處於活躍開發且額度使用均衡。", "稳健中等的消耗速度；处于活跃开发且额度使用均衡。"));
        }
        return (
            _text.UsageHistoryBurnRateHigh,
            StatusStyle.Critical(_isLightTheme),
            L("High quota consumption velocity; consider pacing heavy prompt executions.", "額度消耗速度偏高；建議留意高負載呼叫以防過早耗盡。", "额度消耗速度较高；建议留意高负载调用以防过早耗尽。"));
    }

    private (string Description, string Color) GetQuotaBalanceDetails(double fiveHourConsumed, double weeklyConsumed, string ratio)
    {
        var accent = _palette.Blue;
        if (weeklyConsumed <= 0)
        {
            return (
                L("Weekly quota usage did not change during this window.", "此期間每週用量無明顯變化。", "此期间每周用量无明显变化。"),
                accent);
        }
        var desc = L(
            $"{ratio} of 5-hour quota observed per unit of weekly quota.",
            $"每使用 1 單位每週額度，觀察到 {ratio} 單位 5 小時額度。",
            $"每使用 1 单位每周额度，观察到 {ratio} 单位 5 小时额度。");
        return (desc, accent);
    }

    private async Task CopyReportToClipboardAsync(Button button)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        var report = BuildMarkdownReport();
        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateText(report));
        await topLevel.Clipboard.SetDataAsync(dataTransfer);

        var originalContent = button.Content;
        button.Background = Brush(_palette.AccentSoft);
        button.BorderBrush = Brush(_palette.AccentBorder);
        button.Foreground = Brush(_palette.Accent);
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Avalonia.Controls.Shapes.Path
                {
                    Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"),
                    Fill = Brush(_palette.Accent),
                    Width = 11,
                    Height = 11,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = _text.UsageAnalysisReportCopied,
                    FontSize = 10.5,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(_palette.Accent),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = originalContent;
            button.Background = Brush(_palette.Surface);
            button.BorderBrush = Brush(_palette.BorderStrong);
            button.Foreground = Brush(_palette.Primary);
        };
        timer.Start();
    }

    private string BuildMarkdownReport()
    {
        var period = _start.ToLocalTime().Date == _end.ToLocalTime().Date
            ? $"{_text.FormatWeekdayDate(_start.ToLocalTime())} · {_start.ToLocalTime():HH:mm}–{_end.ToLocalTime():HH:mm}"
            : $"{_text.FormatDate(_start.ToLocalTime())} {_start.ToLocalTime():HH:mm} — {_text.FormatDate(_end.ToLocalTime())} {_end.ToLocalTime():HH:mm}";

        var ratio = _summary.Weekly.ConsumedPercent <= 0
            ? "—"
            : $"{_summary.FiveHour.ConsumedPercent / _summary.Weekly.ConsumedPercent:0.##}×";

        var providers = string.Join(", ", _entries
            .Where(e => string.Equals(e.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Select(e => DisplayProvider(e.Provider))
            .Distinct(StringComparer.OrdinalIgnoreCase));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Usage Analysis Report");
        sb.AppendLine($"**Period:** {period}");
        sb.AppendLine($"**Observed Span:** {FormatDuration(_summary.ObservedDuration)} ({_summary.RecordCount:#,##0} valid samples)");
        if (!string.IsNullOrWhiteSpace(providers))
        {
            sb.AppendLine($"**Active Providers:** {providers}");
        }
        sb.AppendLine();
        sb.AppendLine($"## Key Metrics");
        sb.AppendLine($"- **Data Coverage:** {_summary.Quality.CoveragePercent:0}% ({_summary.Quality.GapCount} gap(s), {FormatDuration(_summary.UnobservedPerProvider)} unobserved per provider)");
        sb.AppendLine($"- **5-Hour Quota Used:** {FormatQuotaMultiple(_summary.FiveHour.EquivalentWindows)} full quotas ({FormatPercent(_summary.FiveHour.ConsumedPercent)} points across {_summary.ProviderCount} provider(s); {_summary.FiveHour.ConsumptionPerProviderHour:0.##}%/h each)");
        sb.AppendLine($"- **Weekly Quota Used:** {FormatQuotaMultiple(_summary.Weekly.EquivalentWindows)} full quotas ({FormatPercent(_summary.Weekly.ConsumedPercent)} points across {_summary.ProviderCount} provider(s); {_summary.Weekly.ConsumptionPerProviderHour:0.##}%/h each)");
        sb.AppendLine($"- **5hr : Week Ratio:** {ratio}");
        sb.AppendLine($"- **Detected Resets:** {_resetEvents.Count} ({_resetEvents.Count(item => item.Quality == UsageResetQuality.Measured)} measured)");
        sb.AppendLine();
        sb.AppendLine($"## Analytical Insights");
        sb.AppendLine($"- **Burn Rate & Pace:** {GetBurnRatePaceText()} ({_summary.FiveHour.ConsumptionPerProviderHour:0.##}%/h per provider)");
        sb.AppendLine($"- **Quota Balance:** {GetQuotaBalanceText()}");
        sb.AppendLine($"- **Replenishment Activity:** {_resetEvents.Count} reset event(s) detected across {_summary.ProviderCount} provider(s).");
        sb.AppendLine();
        sb.AppendLine($"## Quota Runway Projections");
        sb.AppendLine($"- **5-Hour Window:** Average remaining: {_summary.FiveHour.AverageRemainingPercent:0.#}% · Soonest runway: {RunwayText(UsageHistoryQuotaWindow.FiveHour)}");
        sb.AppendLine($"- **Weekly Window:** Average remaining: {_summary.Weekly.AverageRemainingPercent:0.#}% · Soonest runway: {RunwayText(UsageHistoryQuotaWindow.Weekly)}");
        sb.AppendLine();
        sb.AppendLine($"## By Provider");
        foreach (var provider in _summary.Providers)
        {
            sb.AppendLine($"- **{DisplayProvider(provider.Provider)}:** 5-hour {FormatQuotaMultiple(provider.FiveHour.EquivalentWindows)} ({provider.FiveHour.ConsumptionPerHour:0.##}%/h), weekly {FormatQuotaMultiple(provider.Weekly.EquivalentWindows)}; {provider.Quality.CoveragePercent:0}% covered");
        }

        return sb.ToString();
    }

    private string GetBurnRatePaceText()
    {
        var rate = _summary.FiveHour.ConsumptionPerProviderHour;
        if (rate <= 0) return _text.UsageHistoryBurnRateIdle;
        if (rate < 2.0) return _text.UsageHistoryBurnRateLight;
        if (rate < 8.0) return _text.UsageHistoryBurnRateModerate;
        return _text.UsageHistoryBurnRateHigh;
    }

    private string GetQuotaBalanceText()
    {
        if (_summary.Weekly.ConsumedPercent <= 0)
        {
            return "—";
        }
        var ratio = _summary.FiveHour.ConsumedPercent / _summary.Weekly.ConsumedPercent;
        return $"{ratio:0.##} units 5-hour per weekly unit";
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
        var pace = summary.FiveHour.ConsumptionPerProviderHour switch
        {
            <= 0 => L("No measurable 5-hour quota decrease", "未測得 5 小時額度下降", "未测得 5 小时额度下降"),
            < 2 => L("The observed 5-hour burn rate was light", "觀察到的 5 小時消耗速度偏低", "观察到的 5 小时消耗速度较低"),
            < 8 => L("The observed 5-hour burn rate was moderate", "觀察到的 5 小時消耗速度中等", "观察到的 5 小时消耗速度中等"),
            _ => L("The observed 5-hour burn rate was high", "觀察到的 5 小時消耗速度偏高", "观察到的 5 小时消耗速度较高")
        };
        return L(
            $"The 5hr-to-week rate is {ratio}. {pace} ({summary.FiveHour.ConsumptionPerProviderHour:0.##}%/h per provider). {resets.Count} reset event(s) were detected across {summary.ProviderCount} provider(s).",
            $"5 小時與每週比率：{ratio}。{pace}（每來源 {summary.FiveHour.ConsumptionPerProviderHour:0.##}%/小時）。在 {summary.ProviderCount} 個來源中偵測到 {resets.Count} 次重置。",
            $"5 小时与每周比率：{ratio}。{pace}（每来源 {summary.FiveHour.ConsumptionPerProviderHour:0.##}%/小时）。在 {summary.ProviderCount} 个来源中检测到 {resets.Count} 次重置。");
    }

    private Control WindowDetailCard(UsageHistoryQuotaWindow window, string title, string badge, string accent)
    {
        var metrics = _summary.For(window);
        var multiProvider = _summary.ProviderCount > 1;
        // The runway that matters is the first one to run out, judged against
        // each provider's own remaining quota and burn - not one provider's
        // remaining divided by everyone's combined consumption.
        var soonest = _summary.SoonestRunway(window);
        var runwayHours = soonest?.Hours;

        string projectedText = runwayHours is not null
            ? FormatDuration(TimeSpan.FromHours(runwayHours.Value)) +
              (multiProvider ? $" · {DisplayProvider(soonest!.Provider)}" : string.Empty)
            : (metrics.ConsumptionPerHour <= 0 && metrics.LastRemainingPercent is not null && metrics.LastRemainingPercent > 0
                ? _text.UsageAnalysisIndefinite
                : "—");

        (string runwayStatusText, string runwayStatusColor) = runwayHours switch
        {
            null when metrics.LastRemainingPercent == 0 =>
                (_text.UsageAnalysisRunwayExhausted, _palette.Error),
            null when metrics.ConsumptionPerHour <= 0 && metrics.LastRemainingPercent > 0 =>
                (_text.UsageAnalysisRunwayStable, StatusStyle.Good(_isLightTheme)),
            null =>
                (_text.UsageAnalysisRunwayNoData, _palette.Secondary),
            >= 12 =>
                (_text.UsageAnalysisRunwaySafe, StatusStyle.Good(_isLightTheme)),
            >= 4 =>
                (_text.UsageAnalysisRunwayModerate, StatusStyle.Caution(_isLightTheme)),
            _ =>
                (_text.UsageAnalysisRunwayUrgent, _palette.Error)
        };

        var runwayStatusBadge = new Border
        {
            Background = Tint(runwayStatusColor, _isLightTheme ? (byte)22 : (byte)36),
            BorderBrush = Tint(runwayStatusColor, _isLightTheme ? (byte)70 : (byte)110),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Border
                    {
                        Width = 5,
                        Height = 5,
                        CornerRadius = new CornerRadius(2.5),
                        Background = Brush(runwayStatusColor),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = runwayStatusText,
                        FontSize = 10,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brush(runwayStatusColor),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };

        var runwayValuePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = projectedText,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(_palette.Primary),
                    VerticalAlignment = VerticalAlignment.Center
                },
                runwayStatusBadge
            }
        };

        int? first = metrics.FirstRemainingPercent;
        int? last = metrics.LastRemainingPercent;
        string rangeText = $"{Percent(first)} → {Percent(last)}";

        Control startLatestValue;
        if (first is not null && last is not null)
        {
            int delta = last.Value - first.Value;
            string deltaText;
            string deltaColor;

            if (delta < 0)
            {
                deltaText = $"↓ {Math.Abs(delta)}% {_text.UsageAnalysisDeltaUsed}";
                deltaColor = Math.Abs(delta) >= 20 ? _palette.Error : (StatusStyle.Caution(_isLightTheme));
            }
            else if (delta > 0)
            {
                deltaText = $"↑ +{delta}% {_text.UsageAnalysisDeltaRestored}";
                deltaColor = StatusStyle.Good(_isLightTheme);
            }
            else
            {
                deltaText = $"— {_text.UsageAnalysisDeltaUnchanged}";
                deltaColor = _palette.Secondary;
            }

            var deltaBadge = new Border
            {
                Background = Tint(deltaColor, _isLightTheme ? (byte)20 : (byte)32),
                BorderBrush = Tint(deltaColor, _isLightTheme ? (byte)60 : (byte)95),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = deltaText,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(deltaColor),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            startLatestValue = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = rangeText,
                        FontSize = 11,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brush(_palette.Primary),
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    deltaBadge
                }
            };
        }
        else
        {
            startLatestValue = new TextBlock
            {
                Text = rangeText,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(_palette.Primary),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var resetsValue = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = metrics.ResetCount.ToString(CultureInfo.CurrentCulture),
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush(metrics.ResetCount > 0 ? accent : _palette.Primary),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        if (metrics.ResetCount > 0)
        {
            resetsValue.Children.Add(new Border
            {
                Background = Tint(accent, _isLightTheme ? (byte)20 : (byte)32),
                BorderBrush = Tint(accent, _isLightTheme ? (byte)60 : (byte)95),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 1.5),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = _text.UsageAnalysisCycleReset,
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(accent),
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
        }

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
                FontSize = 10,
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
                multiProvider
                    ? DetailRow(L("Lowest now", "目前最低", "当前最低"), LowestNowValue(window))
                    : DetailRow(L("Start → latest", "開始 → 最新", "开始 → 最新"), startLatestValue),
                DetailRow(L("Detected resets", "偵測到重置", "检测到重置"), resetsValue),
                DetailRow(L("Projected runway", "預估可用時間", "预计可用时间"), runwayValuePanel)
            }
        };
        var card = Card(content, 14, new Thickness(15, 13));
        AddShadow(card, 10, 2);
        return card;
    }

    // Different providers' first and last samples are unrelated numbers, so a
    // combined "start -> latest" says nothing. What does matter across them is
    // which one has the least left right now.
    private Control LowestNowValue(UsageHistoryQuotaWindow window)
    {
        var lowest = _summary.Providers
            .Where(provider => provider.For(window).LastRemainingPercent is not null)
            .OrderBy(provider => provider.For(window).LastRemainingPercent)
            .FirstOrDefault();
        if (lowest is null)
        {
            return new TextBlock
            {
                Text = "—",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(_palette.Primary)
            };
        }

        var remaining = lowest.For(window).LastRemainingPercent!.Value;
        var color = remaining < 20
            ? _palette.Error
            : remaining < 50 ? (StatusStyle.Caution(_isLightTheme)) : (StatusStyle.Good(_isLightTheme));
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = $"{remaining}%",
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = Brush(color),
                    VerticalAlignment = VerticalAlignment.Center
                },
                new TextBlock
                {
                    Text = DisplayProvider(lowest.Provider),
                    FontSize = 10.5,
                    Foreground = Brush(_palette.Secondary),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };
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
            Child = new Avalonia.Controls.Shapes.Path
            {
                Width = 15,
                Height = 15,
                Fill = Brush(accent),
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Data = Geometry.Parse(ProviderIconGeometry(provider))
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
                            Text = L(
                                $"{summary.RecordCount:#,##0} samples · {summary.Quality.CoveragePercent:0}% covered",
                                $"{summary.RecordCount:#,##0} 筆樣本 · 涵蓋 {summary.Quality.CoveragePercent:0}%",
                                $"{summary.RecordCount:#,##0} 条样本 · 覆盖 {summary.Quality.CoveragePercent:0}%"),
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
                ValuePill(_text.QuotaFiveHourLabel.ToUpperInvariant(), FormatQuotaMultiple(summary.FiveHour.EquivalentWindows), _palette.Orange),
                ValuePill(_text.UsageHistoryWeek.ToUpperInvariant(), FormatQuotaMultiple(summary.Weekly.EquivalentWindows), _palette.Purple)
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
                    FontSize = 10,
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

    private Border MetricCard(string label, string value, string detail, string accent, string iconGeometry)
    {
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
            Width = 13,
            Height = 13,
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
            Background = Tint(accent, _isLightTheme ? (byte)24 : (byte)38),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)80 : (byte)120),
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

        var valueText = new TextBlock
        {
            Text = value,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brush(_palette.Primary),
            Margin = new Thickness(0, 2, 0, 4),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var detailDot = new Border
        {
            Width = 5,
            Height = 5,
            CornerRadius = new CornerRadius(2.5),
            Background = Brush(accent),
            VerticalAlignment = VerticalAlignment.Center
        };

        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 10,
            FontWeight = FontWeight.Medium,
            Foreground = Brush(_palette.Secondary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        var detailPill = new Border
        {
            Background = Tint(accent, _isLightTheme ? (byte)16 : (byte)26),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)50 : (byte)75),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 2.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { detailDot, detailText }
            }
        };

        var card = Card(
            new StackPanel
            {
                Spacing = 2,
                Children = { headerGrid, valueText, detailPill }
            }, 14, new Thickness(14, 12));

        card.MinHeight = 100;
        AddShadow(card, 10, 2);

        card.PointerEntered += (_, _) =>
        {
            card.Background = Brush(_isLightTheme ? "#FFFFFFFF" : _palette.SurfaceHover);
            card.BorderBrush = Tint(accent, _isLightTheme ? (byte)130 : (byte)170);
            if (_isLightTheme)
            {
                card.BoxShadow = new BoxShadows(new BoxShadow
                {
                    Blur = 14,
                    OffsetY = 3,
                    Color = Color.Parse("#14000000")
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
                    Blur = 10,
                    OffsetY = 2,
                    Color = Color.Parse("#08000000")
                });
            }
        };

        A11y.Name(card, $"{label}: {value}, {detail}");
        return card;
    }

    private Control DetailRow(string label, Control content)
    {
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
                content
            }
        };
        Grid.SetColumn(content, 1);
        return grid;
    }

    private Control DetailRow(string label, string value) => DetailRow(label, new TextBlock
    {
        Text = value,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brush(_palette.Primary),
        VerticalAlignment = VerticalAlignment.Center
    });

    private Control Progress(double value, string accent)
    {
        var fill = new Border
        {
            Height = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4),
            Background = Brush(accent)
        };
        var track = new Border
        {
            Height = 8,
            Background = Brush(_palette.Track),
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) =>
            fill.Width = Math.Round(track.Bounds.Width * Math.Clamp(value, 0, 100) / 100d);

        var scaleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            Margin = new Thickness(1, 2, 1, 0),
            Children =
            {
                new TextBlock
                {
                    Text = "0%",
                    FontSize = 10,
                    Foreground = Brush(_palette.Secondary),
                    HorizontalAlignment = HorizontalAlignment.Left
                },
                new TextBlock
                {
                    Text = "100%",
                    FontSize = 10,
                    Foreground = Brush(_palette.Secondary),
                    HorizontalAlignment = HorizontalAlignment.Right
                }
            }
        };
        Grid.SetColumn(scaleRow.Children[1], 1);

        return new StackPanel
        {
            Spacing = 2,
            Children = { track, scaleRow }
        };
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
        RebuildBody();
    }

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

    private string FormatDuration(TimeSpan duration) => _text.FormatDuration(duration);

    private static string FormatPercent(double value) => $"{value:#,##0.#}%";

    private static string FormatQuotaMultiple(double windows) =>
        windows >= 100 ? $"{windows:#,##0}×" : $"{windows:0.#}×";

    // The rate is the average per provider so it does not scale with how many
    // are tracked; with several providers the card says so.
    private string RateDetail(UsageWindowMetrics metrics) => metrics.ProviderCount > 1
        ? L(
            $"{metrics.ConsumptionPerProviderHour:0.##}%/h per provider",
            $"每來源每小時 {metrics.ConsumptionPerProviderHour:0.##}%",
            $"每来源每小时 {metrics.ConsumptionPerProviderHour:0.##}%")
        : L(
            $"{metrics.ConsumptionPerProviderHour:0.##}% per hour",
            $"每小時 {metrics.ConsumptionPerProviderHour:0.##}%",
            $"每小时 {metrics.ConsumptionPerProviderHour:0.##}%");

    private string ConsumptionTooltip(UsageWindowMetrics metrics, string english, string traditional, string simplified)
    {
        var points = metrics.ConsumedPercent.ToString("#,##0", CultureInfo.CurrentCulture);
        return L(
            $"{metrics.EquivalentWindows:0.#} full {english} quotas used: {points} percentage points added up across {metrics.ProviderCount} provider(s).",
            $"共使用 {metrics.EquivalentWindows:0.#} 個完整{traditional}額度：{metrics.ProviderCount} 個來源合計 {points} 個百分點。",
            $"共使用 {metrics.EquivalentWindows:0.#} 个完整{simplified}额度：{metrics.ProviderCount} 个来源合计 {points} 个百分点。");
    }

    private string RunwayText(UsageHistoryQuotaWindow window)
    {
        var soonest = _summary.SoonestRunway(window);
        return soonest is null
            ? "—"
            : $"{FormatDuration(TimeSpan.FromHours(soonest.Hours))} ({DisplayProvider(soonest.Provider)})";
    }

    private static string Percent(int? value) => value is null ? "—" : $"{value}%";

    private static string DisplayProvider(string provider) => ProviderStyle.DisplayName(provider);

    private static int ProviderOrder(string provider) => ProviderStyle.Order(provider);

    private static string ProviderIconGeometry(string provider) => ProviderStyle.IconGeometry(provider);

    private string ProviderColor(string provider) => ProviderStyle.Accent(provider, _isLightTheme, _palette.Secondary);

    private static SolidColorBrush Tint(string color, byte alpha)
    {
        var parsed = Color.Parse(color);
        return new SolidColorBrush(Color.FromArgb(alpha, parsed.R, parsed.G, parsed.B));
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    internal sealed record AnalysisPalette(
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
            "#FF1F2822", "#FF67736B", "#FF0B7A5A", "#FFE5F5EE",
            "#FFA8D8C5", "#FFEEF7F2", "#FFC8E3D5", "#FF176345",
            "#FF385447", "#FFDDE4DF", "#FFB13A32", "#FF0F8A5F",
            "#FF5368DC", CadenceStyle.FiveHourArgb(true), CadenceStyle.WeeklyArgb(true));

        public static AnalysisPalette Dark { get; } = new(
            "#FF151816", "#FF202421", "#FF29352F", "#FF292E2A",
            "#FF252A26", "#FF1C3028", "#FF394039", "#FF4A534C",
            "#FFF0F4F1", "#FFA9B3AB", "#FF4AD894", "#FF1D392E",
            "#FF376D58", "#FF1C3028", "#FF315C49", "#FF72E0A9",
            "#FFC0D4C7", "#FF3A413B", "#FFFF8A80", "#FF4AD894",
            "#FF8794FF", CadenceStyle.FiveHourArgb(false), CadenceStyle.WeeklyArgb(false));
    }
}
