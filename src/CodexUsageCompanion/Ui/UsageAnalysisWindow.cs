using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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

    public UsageAnalysisWindow(
        IReadOnlyList<UsageHistoryEntry> entries,
        DateTimeOffset start,
        DateTimeOffset end,
        UiText text)
    {
        _text = text;
        var summary = UsageHistoryAnalytics.Summarize(entries);
        var resetEvents = UsageHistoryAnalytics.FindResetEvents(entries);

        Title = L("Usage analysis", "用量分析", "用量分析");
        Width = 820;
        Height = 690;
        MinWidth = 680;
        MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var heading = new TextBlock
        {
            Text = L("Period analysis", "期間分析", "期间分析"),
            FontSize = 24,
            FontWeight = FontWeight.Bold
        };
        var period = new TextBlock
        {
            Text = $"{start.ToLocalTime():MMM d, yyyy HH:mm} — {end.ToLocalTime():MMM d, yyyy HH:mm}",
            FontSize = 12,
            Foreground = Brush("#68756D")
        };
        var close = new Button { Content = text.CloseAction, MinWidth = 88, IsCancel = true };
        close.Click += (_, _) => Close();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel { Spacing = 3, Children = { heading, period } });
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        var ratio = summary.Weekly.ConsumedPercent <= 0
            ? "—"
            : $"{summary.FiveHour.ConsumedPercent / summary.Weekly.ConsumedPercent:0.##}×";
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        cards.Children.Add(MetricCard(
            L("5hr : week rate", "5 小時：每週比率", "5 小时：每周比率"),
            ratio,
            L("Consumed quota ratio", "已用額度比率", "已用额度比率"),
            "#5B6FEF"));
        cards.Children.Add(MetricCard(
            L("Observed span", "觀察時間", "观察时间"),
            FormatDuration(summary.ObservedDuration),
            L($"{summary.RecordCount} valid samples", $"{summary.RecordCount} 筆有效樣本", $"{summary.RecordCount} 条有效样本"),
            "#0F8A5F"));
        cards.Children.Add(MetricCard(
            L("5-hour consumed", "5 小時用量", "5 小时用量"),
            $"{summary.FiveHour.ConsumedPercent:0.#}%",
            L($"{summary.FiveHour.ConsumptionPerHour:0.##}% per hour", $"每小時 {summary.FiveHour.ConsumptionPerHour:0.##}%", $"每小时 {summary.FiveHour.ConsumptionPerHour:0.##}%"),
            "#D46A45"));
        cards.Children.Add(MetricCard(
            L("Weekly consumed", "每週用量", "每周用量"),
            $"{summary.Weekly.ConsumedPercent:0.#}%",
            L($"{summary.Weekly.ConsumptionPerHour:0.##}% per hour", $"每小時 {summary.Weekly.ConsumptionPerHour:0.##}%", $"每小时 {summary.Weekly.ConsumptionPerHour:0.##}%"),
            "#8A5CD7"));

        var interpretation = new Border
        {
            Background = Brush("#EEF6F1"),
            BorderBrush = Brush("#CDE2D4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(15),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = "✦  " + L("What this period suggests", "此期間分析", "此期间分析"),
                        FontWeight = FontWeight.SemiBold,
                        Foreground = Brush("#24633F")
                    },
                    new TextBlock
                    {
                        Text = BuildInterpretation(summary, resetEvents),
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 19,
                        Foreground = Brush("#385447")
                    }
                }
            }
        };

        var detailGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12
        };
        detailGrid.Children.Add(WindowDetailCard(summary.FiveHour, "5hr", "#D46A45"));
        var weeklyCard = WindowDetailCard(summary.Weekly, text.UsageHistoryWeek, "#8A5CD7");
        Grid.SetColumn(weeklyCard, 1);
        detailGrid.Children.Add(weeklyCard);

        var providers = new StackPanel { Spacing = 6 };
        foreach (var group in entries.GroupBy(entry => entry.Provider, StringComparer.OrdinalIgnoreCase))
        {
            providers.Children.Add(ProviderRow(group.Key, UsageHistoryAnalytics.Summarize(group.ToArray())));
        }
        if (providers.Children.Count == 0)
        {
            providers.Children.Add(new Border
            {
                Padding = new Thickness(20),
                Background = Brush("#F6F8F6"),
                CornerRadius = new CornerRadius(10),
                Child = new TextBlock
                {
                    Text = _text.UsageHistoryNoPeriodData,
                    Foreground = Brush("#68756D"),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                }
            });
        }

        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                header,
                SectionLabel(L("Key metrics", "主要指標", "主要指标")),
                cards,
                interpretation,
                SectionLabel(L("Quota detail", "額度明細", "额度明细")),
                detailGrid,
                SectionLabel(L("Provider breakdown", "來源明細", "来源明细")),
                providers,
                new TextBlock
                {
                    Text = L(
                        "Estimates use observed samples and may miss activity between refreshes.",
                        "估算依據觀察樣本，可能遺漏兩次更新之間的活動。",
                        "估算依据观察样本，可能遗漏两次刷新之间的活动。"),
                    FontSize = 10,
                    Foreground = Brush("#758078"),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(24), Child = body }
        };
        AddHandler(KeyDownEvent, (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                eventArgs.Handled = true;
                Close();
            }
        }, RoutingStrategies.Tunnel);
    }

    private string BuildInterpretation(
        UsageSelectionSummary summary,
        IReadOnlyList<UsageResetEvent> resets)
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

    private Control WindowDetailCard(UsageWindowMetrics metrics, string title, string accent)
    {
        var projected = metrics.LastRemainingPercent is int remaining && metrics.ConsumptionPerHour > 0
            ? FormatDuration(TimeSpan.FromHours(remaining / metrics.ConsumptionPerHour))
            : "—";
        return new Border
        {
            Background = Brush("#F8FAF9"),
            BorderBrush = Brush("#DCE4DF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.Bold, Foreground = Brush(accent) },
                    DetailRow(L("Average remaining", "平均剩餘", "平均剩余"), $"{metrics.AverageRemainingPercent:0.#}%"),
                    DetailRow(L("Start → latest", "開始 → 最新", "开始 → 最新"), $"{Percent(metrics.FirstRemainingPercent)} → {Percent(metrics.LastRemainingPercent)}"),
                    DetailRow(L("Detected resets", "偵測到重置", "检测到重置"), metrics.ResetCount.ToString(CultureInfo.CurrentCulture)),
                    DetailRow(L("Projected at observed pace", "依觀察速度推估", "按观察速度推算"), projected)
                }
            }
        };
    }

    private Control ProviderRow(string provider, UsageSelectionSummary summary)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 18 };
        grid.Children.Add(new TextBlock { Text = DisplayProvider(provider), FontWeight = FontWeight.SemiBold });
        var five = new TextBlock { Text = $"5hr  {summary.FiveHour.ConsumedPercent:0.#}%", Foreground = Brush("#D46A45") };
        var weekly = new TextBlock { Text = $"{_text.UsageHistoryWeek}  {summary.Weekly.ConsumedPercent:0.#}%", Foreground = Brush("#8A5CD7") };
        Grid.SetColumn(five, 1);
        Grid.SetColumn(weekly, 2);
        grid.Children.Add(five);
        grid.Children.Add(weekly);
        return new Border
        {
            Background = Brush("#FCFDFC"),
            BorderBrush = Brush("#E0E6E2"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(13, 10),
            Child = grid
        };
    }

    private static Border MetricCard(string label, string value, string detail, string accent) => new()
    {
        Width = 177,
        MinHeight = 96,
        Background = Brush("#F8FAF9"),
        BorderBrush = Brush("#DCE4DF"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(11),
        Padding = new Thickness(13),
        Margin = new Thickness(0, 0, 9, 7),
        Child = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#68756D") },
                new TextBlock { Text = value, FontSize = 23, FontWeight = FontWeight.Bold, Foreground = Brush(accent) },
                new TextBlock { Text = detail, FontSize = 10, Foreground = Brush("#718078"), TextWrapping = TextWrapping.Wrap }
            }
        }
    };

    private static Grid DetailRow(string label, string value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#68756D") });
        var result = new TextBlock { Text = value, FontSize = 11, FontWeight = FontWeight.SemiBold };
        Grid.SetColumn(result, 1);
        grid.Children.Add(result);
        return grid;
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 14,
        FontWeight = FontWeight.SemiBold,
        Margin = new Thickness(0, 3, 0, 0)
    };

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

    private static string FormatDuration(TimeSpan duration) => duration.TotalDays >= 1
        ? $"{(int)duration.TotalDays}d {duration.Hours}h"
        : duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}h {duration.Minutes}m" : $"{Math.Max(0, duration.Minutes)}m";

    private static string Percent(int? value) => value is null ? "—" : $"{value}%";

    private static string DisplayProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · Claude + ChatGPT",
        _ => provider
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
