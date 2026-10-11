using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Localization;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// The "Records" tab of Usage insights: every sample of the selected period as
/// a searchable, filterable table that can be exported.
/// </summary>
public sealed class RecordsView : UserControl
{
    private const int PageSize = 300;

    private readonly UiText _text;
    private readonly TextBox _search;
    private readonly ToggleSwitch _changedOnly = new() { OnContent = string.Empty, OffContent = string.Empty };
    private readonly ToggleSwitch _errorsOnly = new() { OnContent = string.Empty, OffContent = string.Empty };
    private readonly TextBlock _count = new();
    private readonly Button _exportButton;
    private readonly Button _clearFiltersButton;
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly Button _showMoreButton;
    private readonly Border _toolbarCard;
    private readonly Border _tableCard;
    private readonly Border _tableHeaderSurface;
    private readonly List<TextBlock> _mutedLabels = [];
    private readonly DispatcherTimer _searchTimer;
    private TimelinePalette _palette = TimelinePalette.Light;
    private bool _isLightTheme = true;
    private IReadOnlyList<UsageHistoryEntry> _records = [];
    private IReadOnlyList<UsageHistoryEntry> _filtered = [];
    private DateTimeOffset _start;
    private DateTimeOffset _end;
    private int _shown;

    public RecordsView(UiText text)
    {
        _text = text;

        _search = new TextBox
        {
            PlaceholderText = L("Search provider, error or date…", "搜尋來源、錯誤或日期…", "搜索来源、错误或日期…"),
            MinWidth = 260,
            Height = 34,
            CornerRadius = new CornerRadius(9),
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Filtering thousands of rows per keystroke is wasteful; wait for a pause.
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ApplyFilters();
        };
        _search.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.TextProperty)
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            }
        };
        _changedOnly.IsCheckedChanged += (_, _) => ApplyFilters();
        _errorsOnly.IsCheckedChanged += (_, _) => ApplyFilters();
        ToolTip.SetTip(
            _changedOnly,
            L("Hide samples identical to the provider's previous one", "隱藏與該來源上一筆相同的樣本", "隐藏与该来源上一条相同的样本"));

        _count.FontSize = 11;
        _count.FontWeight = FontWeight.SemiBold;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _exportButton = ToolbarButton(string.Empty);
        _exportButton.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            Children =
            {
                new PathIcon
                {
                    Width = 12,
                    Height = 12,
                    Data = Geometry.Parse("M5 20H19V18H5ZM19 9H15V3H9V9H5L12 16Z")
                },
                new TextBlock
                {
                    Text = L("Export CSV", "匯出 CSV", "导出 CSV"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        _exportButton.Click += async (_, _) => await ExportAsync();
        _clearFiltersButton = ToolbarButton(L("Clear filters", "清除篩選", "清除筛选"));
        _clearFiltersButton.IsVisible = false;
        _clearFiltersButton.Click += (_, _) => ClearFilters();

        var filters = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Group(null, _search),
                Group(L("Changed only", "僅顯示變動", "仅显示变动"), _changedOnly),
                Group(L("Errors only", "僅顯示錯誤", "仅显示错误"), _errorsOnly),
                _clearFiltersButton
            }
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _count, _exportButton }
        };
        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Children = { filters, actions }
        };
        Grid.SetColumn(actions, 1);
        _toolbarCard = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 8),
            Child = toolbar
        };

        _tableHeaderSurface = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 11, 24, 11),
            Child = CreateTableHeader()
        };
        _showMoreButton = ToolbarButton(string.Empty);
        _showMoreButton.HorizontalAlignment = HorizontalAlignment.Center;
        _showMoreButton.Margin = new Thickness(0, 6, 0, 4);
        _showMoreButton.IsVisible = false;
        _showMoreButton.Click += (_, _) => ShowMore();
        var rowsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Children = { _rows, _showMoreButton }
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
        _tableCard.SizeChanged += (_, _) =>
            _tableCard.Clip = new RectangleGeometry(
                new Rect(0, 0, _tableCard.Bounds.Width, _tableCard.Bounds.Height), 16, 16);

        var body = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 12,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { _toolbarCard, _tableCard }
        };
        Grid.SetRow(_tableCard, 1);
        Content = body;

        AddHandler(KeyDownEvent, HandleKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        ActualThemeVariantChanged += (_, _) =>
        {
            ApplyVisualTheme();
            RebuildRows();
        };
        AttachedToVisualTree += (_, _) => ApplyVisualTheme();
        ApplyVisualTheme();
    }

    /// <summary>The records (newest first, failures included) of the selected period.</summary>
    public void UpdateRecords(
        IReadOnlyList<UsageHistoryEntry> records,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _records = records;
        _start = start;
        _end = end;
        ApplyFilters();
    }

    /// <summary>Puts the cursor in the search box (Ctrl+F).</summary>
    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    private UsageRecordFilterOptions CurrentFilter() => new()
    {
        ChangedOnly = _changedOnly.IsChecked == true,
        ErrorsOnly = _errorsOnly.IsChecked == true,
        Search = _search.Text
    };

    private bool HasActiveFilter() =>
        _changedOnly.IsChecked == true ||
        _errorsOnly.IsChecked == true ||
        !string.IsNullOrWhiteSpace(_search.Text);

    private void ApplyFilters()
    {
        _filtered = HasActiveFilter()
            ? UsageRecordFilter.Apply(_records, CurrentFilter(), DisplayProvider)
            : _records;
        _shown = Math.Min(PageSize, _filtered.Count);
        _clearFiltersButton.IsVisible = HasActiveFilter();
        _exportButton.IsEnabled = _filtered.Count > 0;
        UpdateCount();
        RebuildRows();
    }

    private void ClearFilters()
    {
        _search.Text = string.Empty;
        _changedOnly.IsChecked = false;
        _errorsOnly.IsChecked = false;
        ApplyFilters();
    }

    private void ShowMore()
    {
        _shown = Math.Min(_shown + PageSize, _filtered.Count);
        UpdateCount();
        RebuildRows();
    }

    private void UpdateCount()
    {
        var failures = _filtered.Count(entry =>
            !string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase));
        _count.Text = HasActiveFilter()
            ? L(
                $"{_filtered.Count:N0} of {_records.Count:N0} records",
                $"{_filtered.Count:N0} / {_records.Count:N0} 筆",
                $"{_filtered.Count:N0} / {_records.Count:N0} 条")
            : failures > 0
                ? L(
                    $"{_records.Count:N0} records · {failures:N0} errors",
                    $"{_records.Count:N0} 筆 · {failures:N0} 筆錯誤",
                    $"{_records.Count:N0} 条 · {failures:N0} 条错误")
                : L($"{_records.Count:N0} records", $"{_records.Count:N0} 筆", $"{_records.Count:N0} 条");
    }

    private void RebuildRows()
    {
        _rows.Children.Clear();
        if (_filtered.Count == 0)
        {
            _rows.Children.Add(CreateEmptyState());
            _showMoreButton.IsVisible = false;
            return;
        }

        for (var index = 0; index < _shown; index++)
        {
            _rows.Children.Add(CreateRow(_filtered[index], index));
        }

        var remaining = _filtered.Count - _shown;
        _showMoreButton.IsVisible = remaining > 0;
        if (remaining > 0)
        {
            _showMoreButton.Content = L(
                $"Show {Math.Min(PageSize, remaining):N0} more  ({remaining:N0} left)",
                $"再顯示 {Math.Min(PageSize, remaining):N0} 筆（剩餘 {remaining:N0} 筆）",
                $"再显示 {Math.Min(PageSize, remaining):N0} 条（剩余 {remaining:N0} 条）");
        }
    }

    private async Task ExportAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null || !storage.CanSave || _filtered.Count == 0)
        {
            return;
        }

        var defaultName = $"usage-records-{_start.ToLocalTime():yyyyMMdd}-{_end.ToLocalTime():yyyyMMdd}.csv";
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = L("Export records", "匯出記錄", "导出记录"),
            DefaultExtension = "csv",
            SuggestedFileName = defaultName,
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            // Every filtered row, not just the page that is on screen.
            await writer.WriteAsync(UsageRecordsExporter.ToCsv(_filtered));
            await writer.FlushAsync();
            await FlashAsync(_exportButton, "✓  " + L("Saved", "已儲存", "已保存"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await FlashAsync(_exportButton, "⚠  " + exception.Message);
        }
    }

    private static async Task FlashAsync(Button button, string text)
    {
        var original = button.Content;
        button.Content = text;
        await Task.Delay(1800);
        button.Content = original;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape && !string.IsNullOrEmpty(_search.Text) && _search.IsFocused)
        {
            eventArgs.Handled = true;
            _search.Text = string.Empty;
        }
    }

    private Control CreateEmptyState()
    {
        var message = _records.Count == 0
            ? _text.UsageHistoryNoPeriodData
            : L("No records match these filters.", "沒有符合篩選條件的記錄。", "没有符合筛选条件的记录。");
        return new Border
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
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush(_palette.Secondary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                }
            }
        };
    }

    private Control Group(string? label, Control control)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (label is not null)
        {
            var text = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 9,
                FontWeight = FontWeight.Bold,
                LetterSpacing = 0.35,
                VerticalAlignment = VerticalAlignment.Center
            };
            _mutedLabels.Add(text);
            panel.Children.Add(text);
        }

        panel.Children.Add(control);
        return panel;
    }

    private static Button ToolbarButton(string content) => new()
    {
        Content = content,
        Height = 34,
        Padding = new Thickness(13, 0),
        CornerRadius = new CornerRadius(9),
        BorderThickness = new Thickness(1),
        FontSize = 11.5,
        FontWeight = FontWeight.SemiBold,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void ApplyVisualTheme()
    {
        _isLightTheme = ActualThemeVariant == ThemeVariant.Light;
        _palette = _isLightTheme ? TimelinePalette.Light : TimelinePalette.Dark;

        _toolbarCard.Background = Brush(_palette.Surface);
        _toolbarCard.BorderBrush = Brush(_palette.Border);
        foreach (var label in _mutedLabels)
        {
            label.Foreground = Brush(_palette.Secondary);
        }

        _search.Background = Brush(_palette.Soft);
        _search.BorderBrush = Brush(_palette.BorderStrong);
        _search.Foreground = Brush(_palette.Primary);
        _count.Foreground = Brush(_palette.Accent);
        foreach (var button in new[] { _exportButton, _clearFiltersButton, _showMoreButton })
        {
            button.Background = Brush(_palette.Soft);
            button.BorderBrush = Brush(_palette.BorderStrong);
            button.Foreground = Brush(_palette.Primary);
        }

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

    private string L(string english, string traditional, string simplified) => _text.Language switch
    {
        UiLanguage.TraditionalChinese => traditional,
        UiLanguage.SimplifiedChinese => simplified,
        _ => english
    };

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
