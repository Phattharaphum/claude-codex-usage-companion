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
    private readonly StackPanel _events = new() { Spacing = 7 };
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _period = new();
    private readonly TextBlock _eventCount = new();
    private IReadOnlyList<UsageHistoryEntry> _entries = [];
    private IReadOnlyList<string> _providerKeys = [];

    public ResetEfficiencyWindow(
        CompanionSettings settings,
        UiText text,
        UsageHistoryReader? reader = null)
    {
        _settings = settings;
        _text = text;
        _reader = reader ?? new UsageHistoryReader();
        _rangeSelector = new ComboBox
        {
            ItemsSource = new[] { L("7 days", "7 天", "7 天"), L("30 days", "30 天", "30 天"), L("90 days", "90 天", "90 天"), text.UsageHistoryAll },
            SelectedIndex = 1,
            MinWidth = 105
        };
        _kindSelector = new ComboBox
        {
            ItemsSource = new[] { L("All resets", "所有重置", "所有重置"), "5hr", text.UsageHistoryWeek },
            SelectedIndex = 0,
            MinWidth = 115
        };
        _providerSelector = new ComboBox
        {
            ItemsSource = new[] { L("All providers", "所有來源", "所有来源") },
            SelectedIndex = 0,
            MinWidth = 145
        };
        _rangeSelector.SelectionChanged += (_, _) => ApplyFilters();
        _kindSelector.SelectionChanged += (_, _) => ApplyFilters();
        _providerSelector.SelectionChanged += (_, _) => ApplyFilters();

        Title = text.ResetEfficiencyTitle;
        Width = 940;
        Height = 760;
        MinWidth = 760;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;

        var title = new TextBlock
        {
            Text = L("Reset efficiency", "重置效率", "重置效率"),
            FontSize = 25,
            FontWeight = FontWeight.Bold
        };
        _subtitle.Text = L(
            "How much quota was still available just before each reset",
            "查看每次重置前仍有多少額度未使用",
            "查看每次重置前仍有多少额度未使用");
        _subtitle.FontSize = 12;
        _subtitle.Foreground = Brush("#68756D");
        var refresh = new Button { Content = "↻  " + text.RefreshAction, MinWidth = 112 };
        refresh.Click += (_, _) => Reload();
        var close = new Button { Content = text.CloseAction, MinWidth = 88, IsCancel = true };
        close.Click += (_, _) => Close();
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { refresh, close } };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel { Spacing = 3, Children = { title, _subtitle } });
        Grid.SetColumn(headerActions, 1);
        header.Children.Add(headerActions);

        _period.FontSize = 12;
        _period.FontWeight = FontWeight.SemiBold;
        _period.VerticalAlignment = VerticalAlignment.Center;
        var filters = new Border
        {
            Background = Brush("#F5F8F6"),
            BorderBrush = Brush("#DCE4DF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 10),
            Child = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    FilterGroup(text.UsageHistoryTimeRange, _rangeSelector),
                    FilterGroup(L("Reset type", "重置類型", "重置类型"), _kindSelector),
                    FilterGroup(text.UsageHistoryProvider, _providerSelector),
                    _period
                }
            }
        };

        var explanation = new Border
        {
            Background = Brush("#EEF6F1"),
            BorderBrush = Brush("#CDE2D4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 11),
            Child = new TextBlock
            {
                Text = L(
                    "A lower unused percentage means the quota cycle was used more fully. A changed reset time is the primary signal, confirmed by quota recovery or crossing the scheduled reset; values come from the last sample before that change.",
                    "未使用百分比越低，代表該額度週期使用得越充分。重置時間變更是主要訊號，並以額度回升或跨過預定重置時間確認；數值取自變更前最後一筆樣本。",
                    "未使用百分比越低，代表该额度周期使用得越充分。重置时间变更是主要信号，并以额度回升或跨过预定重置时间确认；数值取自变更前最后一条样本。"),
                Foreground = Brush("#385447"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            }
        };

        _eventCount.FontSize = 11;
        _eventCount.Foreground = Brush("#68756D");
        var eventHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        eventHeader.Children.Add(new TextBlock
        {
            Text = L("Reset cycles", "重置週期", "重置周期"),
            FontSize = 15,
            FontWeight = FontWeight.SemiBold
        });
        Grid.SetColumn(_eventCount, 1);
        eventHeader.Children.Add(_eventCount);
        var eventScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _events
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(24),
            RowSpacing = 13
        };
        AddRow(layout, header, 0);
        AddRow(layout, filters, 1);
        AddRow(layout, explanation, 2);
        AddRow(layout, _summary, 3);
        AddRow(layout, eventHeader, 4);
        AddRow(layout, eventScroll, 5);
        Content = layout;
        AddHandler(KeyDownEvent, (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                eventArgs.Handled = true;
                Close();
            }
        }, RoutingStrategies.Tunnel);
        Reload();
    }

    public void Reload()
    {
        var result = _reader.Read(_settings.UsageLogFilePath, _settings.UsageLogFormat);
        _entries = result.Entries;
        var selectedProvider = _providerSelector.SelectedIndex > 0 &&
            _providerSelector.SelectedIndex - 1 < _providerKeys.Count
                ? _providerKeys[_providerSelector.SelectedIndex - 1]
                : null;
        _providerKeys = _entries
            .Where(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Provider)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(provider => provider, StringComparer.OrdinalIgnoreCase)
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
            "How much quota was still available just before each reset",
            "查看每次重置前仍有多少額度未使用",
            "查看每次重置前仍有多少额度未使用");
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
            events.Count.ToString(CultureInfo.CurrentCulture),
            L("Reset events", "重置事件", "重置事件"),
            "#5B6FEF"));
        _summary.Children.Add(SummaryCard(
            L("5hr avg unused", "5 小時平均未使用", "5 小时平均未使用"),
            AveragePercent(fiveValues),
            L("Before 5hr resets", "5 小時重置前", "5 小时重置前"),
            "#D46A45"));
        _summary.Children.Add(SummaryCard(
            L("Week avg unused", "每週平均未使用", "每周平均未使用"),
            AveragePercent(weeklyValues),
            L("Before weekly resets", "每週重置前", "每周重置前"),
            "#8A5CD7"));
        _summary.Children.Add(SummaryCard(
            L("Overall utilization", "整體使用率", "整体使用率"),
            combined.Length == 0 ? "—" : $"{100 - combined.Average():0.#}%",
            L("100% − average unused", "100% − 平均未使用", "100% − 平均未使用"),
            "#0F8A5F"));
    }

    private void UpdateEvents(IReadOnlyList<UsageResetEvent> resetEvents)
    {
        _events.Children.Clear();
        _eventCount.Text = L(
            $"{resetEvents.Count} detected",
            $"偵測到 {resetEvents.Count} 次",
            $"检测到 {resetEvents.Count} 次");
        if (resetEvents.Count == 0)
        {
            _events.Children.Add(new Border
            {
                Background = Brush("#F6F8F6"),
                BorderBrush = Brush("#D9DFD9"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(24),
                Child = new StackPanel
                {
                    Spacing = 7,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = "↻", FontSize = 28, Foreground = Brush("#9AA59E"), HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock
                        {
                            Text = L("No reset cycles detected in this period", "此期間未偵測到重置週期", "此期间未检测到重置周期"),
                            FontWeight = FontWeight.SemiBold,
                            TextAlignment = TextAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = L("Keep usage logging enabled; a cycle appears after samples exist on both sides of a reset.", "請保持啟用用量記錄；重置前後都有樣本時才會顯示週期。", "请保持启用用量日志；重置前后都有样本时才会显示周期。"),
                            Foreground = Brush("#68756D"),
                            TextWrapping = TextWrapping.Wrap,
                            TextAlignment = TextAlignment.Center
                        }
                    }
                }
            });
            return;
        }
        foreach (var resetEvent in resetEvents.Take(250))
        {
            _events.Children.Add(EventCard(resetEvent));
        }
    }

    private Control EventCard(UsageResetEvent item)
    {
        var provider = new Border
        {
            Background = ProviderBrush(item.Provider, 0.13),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(9, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = DisplayProvider(item.Provider),
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = ProviderBrush(item.Provider, 1)
            }
        };
        var kind = new Border
        {
            Background = Brush("#FFF1E5"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = item.Kind switch
                {
                    UsageResetKind.FiveHour => "5hr reset",
                    UsageResetKind.Weekly => L("Week reset", "每週重置", "每周重置"),
                    _ => L("5hr + week reset", "5 小時 + 每週重置", "5 小时 + 每周重置")
                },
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush("#A95818")
            }
        };
        var badges = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children = { provider, kind }
        };
        var time = new TextBlock
        {
            Text = item.ObservedAt.ToLocalTime().ToString("ddd, MMM d · HH:mm", CultureInfo.CurrentCulture),
            FontWeight = FontWeight.SemiBold
        };
        var subtime = new TextBlock
        {
            Text = L(
                $"Expected {item.ExpectedAt.ToLocalTime():MMM d HH:mm} · observation gap {FormatDuration(item.ObservationGap)}",
                $"預計 {item.ExpectedAt.ToLocalTime():MMM d HH:mm} · 觀察間隔 {FormatDuration(item.ObservationGap)}",
                $"预计 {item.ExpectedAt.ToLocalTime():MMM d HH:mm} · 观察间隔 {FormatDuration(item.ObservationGap)}"),
            FontSize = 10,
            Foreground = Brush("#718078")
        };
        var quotaGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") , ColumnSpacing = 18 };
        quotaGrid.Children.Add(QuotaBar("5hr", item.FiveHourRemainingBefore, "#D46A45"));
        var week = QuotaBar(_text.UsageHistoryWeek, item.WeeklyRemainingBefore, "#8A5CD7");
        Grid.SetColumn(week, 1);
        quotaGrid.Children.Add(week);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel { Spacing = 4, Children = { time, subtime } });
        Grid.SetColumn(badges, 1);
        header.Children.Add(badges);
        return new Border
        {
            Background = Brush("#FCFDFC"),
            BorderBrush = Brush("#DDE5E0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 11),
            Child = new StackPanel { Spacing = 10, Children = { header, quotaGrid } }
        };
    }

    private Control QuotaBar(string label, int? unused, string color)
    {
        var value = new TextBlock
        {
            Text = unused is null ? "—" : $"{unused}% " + L("unused", "未使用", "未使用"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(color)
        };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#68756D") });
        Grid.SetColumn(value, 1);
        heading.Children.Add(value);
        var fill = new Border
        {
            Background = Brush(color),
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(4)
        };
        var track = new Border
        {
            Background = Brush("#DFE6E1"),
            Height = 7,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Child = fill
        };
        track.SizeChanged += (_, _) => fill.Width = track.Bounds.Width * Math.Clamp(unused ?? 0, 0, 100) / 100d;
        return new StackPanel { Spacing = 5, Children = { heading, track } };
    }

    private static Border SummaryCard(string label, string value, string detail, string accent) => new()
    {
        Width = 205,
        MinHeight = 91,
        Background = Brush("#F8FAF9"),
        BorderBrush = Brush("#DCE4DF"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(11),
        Padding = new Thickness(13),
        Margin = new Thickness(0, 0, 9, 6),
        Child = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = label, FontSize = 11, Foreground = Brush("#68756D") },
                new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brush(accent) },
                new TextBlock { Text = detail, FontSize = 10, Foreground = Brush("#718078") }
            }
        }
    };

    private static Control FilterGroup(string label, Control control) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 8,
        Margin = new Thickness(0, 0, 18, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new TextBlock
            {
                Text = label + ":",
                FontSize = 11,
                Foreground = Brush("#68756D"),
                VerticalAlignment = VerticalAlignment.Center
            },
            control
        }
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

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
