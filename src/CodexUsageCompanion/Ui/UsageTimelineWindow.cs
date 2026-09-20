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
/// A dedicated, theme-aware table for the records selected in Usage overview.
/// </summary>
public sealed class UsageTimelineWindow : Window
{
    private readonly UiText _text;
    private readonly TextBlock _period = new();
    private readonly TextBlock _count = new();
    private readonly StackPanel _rows = new() { Spacing = 7 };
    private readonly Border _root;
    private readonly Border _titleBar;
    private readonly TextBlock _windowTitle;
    private readonly Button _minimizeButton;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private readonly Border _heroCard;
    private readonly Border _titleBadge;
    private readonly PathIcon _titleIcon;
    private readonly TextBlock _pageTitle;
    private readonly Border _countPill;
    private readonly Border _tableCard;
    private readonly Border _tableHeaderSurface;
    private TimelinePalette _palette = TimelinePalette.Light;
    private bool _isLightTheme = true;
    private IReadOnlyList<UsageHistoryEntry> _records = [];

    public UsageTimelineWindow(
        IReadOnlyList<UsageHistoryEntry> records,
        DateTimeOffset start,
        DateTimeOffset end,
        UiText text)
    {
        _text = text;
        Title = $"{text.UsageHistoryRecords} - Claude Codex Usage Companion";
        Width = 1040;
        Height = 736;
        MinWidth = 820;
        MinHeight = 520;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _windowTitle = new TextBlock
        {
            Text = text.UsageHistoryRecords,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _minimizeButton = WindowControlButton(
            WindowIcon("M4 11H20V13H4Z"),
            text.MinimizeAction);
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

        _titleIcon = new PathIcon
        {
            Width = 19,
            Height = 19,
            Data = Geometry.Parse(
                "M5 4H19V6H5ZM5 11H19V13H5ZM5 18H19V20H5ZM2 4H3.5V6H2ZM2 11H3.5V13H2ZM2 18H3.5V20H2Z")
        };
        _titleBadge = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(13),
            Child = _titleIcon
        };
        _pageTitle = new TextBlock
        {
            Text = text.UsageHistoryRecords,
            FontSize = 24,
            FontWeight = FontWeight.Bold
        };
        _period.FontSize = 11.5;
        var heading = new StackPanel
        {
            Spacing = 2,
            Children = { _pageTitle, _period }
        };
        var headingArea = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _titleBadge, heading }
        };
        _count.FontSize = 11;
        _count.FontWeight = FontWeight.SemiBold;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _countPill = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 7),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _count
        };
        var heroGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { headingArea, _countPill }
        };
        Grid.SetColumn(_countPill, 1);
        _heroCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(17, 14),
            Child = heroGrid
        };

        _tableHeaderSurface = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 11),
            Child = CreateTableHeader()
        };
        var rowsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border
            {
                Padding = new Thickness(10),
                Child = _rows
            }
        };
        var tableGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { _tableHeaderSurface, rowsScroll }
        };
        Grid.SetRow(rowsScroll, 1);
        _tableCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Child = tableGrid
        };
        _tableCard.SizeChanged += (_, _) => ApplyRoundedClip(_tableCard, 16);

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14,
            Margin = new Thickness(20),
            Children = { _heroCard, _tableCard }
        };
        Grid.SetRow(_tableCard, 1);
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

        ActualThemeVariantChanged += (_, _) =>
        {
            ApplyVisualTheme();
            RebuildRows();
        };
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
        UpdateRecords(records, start, end);
    }

    public void UpdateRecords(
        IReadOnlyList<UsageHistoryEntry> records,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _records = records;
        _period.Text = start.ToLocalTime().Date == end.ToLocalTime().Date
            ? start.ToLocalTime().ToString("dddd, MMM d, yyyy", CultureInfo.CurrentCulture)
            : $"{start.ToLocalTime():MMM d, yyyy} — {end.ToLocalTime():MMM d, yyyy}";
        _count.Text = _text.FormatUsageHistoryVisibleRecords(
            records.Count,
            records.Count(entry => string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)));
        RebuildRows();
    }

    private void RebuildRows()
    {
        _rows.Children.Clear();
        if (_records.Count == 0)
        {
            _rows.Children.Add(CreateEmptyState());
            return;
        }

        for (var index = 0; index < _records.Count; index++)
        {
            _rows.Children.Add(CreateRow(_records[index], index));
        }
    }

    private Control CreateTableHeader()
    {
        var grid = new Grid { ColumnDefinitions = TableColumns() };
        AddHeaderCell(grid, _text.UsageHistoryTimestamp, 0);
        AddHeaderCell(grid, _text.UsageHistoryProvider, 1);
        AddHeaderCell(grid, _text.UsageHistoryFiveHour, 2);
        AddHeaderCell(grid, _text.UsageHistoryWeek, 3);
        return grid;
    }

    private void AddHeaderCell(Grid grid, string text, int column)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = 9.5,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.5,
            Foreground = Brush(_palette.Secondary),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(block, column);
        grid.Children.Add(block);
    }

    private Control CreateRow(UsageHistoryEntry entry, int index)
    {
        var success = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase);
        var row = new Border
        {
            Background = Brush(success
                ? index % 2 == 0 ? _palette.Row : _palette.RowAlternate
                : _palette.ErrorSoft),
            BorderBrush = Brush(success ? _palette.Border : _palette.ErrorBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10)
        };
        row.PointerEntered += (_, _) => row.Background = Brush(success
            ? _palette.RowHover
            : _palette.ErrorSoftHover);
        row.PointerExited += (_, _) => row.Background = Brush(success
            ? index % 2 == 0 ? _palette.Row : _palette.RowAlternate
            : _palette.ErrorSoft);

        var grid = new Grid { ColumnDefinitions = TableColumns() };
        grid.Children.Add(CreateTimestampCell(entry.UpdatedAt));
        var provider = CreateProviderPill(entry);
        Grid.SetColumn(provider, 1);
        grid.Children.Add(provider);

        if (!success && !string.IsNullOrWhiteSpace(entry.Error))
        {
            var error = new Border
            {
                Background = Brush(_palette.ErrorSoftHover),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = entry.Error,
                    Foreground = Brush(_palette.Error),
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(error, 2);
            Grid.SetColumnSpan(error, 2);
            grid.Children.Add(error);
        }
        else
        {
            var fiveHour = CreateLimitCell(entry.FiveHourRemainingPercent, entry.FiveHourResetAt);
            var week = CreateLimitCell(entry.WeeklyRemainingPercent, entry.WeeklyResetAt);
            Grid.SetColumn(fiveHour, 2);
            Grid.SetColumn(week, 3);
            grid.Children.Add(fiveHour);
            grid.Children.Add(week);
        }

        row.Child = grid;
        return row;
    }

    private Control CreateTimestampCell(DateTimeOffset updatedAt)
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
                    Text = local.ToString("MMM d, yyyy", CultureInfo.CurrentCulture),
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

    private Control CreateProviderPill(UsageHistoryEntry entry)
    {
        var accent = ProviderColor(entry.Provider);
        return new Border
        {
            Background = Tint(accent, _isLightTheme ? (byte)24 : (byte)38),
            BorderBrush = Tint(accent, _isLightTheme ? (byte)95 : (byte)130),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(10, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase)
                    ? DisplayProvider(entry.Provider)
                    : $"{DisplayProvider(entry.Provider)} · {_text.UsageHistoryError}",
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brush(accent)
            }
        };
    }

    private Control CreateLimitCell(int? remainingPercent, DateTimeOffset? resetAt)
    {
        var signal = SignalColor(remainingPercent);
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 12, 0)
        };
        var percent = new TextBlock
        {
            Text = remainingPercent is null ? "—" : $"{Math.Clamp(remainingPercent.Value, 0, 100)}%",
            FontWeight = FontWeight.Bold,
            Foreground = Brush(signal),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center
        };
        var reset = new TextBlock
        {
            Text = resetAt is null
                ? _text.ResetUnavailable
                : _text.FormatUsageHistoryReset(resetAt.Value.ToLocalTime()),
            FontSize = 9.5,
            Foreground = Brush(_palette.Secondary),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(reset, 1);
        heading.Children.Add(percent);
        heading.Children.Add(reset);
        return new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { heading, CreateProgress(remainingPercent, signal) }
        };
    }

    private Control CreateProgress(int? remainingPercent, string signal)
    {
        var fill = new Border
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(3),
            Background = Brush(signal)
        };
        var track = new Border
        {
            Height = 6,
            Margin = new Thickness(0, 0, 12, 0),
            Background = Brush(_palette.Track),
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
        Background = Brush(_palette.Soft),
        BorderBrush = Brush(_palette.Border),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(32, 42),
        Child = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new PathIcon
                {
                    Width = 24,
                    Height = 24,
                    Foreground = Brush(_palette.Secondary),
                    Data = Geometry.Parse("M5 4H19V6H5ZM5 11H19V13H5ZM5 18H19V20H5Z")
                },
                new TextBlock
                {
                    Text = _text.UsageHistoryNoPeriodData,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush(_palette.Secondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            }
        }
    };

    private void ApplyVisualTheme()
    {
        _isLightTheme = ActualThemeVariant == ThemeVariant.Light;
        _palette = _isLightTheme ? TimelinePalette.Light : TimelinePalette.Dark;
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
        _titleBadge.Background = Brush(_palette.AccentSoft);
        _titleBadge.BorderBrush = Brush(_palette.AccentBorder);
        _titleBadge.BorderThickness = new Thickness(1);
        _titleIcon.Foreground = Brush(_palette.Accent);
        _pageTitle.Foreground = Brush(_palette.Primary);
        _period.Foreground = Brush(_palette.Secondary);
        _countPill.Background = Brush(_palette.AccentSoft);
        _countPill.BorderBrush = Brush(_palette.AccentBorder);
        _count.Foreground = Brush(_palette.Accent);

        _tableCard.Background = Brush(_palette.Surface);
        _tableCard.BorderBrush = Brush(_palette.Border);
        _tableHeaderSurface.Background = Brush(_palette.Soft);
        _tableHeaderSurface.BorderBrush = Brush(_palette.Border);
        if (_tableHeaderSurface.Child is Grid header)
        {
            foreach (var label in header.Children.OfType<TextBlock>())
            {
                label.Foreground = Brush(_palette.Secondary);
            }
        }
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
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void UpdateWindowShape()
    {
        var maximized = WindowState == WindowState.Maximized;
        _root.Margin = maximized ? new Thickness(0) : new Thickness(8);
        _root.CornerRadius = new CornerRadius(maximized ? 0 : 17);
        _root.BorderThickness = maximized ? new Thickness(0) : new Thickness(1);
        _titleBar.CornerRadius = maximized
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

    private static void ApplyRoundedClip(Control control, double radius) =>
        control.Clip = new RectangleGeometry(
            new Rect(0, 0, control.Bounds.Width, control.Bounds.Height),
            radius,
            radius);

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

    private static ColumnDefinitions TableColumns() => new("170,220,*,*");

    private static string DisplayProvider(string provider) => provider.ToLowerInvariant() switch
    {
        "claude" => "Claude",
        "codex" => "Codex",
        "antigravity-gemini" => "Antigravity · Gemini",
        "antigravity-claudeandchatgpt" => "Antigravity · Claude + ChatGPT",
        _ => provider
    };

    private string SignalColor(int? remainingPercent) => (remainingPercent ?? 0) switch
    {
        < 40 => _palette.Red,
        < 60 => _palette.Orange,
        < 80 => _palette.Yellow,
        _ => _palette.Green
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

    private sealed record TimelinePalette(
        string Root,
        string Surface,
        string Soft,
        string HeaderStart,
        string HeaderEnd,
        string Row,
        string RowAlternate,
        string RowHover,
        string Border,
        string BorderStrong,
        string Primary,
        string Secondary,
        string Accent,
        string AccentSoft,
        string AccentBorder,
        string Track,
        string Error,
        string ErrorSoft,
        string ErrorSoftHover,
        string ErrorBorder,
        string Green,
        string Yellow,
        string Orange,
        string Red)
    {
        public static TimelinePalette Light { get; } = new(
            "#FFF2F5F3", "#FFFFFFFF", "#FFF7F9F7", "#FFFFFFFF", "#FFEDF8F3",
            "#FFFFFFFF", "#FFFAFBFA", "#FFF1F7F4", "#FFDCE4DF", "#FFCAD6CE",
            "#FF1F2822", "#FF67736B", "#FF0F8A68", "#FFE5F5EE", "#FFA8D8C5",
            "#FFDDE4DF", "#FFB13A32", "#FFFFF3F1", "#FFFFE8E5", "#FFF1C4BF",
            "#FF0F8A5F", "#FFA98700", "#FFD97706", "#FFD84A42");

        public static TimelinePalette Dark { get; } = new(
            "#FF151816", "#FF202421", "#FF292E2A", "#FF252A26", "#FF1C3028",
            "#FF202421", "#FF232824", "#FF29352F", "#FF394039", "#FF4A534C",
            "#FFF0F4F1", "#FFA9B3AB", "#FF4AD894", "#FF1D392E", "#FF376D58",
            "#FF3A413B", "#FFFF8A80", "#FF3A2422", "#FF482A27", "#FF70403B",
            "#FF4AD894", "#FFF2CF5B", "#FFFF9847", "#FFFF6666");
    }
}
