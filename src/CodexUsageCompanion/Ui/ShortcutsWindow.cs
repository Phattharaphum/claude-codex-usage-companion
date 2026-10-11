using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// The keyboard shortcut reference. It shares the chrome and palette of the
/// other secondary windows and draws each key as a keycap.
/// </summary>
public sealed class ShortcutsWindow : Window
{
    private readonly UiText _text;
    private readonly Border _root;
    private readonly Border _titleBar;
    private readonly TextBlock _titleText;
    private readonly Button _closeButton;
    private readonly Grid _groups = new()
    {
        ColumnDefinitions = new ColumnDefinitions("*,*"),
        ColumnSpacing = 18
    };
    private readonly StackPanel _leftColumn = new() { Spacing = 14 };
    private readonly StackPanel _rightColumn = new() { Spacing = 14 };
    private readonly TextBlock _hint;
    private ShortcutsPalette _palette = ShortcutsPalette.Dark;

    public ShortcutsWindow(CompanionSettings settings, UiText text)
    {
        _text = text;
        Title = text.ShortcutsTitle;
        Width = 900;
        MinWidth = 760;
        MaxWidth = 1000;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        CanResize = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _titleText = new TextBlock
        {
            Text = text.ShortcutsAction,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _closeButton = new Button
        {
            Content = new PathIcon
            {
                Width = 12,
                Height = 12,
                Data = Geometry.Parse("M6.7 5.3L12 10.6L17.3 5.3L18.7 6.7L13.4 12L18.7 17.3L17.3 18.7L12 13.4L6.7 18.7L5.3 17.3L10.6 12L5.3 6.7Z")
            },
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        A11y.Tip(_closeButton, text.CloseAction);
        _closeButton.Click += (_, _) => Close();
        _closeButton.PointerEntered += (_, _) => ApplyCloseTheme(hovered: true);
        _closeButton.PointerExited += (_, _) => ApplyCloseTheme(hovered: false);

        var dragLeft = new Border { Background = Brushes.Transparent };
        var dragCenter = new Border { Background = Brushes.Transparent, Child = _titleText };
        dragLeft.PointerPressed += HandleTitleBarPointerPressed;
        dragCenter.PointerPressed += HandleTitleBarPointerPressed;
        var titleGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("60,*,60"),
            Height = 38,
            Children = { dragLeft, dragCenter, _closeButton }
        };
        Grid.SetColumn(dragCenter, 1);
        Grid.SetColumn(_closeButton, 2);
        _titleBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(16, 16, 0, 0),
            Child = titleGrid
        };

        Grid.SetColumn(_rightColumn, 1);
        _groups.Children.Add(_leftColumn);
        _groups.Children.Add(_rightColumn);
        _hint = new TextBlock
        {
            Text = text.ShortcutsCloseHint,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var body = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(22, 18, 22, 18),
            Children = { _groups, _hint }
        };
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = body
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("38,*"),
            Children = { _titleBar, scroll }
        };
        Grid.SetRow(scroll, 1);
        _root = new Border
        {
            Margin = new Thickness(8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(17),
            ClipToBounds = true,
            Child = layout
        };
        Content = _root;

        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        Opened += (_, _) => ApplyTheme();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        _palette = ActualThemeVariant == ThemeVariant.Light ? ShortcutsPalette.Light : ShortcutsPalette.Dark;
        _root.Background = Brush(_palette.Root);
        _root.BorderBrush = Brush(_palette.BorderStrong);
        _titleBar.Background = Brush(_palette.Surface);
        _titleBar.BorderBrush = Brush(_palette.Border);
        _titleText.Foreground = Brush(_palette.Primary);
        _hint.Foreground = Brush(_palette.Secondary);
        ApplyCloseTheme(hovered: false);

        // Two columns keep the whole reference on one screen: alternate groups
        // go left and right.
        _leftColumn.Children.Clear();
        _rightColumn.Children.Clear();
        var index = 0;
        foreach (var group in _text.ShortcutGroups)
        {
            (index++ % 2 == 0 ? _leftColumn : _rightColumn).Children.Add(CreateGroup(group));
        }
    }

