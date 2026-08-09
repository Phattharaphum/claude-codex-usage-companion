using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

public sealed class ShortcutsWindow : Window
{
    public ShortcutsWindow(CompanionSettings settings, UiText text)
    {
        Title = text.ShortcutsTitle;
        Width = 420;
        MinWidth = 420;
        MaxWidth = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var groups = new StackPanel { Spacing = 18 };
        foreach (var group in text.ShortcutGroups)
        {
            groups.Children.Add(CreateGroup(group));
        }

        var close = new Button
        {
            Content = text.OkAction,
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsDefault = true
        };
        close.Click += (_, _) => Close();

        var layout = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(24),
            Children = { groups, close }
        };
        Content = layout;
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    private static Control CreateGroup(ShortcutGroup group)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = group.Title,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 2)
        });
        foreach (var shortcut in group.Shortcuts)
        {
            stack.Children.Add(CreateShortcutRow(shortcut));
        }

        return stack;
    }

    private static Control CreateShortcutRow(ShortcutHint shortcut)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("96,*")
        };
        var keys = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#20808080")),
            BorderBrush = new SolidColorBrush(Color.Parse("#40808080")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = shortcut.Keys,
                FontFamily = FontFamily.Default,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12
            }
        };
        var description = new TextBlock
        {
            Text = shortcut.Description,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(description, 1);
        grid.Children.Add(keys);
        grid.Children.Add(description);
        return grid;
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
}
