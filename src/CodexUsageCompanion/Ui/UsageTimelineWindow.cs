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
/// A dedicated table window for the records selected in Usage overview.
/// </summary>
public sealed class UsageTimelineWindow : Window
{
    private readonly UiText _text;
    private readonly TextBlock _period = new();
    private readonly TextBlock _count = new();
    private readonly StackPanel _rows = new() { Spacing = 6 };

    public UsageTimelineWindow(
        IReadOnlyList<UsageHistoryEntry> records,
        DateTimeOffset start,
        DateTimeOffset end,
        UiText text)
    {
        _text = text;
        Title = $"{text.UsageHistoryRecords} - Claude Codex Usage Companion";
        Width = 940;
        Height = 680;
        MinWidth = 760;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var title = new TextBlock
        {
            Text = text.UsageHistoryRecords,
            FontSize = 23,
            FontWeight = FontWeight.Bold
        };
        _period.FontSize = 12;
        _period.Foreground = Brush("#68756D");
        _count.FontSize = 11;
        _count.Foreground = Brush("#68756D");
        _count.VerticalAlignment = VerticalAlignment.Center;
        var close = new Button { Content = text.CloseAction, MinWidth = 88, IsCancel = true };
        close.Click += (_, _) => Close();
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new StackPanel { Spacing = 3, Children = { title, _period } });
        Grid.SetColumn(close, 1);
        header.Children.Add(close);

        var table = new StackPanel { Spacing = 7 };
        table.Children.Add(CreateTableHeader());
        table.Children.Add(_rows);
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = table
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 12,
            Margin = new Thickness(24)
        };
        AddRow(layout, header, 0);
        AddRow(layout, _count, 1);
        AddRow(layout, scroll, 2);
        Content = layout;
        AddHandler(KeyDownEvent, (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                eventArgs.Handled = true;
                Close();
            }
        }, RoutingStrategies.Tunnel);
        UpdateRecords(records, start, end);
    }

    public void UpdateRecords(
        IReadOnlyList<UsageHistoryEntry> records,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _period.Text = start.ToLocalTime().Date == end.ToLocalTime().Date
            ? start.ToLocalTime().ToString("dddd, MMM d, yyyy", CultureInfo.CurrentCulture)
            : $"{start.ToLocalTime():MMM d, yyyy} — {end.ToLocalTime():MMM d, yyyy}";
        _count.Text = _text.FormatUsageHistoryVisibleRecords(
            records.Count,
            records.Count(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)));
        _rows.Children.Clear();
        if (records.Count == 0)
        {
            _rows.Children.Add(CreateEmptyState());
            return;
        }

        foreach (var entry in records)
        {
            _rows.Children.Add(CreateRow(entry));
        }
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
            Text = remainingPercent is null ? "—" : $"{Math.Clamp(remainingPercent.Value, 0, 100)}%",
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

    private Control CreateEmptyState() => new Border
    {
        Background = Brush("#F6F8F6"),
        BorderBrush = Brush("#D9DFD9"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(28),
        Child = new TextBlock
        {
            Text = _text.UsageHistoryNoPeriodData,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#68756D"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        }
    };

    private static string DisplayProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · Claude + ChatGPT",
        _ => provider
    };

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

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
