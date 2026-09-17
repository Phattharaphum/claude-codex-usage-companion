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
/// A local-only, read-only view of the optional usage-history CSV.
/// </summary>
public sealed class UsageHistoryWindow : Window
{
    private const int MaximumVisibleRows = 240;
    private readonly CompanionSettings _settings;
    private readonly UiText _text;
    private readonly UsageHistoryReader _reader;
    private readonly StackPanel _summaryCards = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10
    };
    private readonly StackPanel _rows = new() { Spacing = 5 };
    private readonly WrapPanel _providerFilters = new()
    {
        Orientation = Orientation.Horizontal
    };
    private readonly TextBlock _subtitle = new();
    private readonly TextBlock _message = new();
    private readonly ComboBox _rangeSelector;
    private readonly ComboBox _windowSelector;
    private readonly UsageHistoryChart _chart;
    private IReadOnlyList<UsageHistoryEntry> _entries = [];

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
            MinWidth = 96
        };
        _windowSelector = new ComboBox
        {
            ItemsSource = new[]
            {
                text.UsageHistoryWeeklyWindow,
                text.UsageHistoryFiveHourWindow
            },
            SelectedIndex = 0,
            MinWidth = 105
        };
        _chart = new UsageHistoryChart
        {
            RemainingLabel = text.UsageHistoryRemaining,
            ResetLabel = text.UsageHistoryReset,
            NoDataText = text.UsageHistoryNoChartData,
            ProviderLabelFormatter = ChartProviderLabel
        };
        _rangeSelector.SelectionChanged += (_, _) => ApplyChartFilters();
        _windowSelector.SelectionChanged += (_, _) => ApplyChartFilters();

        Title = text.UsageHistoryTitle;
        Width = 980;
        Height = 760;
        MinWidth = 760;
        MinHeight = 560;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var refresh = new Button
        {
            Content = "↻  " + text.RefreshAction,
            MinWidth = 118
        };
        refresh.Click += (_, _) => Reload();
        var close = new Button
        {
            Content = text.CloseAction,
            MinWidth = 92,
            IsCancel = true
        };
        close.Click += (_, _) => Close();

        var title = new TextBlock
        {
            Text = text.UsageHistoryTitle,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold
        };
        _subtitle.FontSize = 12;
        _subtitle.Foreground = Brush("#66706A");
        var titleStack = new StackPanel { Spacing = 3 };
        titleStack.Children.Add(title);
        titleStack.Children.Add(_subtitle);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { refresh, close }
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(actions, 1);
        header.Children.Add(titleStack);
        header.Children.Add(actions);

        var summaryTitle = new TextBlock
        {
            Text = text.UsageHistoryLatestSnapshot,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var summaryScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _summaryCards
        };

        _message.TextWrapping = TextWrapping.Wrap;
        _message.Foreground = Brush("#9C3D35");
        _message.IsVisible = false;

        var rangeFilters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        rangeFilters.Children.Add(CreateFilterLabel(text.UsageHistoryTimeRange));
        rangeFilters.Children.Add(_rangeSelector);
        rangeFilters.Children.Add(CreateFilterLabel(text.UsageHistoryQuotaWindow));
        rangeFilters.Children.Add(_windowSelector);

        var providerFilters = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        providerFilters.Children.Add(CreateFilterLabel(text.UsageHistoryProvider));
        Grid.SetColumn(_providerFilters, 1);
        providerFilters.Children.Add(_providerFilters);

        var filters = new StackPanel
        {
            Spacing = 6,
            Children = { rangeFilters, providerFilters }
        };

        var chartTitle = new TextBlock
        {
            Text = text.UsageHistoryChartTitle,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 6, 0, 0)
        };
        var chartFrame = new Border
        {
            Background = Brush("#FBFCFB"),
            BorderBrush = Brush("#D9DFD9"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 4),
            Child = _chart
        };

        var tableTitle = new TextBlock
        {
            Text = text.UsageHistoryRecords,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 6, 0, 0)
        };
        var tableHeader = CreateTableHeader();
        var tableContent = new StackPanel { Spacing = 5 };
        tableContent.Children.Add(tableHeader);
        tableContent.Children.Add(_rows);
        var tableScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = tableContent
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(24),
            RowSpacing = 12
        };
        AddRow(layout, header, 0);
        AddRow(layout, summaryTitle, 1);
        AddRow(layout, summaryScroll, 2);
        AddRow(layout, filters, 3);
        AddRow(layout, chartTitle, 4);
        AddRow(layout, chartFrame, 5);
        AddRow(layout, _message, 6);
        AddRow(layout, tableTitle, 7);
        AddRow(layout, tableScroll, 8);
        Content = layout;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Reload();
    }

    public void Reload()
    {
        var result = _reader.Read(_settings.UsageLogFilePath, _settings.UsageLogFormat);
        _summaryCards.Children.Clear();
        _rows.Children.Clear();
        _message.IsVisible = !string.IsNullOrWhiteSpace(result.Error);
        _message.Text = result.Error ?? string.Empty;

        _entries = result.Entries;
        var entries = _entries;
        _subtitle.Text = entries.Count == 0
            ? _text.UsageHistoryEmptySubtitle
            : _text.FormatUsageHistoryCount(entries.Count, MaximumVisibleRows);

        _providerFilters.Children.Clear();
        foreach (var provider in entries
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
                IsChecked = true,
                Tag = provider,
                FontSize = 11,
                Margin = new Thickness(0, 0, 10, 2)
            };
            checkBox.IsCheckedChanged += (_, _) => ApplyChartFilters();
            _providerFilters.Children.Add(checkBox);
        }
        ApplyChartFilters();

        foreach (var entry in LatestEntries(entries))
        {
            _summaryCards.Children.Add(CreateSummaryCard(entry));
        }

        if (entries.Count == 0)
        {
            _rows.Children.Add(CreateEmptyState());
            return;
        }

        foreach (var entry in entries.Take(MaximumVisibleRows))
        {
            _rows.Children.Add(CreateRow(entry));
        }
    }

    private static TextBlock CreateFilterLabel(string text) => new()
    {
        Text = text + ":",
        FontSize = 11,
        Foreground = Brush("#66706A"),
        VerticalAlignment = VerticalAlignment.Center
    };

    private void ApplyChartFilters()
    {
        var range = _rangeSelector.SelectedIndex switch
        {
            0 => UsageHistoryTimeRange.TwentyFourHours,
            2 => UsageHistoryTimeRange.ThirtyDays,
            3 => UsageHistoryTimeRange.All,
            _ => UsageHistoryTimeRange.SevenDays
        };
        var window = _windowSelector.SelectedIndex == 1
            ? UsageHistoryQuotaWindow.FiveHour
            : UsageHistoryQuotaWindow.Weekly;
        var selectedProviders = _providerFilters.Children
            .OfType<CheckBox>()
            .Where(checkBox => checkBox.IsChecked == true && checkBox.Tag is string)
            .Select(checkBox => (string)checkBox.Tag!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _chart.SetSeries(UsageHistorySeriesBuilder.Build(
            _entries,
            window,
            range,
            providers: selectedProviders));
    }

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private IEnumerable<UsageHistoryEntry> LatestEntries(IReadOnlyList<UsageHistoryEntry> entries)
    {
        var orderedProviders = new[]
        {
            "claude",
            "codex",
            "Antigravity-Gemini",
            "Antigravity-ClaudeAndChatGPT"
        };
        foreach (var provider in orderedProviders)
        {
            var entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.Status, "success", StringComparison.OrdinalIgnoreCase));
            if (entry is not null)
            {
                yield return entry;
            }
        }
    }

    private Control CreateSummaryCard(UsageHistoryEntry entry)
    {
        var card = new Border
        {
            Width = 205,
            Background = Brush("#F6F8F6"),
            BorderBrush = Brush("#D9DFD9"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12)
        };
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = DisplayProvider(entry.Provider),
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        stack.Children.Add(CreateCompactLimit(entry.FiveHourRemainingPercent, _text.UsageHistoryFiveHour));
        stack.Children.Add(CreateCompactLimit(entry.WeeklyRemainingPercent, _text.UsageHistoryWeek));
        card.Child = stack;
        return card;
    }

    private Control CreateCompactLimit(int? remainingPercent, string label)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var title = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = Brush("#66706A")
        };
        var value = new TextBlock
        {
            Text = Percent(remainingPercent),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = SignalBrush(remainingPercent)
        };
        Grid.SetColumn(value, 1);
        grid.Children.Add(title);
        grid.Children.Add(value);
        return grid;
    }

    private Control CreateTableHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("165,195,*,*"),
            Margin = new Thickness(12, 0)
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
            Foreground = Brush("#66706A")
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private Control CreateRow(UsageHistoryEntry entry)
    {
        var row = new Border
        {
            Background = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)
                ? Brush("#FBFCFB")
                : Brush("#FFF5F3"),
            BorderBrush = Brush("#E0E5E0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 9)
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("165,195,*,*") };
        var timestamp = new TextBlock
        {
            Text = entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd  HH:mm", CultureInfo.CurrentCulture),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        var provider = CreateProviderPill(entry);
        var fiveHour = CreateLimitCell(entry.FiveHourRemainingPercent, entry.FiveHourResetAt, entry.Error);
        var week = CreateLimitCell(entry.WeeklyRemainingPercent, entry.WeeklyResetAt, entry.Error);
        Grid.SetColumn(provider, 1);
        Grid.SetColumn(fiveHour, 2);
        Grid.SetColumn(week, 3);
        grid.Children.Add(timestamp);
        grid.Children.Add(provider);
        grid.Children.Add(fiveHour);
        grid.Children.Add(week);
        row.Child = grid;
        return row;
    }

    private Control CreateProviderPill(UsageHistoryEntry entry)
    {
        var border = new Border
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
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = ProviderBrush(entry.Provider, 1)
            }
        };
        return border;
    }

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

        var stack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 10, 0) };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new TextBlock
        {
            Text = resetAt is null
                ? _text.ResetUnavailable
                : _text.FormatUsageHistoryReset(resetAt.Value.ToLocalTime()),
            FontSize = 11,
            Foreground = Brush("#66706A"),
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
        var track = new Border
        {
            Height = 6,
            Background = Brush("#DDE3DE"),
            CornerRadius = new CornerRadius(3),
            ClipToBounds = true
        };
        var fill = new Border
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(3),
            Background = SignalBrush(remainingPercent)
        };
        track.Child = fill;
        track.SizeChanged += (_, _) =>
        {
            var percent = Math.Clamp(remainingPercent ?? 0, 0, 100);
            fill.Width = Math.Round(track.Bounds.Width * percent / 100d);
        };
        return track;
    }

    private Control CreateEmptyState() => new Border
    {
        Background = Brush("#F6F8F6"),
        BorderBrush = Brush("#D9DFD9"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(20),
        Child = new TextBlock
        {
            Text = _text.UsageHistoryEmpty,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#66706A"),
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

    private static bool IsChartProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" or
        "codex" or
        "antigravity-gemini" or
        "antigravity-claudeandchatgpt" => true,
        _ => false
    };

    private static int ProviderOrder(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => 0,
        "codex" => 1,
        "antigravity-gemini" => 2,
        "antigravity-claudeandchatgpt" => 3,
        _ => 4
    };

    private static string Percent(int? value) => value is null ? "—" : $"{Math.Clamp(value.Value, 0, 100)}%";

    private static IBrush SignalBrush(int? remainingPercent)
    {
        var value = remainingPercent ?? 0;
        return value switch
        {
            < 40 => Brush("#D84A42"),
            < 60 => Brush("#D97706"),
            < 80 => Brush("#A98700"),
            _ => Brush("#0F8A5F")
        };
    }

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