    private void ApplyCloseTheme(bool hovered)
    {
        _closeButton.Background = Brush(hovered ? _palette.ErrorSoft : "#00000000");
        _closeButton.BorderBrush = Brush(hovered ? _palette.ErrorBorder : "#00000000");
        _closeButton.Foreground = Brush(hovered ? _palette.Error : _palette.Secondary);
    }

    private Control CreateGroup(ShortcutGroup group)
    {
        var rows = new StackPanel { Spacing = 0 };
        for (var index = 0; index < group.Shortcuts.Count; index++)
        {
            rows.Children.Add(CreateRow(group.Shortcuts[index], index < group.Shortcuts.Count - 1));
        }

        return new StackPanel
        {
            Spacing = 7,
            Children =
            {
                new TextBlock
                {
                    Text = group.Title.ToUpperInvariant(),
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    LetterSpacing = 0.6,
                    Margin = new Thickness(4, 0, 0, 0),
                    Foreground = Brush(_palette.Accent)
                },
                new Border
                {
                    Background = Brush(_palette.Surface),
                    BorderBrush = Brush(_palette.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(13),
                    Padding = new Thickness(14, 4),
                    Child = rows
                }
            }
        };
    }

    private Control CreateRow(ShortcutHint shortcut, bool divider)
    {
        var keys = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        var parts = shortcut.KeyParts;
        for (var index = 0; index < parts.Count; index++)
        {
            if (index > 0)
            {
                keys.Children.Add(new TextBlock
                {
                    Text = "+",
                    FontSize = 11,
                    Foreground = Brush(_palette.Secondary),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            keys.Children.Add(CreateKeycap(parts[index]));
        }

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            Margin = new Thickness(0, 8, 0, 8),
            Children =
            {
                keys,
                new TextBlock
                {
                    Text = shortcut.Description,
                    FontSize = 12.5,
                    Foreground = Brush(_palette.Primary),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        Grid.SetColumn(row.Children[1], 1);
        return new Border
        {
            BorderBrush = Brush(_palette.Border),
            BorderThickness = new Thickness(0, 0, 0, divider ? 1 : 0),
            Child = row
        };
    }

    private Control CreateKeycap(string key) => new Border
    {
        Background = Brush(_palette.Keycap),
        BorderBrush = Brush(_palette.KeycapBorder),
        // A heavier bottom edge reads as a key you can press.
        BorderThickness = new Thickness(1, 1, 1, 2),
        CornerRadius = new CornerRadius(6),
        MinWidth = 26,
        Padding = new Thickness(8, 3),
        Child = new TextBlock
        {
            Text = key,
            FontSize = 11.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(_palette.Primary),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    private void HandleTitleBarPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(eventArgs);
        }
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Escape or Key.F1))
        {
            return;
        }

        eventArgs.Handled = true;
        Close();
    }

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));

    internal sealed record ShortcutsPalette(
        string Root,
        string Surface,
        string Border,
        string BorderStrong,
        string Primary,
        string Secondary,
        string Accent,
        string Keycap,
        string KeycapBorder,
        string Error,
        string ErrorSoft,
        string ErrorBorder)
    {
        public static ShortcutsPalette Light { get; } = new(
            "#FFF2F5F3", "#FFFFFFFF", "#FFDCE4DF", "#FFCAD6CE",
            "#FF1F2822", "#FF67736B", "#FF0B7A5A",
            "#FFF3F6F4", "#FFC4D0C8", "#FFB13A32", "#FFFFE9E7", "#FFFFC8C3");

        public static ShortcutsPalette Dark { get; } = new(
            "#FF151816", "#FF202421", "#FF394039", "#FF4A534C",
            "#FFF0F4F1", "#FFA9B3AB", "#FF4AD894",
            "#FF2B312C", "#FF4A534C", "#FFFF8A80", "#FF472725", "#FF74413D");
    }
}
