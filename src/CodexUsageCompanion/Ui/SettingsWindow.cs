using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Localization;
using CodexUsageCompanion.Platform;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace CodexUsageCompanion.Ui;

public sealed class SettingsWindow : Window
{
    private enum SettingsCategory
    {
        General,
        Providers,
        Appearance,
        Notifications,
        DateTime,
        Logging,
        About
    }

    private sealed class CategoryNavItem
    {
        public required SettingsCategory Category { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required string IconData { get; init; }
        public required string[] Keywords { get; init; }
        public Button Button { get; set; } = null!;
        public Border Indicator { get; set; } = null!;
        public AvaloniaPath IconPath { get; set; } = null!;
        public TextBlock TitleBlock { get; set; } = null!;
        public StackPanel ContentPanel { get; set; } = null!;
    }

    private sealed record SettingRowInfo(string Text);

    private sealed record SettingsPalette(
        IBrush Window,
        IBrush Sidebar,
        IBrush SidebarBorder,
        IBrush Card,
        IBrush CardBorder,
        IBrush Divider,
        IBrush TextPrimary,
        IBrush TextSecondary,
        IBrush Accent,
        IBrush AccentSoft,
        IBrush PrimaryButton,
        IBrush Control,
        IBrush ControlBorder,
        Color NoticeBackground,
        Color NoticeBorder,
        IBrush NoticeText)
    {
        // One neutral palette and one accent shared with the overlay and the
        // history windows, so Settings no longer looks like a different app.
        public static SettingsPalette Dark { get; } = new(
            Brush("#FF14171A"), Brush("#FF181C20"), Brush("#FF262C33"),
            Brush("#FF1D2227"), Brush("#FF2A3139"), Brush("#FF272E35"),
            Brush("#FFEDEFF1"), Brush("#FF9AA4AD"),
            Brush("#FF3DD598"), Brush("#FF1B3A30"), Brush("#FF14865F"),
            Brush("#FF242B32"), Brush("#FF343C45"),
            Color.Parse("#26F2C94C"), Color.Parse("#59F2C94C"), Brush("#FFF2C94C"));

        public static SettingsPalette Light { get; } = new(
            Brush("#FFF6F7F9"), Brush("#FFEEF1F4"), Brush("#FFE1E5EA"),
            Brush("#FFFFFFFF"), Brush("#FFE1E5EA"), Brush("#FFEDF0F3"),
            Brush("#FF1B1F23"), Brush("#FF5B646D"),
            Brush("#FF12805A"), Brush("#FFDDF1E9"), Brush("#FF14865F"),
            Brush("#FFFFFFFF"), Brush("#FFD3D9DF"),
            Color.Parse("#FFFFF8E1"), Color.Parse("#FFF3DC8A"), Brush("#FF8A6500"));

        private static SolidColorBrush Brush(string color) => SolidColorBrush.Parse(color);
    }

    private sealed class CardInfo
    {
        public string Text { get; init; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public List<Border> Rows { get; } = [];
        public List<Control> Items { get; } = [];
    }

    private sealed record PositionChoice(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly CompanionSettings _settings;
    private readonly UiText _text;

    // General controls
    private readonly ToggleSwitch _systemTray;
    private readonly ComboBox _trayIconStyle;
    private readonly ToggleSwitch _showTaskbarIcon;
    private readonly ToggleSwitch _startOnBoot;
    private readonly ToggleSwitch _minimizeOnStart;
    private readonly ToggleSwitch _alwaysOnTop;
    private Border _minimizeOnStartRow = null!;

    // Providers controls
    private readonly ToggleSwitch _enableClaudeUsage;
    private readonly ToggleSwitch _enableCodexUsage;
    private readonly ToggleSwitch _enableAntigravityUsage;
    private readonly ToggleSwitch _showClaudeSession;
    private readonly ToggleSwitch _showClaudeWeekly;
    private readonly ToggleSwitch _showCodexFiveHour;
    private readonly ToggleSwitch _showCodexWeekly;
    private readonly ComboBox _updateInterval;
    private Border _claudeSubOptionsContainer = null!;
    private Border _codexSubOptionsContainer = null!;

    // Appearance controls
    private readonly ComboBox _language;
    private readonly ComboBox _theme;
    private readonly ComboBox _position;
    private readonly PositionChoice[] _positionChoices;
    private readonly Slider _opacity;
    private readonly TextBlock _opacityValue;
    private readonly NumericUpDown _margin;
    private readonly Dictionary<string, Button> _positionGridButtons = new();
    private Border _screenBorder = null!;
    private Border _screenStandStem = null!;
    private Border _screenStandBase = null!;

    // Notification controls
    private readonly ToggleSwitch _lowUsageAlert;
    private readonly NumericUpDown _lowUsageAlertThreshold;
    private readonly ToggleSwitch _notifyOnReset;
    private Border _lowUsageThresholdRow = null!;
    private Border _notifySendWarning = null!;
    private Button _testNotificationButton = null!;

    // DateTime controls
    private readonly ComboBox _resetDateTimeFormat;
    private readonly ComboBox _lastUpdatedDateTimeFormat;
    private readonly TextBlock _resetDateTimeFormatStatus;
    private readonly TextBlock _lastUpdatedDateTimeFormatStatus;
    private Border _resetFormatBadge = null!;
    private Border _lastUpdatedFormatBadge = null!;
    private readonly List<Button> _presetChips = new();

    // Logging controls
    private readonly ToggleSwitch _usageLogging;
    private readonly TextBox _usageLogFilePath;
    private readonly ComboBox _usageLogFormat;
    private readonly ComboBox _usageLogRetention;
    private Border _usageLogRetentionRow = null!;
    private Border _usageLogFilePathRow = null!;
    private Border _usageLogFormatRow = null!;
    private Border _aboutLogoBadge = null!;
    private Border _aboutVersionChip = null!;
    private TextBlock _historyFileInfo = null!;
    private Button _openFolderButton = null!;
    private Button _clearHistoryButton = null!;
    private int _historyInfoVersion;

    // About
    private TextBox _diagnosticsBox = null!;
    private Button _copyDiagnosticsButton = null!;

    // Action buttons & state
    private readonly Button _save;
    private readonly Button _apply;
    private readonly StackPanel _unsavedIndicator;

    // Layout containers
    private readonly Grid _rootGrid;
    private readonly Border _sidebarBorder;
    private readonly Border _footerBorder;
    private readonly TextBox _searchBox;
    private readonly TextBlock _categoryTitle;
    private readonly TextBlock _categoryDescription;
    private readonly ScrollViewer _contentScrollViewer;
    private readonly TextBlock _noSearchResults;

    private readonly List<Border> _cards = new();
    private readonly List<Border> _dividers = new();
    private readonly List<TextBlock> _textPrimaryElements = new();
    private readonly List<TextBlock> _textSecondaryElements = new();
    private readonly List<CategoryNavItem> _categoryNavItems = new();
    private readonly Dictionary<Border, SettingRowInfo> _rowInfo = new();
    private readonly Dictionary<Border, CardInfo> _cardInfo = new();
    private readonly List<(StackPanel Panel, Border Card, int Index)> _searchOrigin = new();
    private readonly StackPanel _searchResults = new() { Spacing = 0 };
    private readonly List<Border> _notices = new();
    private readonly List<Button> _secondaryButtons = new();
    private SettingsPalette _palette = SettingsPalette.Dark;
    private bool _searching;
    private SettingsCategory _currentCategory = SettingsCategory.General;

    private CompanionSettings _initialSettings = null!;
    private bool _allowClose;
    private bool _discardPromptOpen;

    public SettingsWindow(CompanionSettings settings, UiText text)
    {
        _settings = settings;
        _text = text;
        Title = text.SettingsTitle;
        Width = 780;
        Height = 640;
        MinWidth = 660;
        MinHeight = 500;
        CanResize = true;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // General controls (Modern ToggleSwitches)
        _systemTray = CreateToggleSwitch(settings.EnableSystemTray);
        var trayIconStyles = new[]
        {
            new TrayIconStyleChoice(TrayIconStyleOptions.Original, text.OriginalTrayIconStyle),
            new TrayIconStyleChoice(TrayIconStyleOptions.ClaudeCurrentSession, text.ClaudeCurrentTrayIconStyle),
            new TrayIconStyleChoice(TrayIconStyleOptions.ClaudeWeeklySession, text.ClaudeWeeklyTrayIconStyle),
            new TrayIconStyleChoice(TrayIconStyleOptions.CodexSession, text.CodexSessionTrayIconStyle)
        };
        var selectedTrayIconStyle = TrayIconStyleOptions.Normalize(settings.TrayIconStyle);
        _trayIconStyle = new ComboBox
        {
            ItemsSource = trayIconStyles,
            SelectedItem = trayIconStyles.First(choice => choice.Value == selectedTrayIconStyle),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _showTaskbarIcon = CreateToggleSwitch(settings.ShowTaskbarIcon);
        _startOnBoot = CreateToggleSwitch(settings.StartOnBoot);
        _minimizeOnStart = CreateToggleSwitch(settings.MinimizeOnStart);
        _startOnBoot.IsCheckedChanged += (_, _) => UpdateMinimizeOnStartControls();
        _alwaysOnTop = CreateToggleSwitch(settings.AlwaysOnTop);

        // Providers controls (Modern ToggleSwitches)
        _enableClaudeUsage = CreateToggleSwitch(settings.EnableClaudeUsage);
        _enableCodexUsage = CreateToggleSwitch(settings.EnableCodexUsage);
        _enableAntigravityUsage = CreateToggleSwitch(settings.EnableAntigravityUsage);
        _showClaudeSession = CreateToggleSwitch(settings.ShowClaudeSession);
        _showClaudeWeekly = CreateToggleSwitch(settings.ShowClaudeWeekly);
        _showCodexFiveHour = CreateToggleSwitch(settings.ShowCodexFiveHour);
        _showCodexWeekly = CreateToggleSwitch(settings.ShowCodexWeekly);

        _enableClaudeUsage.IsCheckedChanged += (_, _) => UpdateDisplayedLimitControls();
        _enableCodexUsage.IsCheckedChanged += (_, _) => UpdateDisplayedLimitControls();

        var updateIntervals = UpdateIntervalOptions.CommonValues
            .Append(UpdateIntervalOptions.Normalize(settings.RefreshIntervalSeconds))
            .Distinct()
            .Order()
            .Select(seconds => new UpdateIntervalChoice(seconds, text.FormatUpdateInterval(seconds)))
            .ToArray();
        var selectedUpdateInterval = UpdateIntervalOptions.Normalize(settings.RefreshIntervalSeconds);
        _updateInterval = new ComboBox
        {
            ItemsSource = updateIntervals,
            SelectedItem = updateIntervals.First(choice => choice.Seconds == selectedUpdateInterval),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        // Appearance controls
        var languages = new[]
        {
            new LanguageChoice("auto", text.SystemLanguage),
            new LanguageChoice("en-US", text.EnglishLanguage),
            new LanguageChoice("zh-tw", text.TraditionalChineseLanguage),
            new LanguageChoice("zh-cn", text.SimplifiedChineseLanguage)
        };
        var selectedLanguage = settings.Language?.ToLowerInvariant() switch
        {
            "zh-tw" => "zh-tw",
            "zh-cn" => "zh-cn",
            "auto" => "auto",
            _ => "en-US"
        };
        _language = new ComboBox
        {
            ItemsSource = languages,
            SelectedItem = languages.First(choice => choice.Value == selectedLanguage),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var themes = new[]
        {
            new ThemeChoice(UiThemeOptions.Dark, text.DarkTheme),
            new ThemeChoice(UiThemeOptions.Light, text.LightTheme),
            new ThemeChoice(UiThemeOptions.System, text.SystemTheme)
        };
        var selectedTheme = UiThemeOptions.Normalize(settings.Theme);
        _theme = new ComboBox
        {
            ItemsSource = themes,
            SelectedItem = themes.First(choice => choice.Value == selectedTheme),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _positionChoices = WindowPosition.Values
            .Select(value => new PositionChoice(value, text.FormatWindowPosition(value)))
            .ToArray();
        _position = new ComboBox
        {
            ItemsSource = _positionChoices,
            SelectedItem = _positionChoices.First(choice =>
                choice.Value == WindowPosition.Normalize(settings.Position)),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _position.SelectionChanged += (_, _) =>
        {
            if (_position.SelectedItem is PositionChoice choice)
            {
                UpdatePositionGridSelection(choice.Value);
                PositionPreviewRequested?.Invoke(choice.Value);
            }
        };
        _opacity = new Slider
        {
            Minimum = 50,
            Maximum = 100,
            SmallChange = 1,
            LargeChange = 5,
            TickFrequency = 5,
            IsSnapToTickEnabled = true,
            Value = Math.Round(Math.Clamp(settings.Opacity, 0.5, 1) * 100),
            Width = 170,
            VerticalAlignment = VerticalAlignment.Center
        };
        _opacityValue = new TextBlock
        {
            Text = $"{_opacity.Value:0}%",
            Width = 40,
            TextAlignment = TextAlignment.Right,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        _opacity.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == RangeBase.ValueProperty)
            {
                _opacityValue.Text = $"{_opacity.Value:0}%";
                PreviewAppearanceFromControls();
            }
        };
        _margin = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 64,
            Increment = 4,
            FormatString = "0",
            Value = Math.Clamp(settings.Margin, 0, 64),
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _margin.ValueChanged += (_, _) => PreviewAppearanceFromControls();

        // Notification controls
        _lowUsageAlert = CreateToggleSwitch(settings.EnableLowUsageAlert);
        _lowUsageAlertThreshold = new NumericUpDown
        {
            Value = UsageAlertOptions.NormalizeThreshold(settings.LowUsageAlertThresholdPercent),
            Minimum = UsageAlertOptions.MinimumThresholdPercent,
            Maximum = UsageAlertOptions.MaximumThresholdPercent,
            Increment = 1,
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _notifyOnReset = CreateToggleSwitch(settings.NotifyOnReset);
        _lowUsageAlert.IsCheckedChanged += (_, _) => UpdateLowUsageAlertControls();

        // DateTime controls
        var resetDateTimeFormat = DateTimeFormatOptions.NormalizeReset(settings.ResetDateTimeFormat);
        var resetDateTimeFormats = DateTimeFormatOptions.ResetFormats
            .Append(resetDateTimeFormat)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _resetDateTimeFormat = new ComboBox
        {
            ItemsSource = resetDateTimeFormats,
            SelectedItem = resetDateTimeFormat,
            Text = resetDateTimeFormat,
            IsEditable = true,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var lastUpdatedDateTimeFormat = DateTimeFormatOptions.NormalizeLastUpdated(settings.LastUpdatedDateTimeFormat);
        var lastUpdatedDateTimeFormats = DateTimeFormatOptions.LastUpdatedFormats
            .Append(lastUpdatedDateTimeFormat)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _lastUpdatedDateTimeFormat = new ComboBox
        {
            ItemsSource = lastUpdatedDateTimeFormats,
            SelectedItem = lastUpdatedDateTimeFormat,
            Text = lastUpdatedDateTimeFormat,
            IsEditable = true,
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _resetDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap
        };
        _lastUpdatedDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap
        };

        // Logging controls
        _usageLogging = CreateToggleSwitch(settings.EnableUsageLogging);
        _usageLogFilePath = new TextBox
        {
            Text = UsageLogOptions.NormalizeFilePath(settings.UsageLogFilePath, settings.UsageLogFormat),
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _usageLogFormat = new ComboBox
        {
            ItemsSource = UsageLogOptions.Formats,
            SelectedItem = UsageLogOptions.NormalizeFormat(settings.UsageLogFormat),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _usageLogFormat.SelectionChanged += (_, _) =>
        {
            if (_usageLogFormat.SelectedItem is not string newFormat)
            {
                return;
            }

            _usageLogFilePath.Text = UsageLogOptions.ChangeFileExtension(_usageLogFilePath.Text, newFormat);
        };
        var retentionChoices = UsageLogRetentionOptions.Choices
            .Append(UsageLogRetentionOptions.Normalize(settings.UsageLogRetentionDays))
            .Distinct()
            .OrderBy(days => days == UsageLogRetentionOptions.Forever ? int.MaxValue : days)
            .Select(days => new LogRetentionChoice(days, text.FormatUsageLogRetention(days)))
            .ToArray();
        var selectedRetention = UsageLogRetentionOptions.Normalize(settings.UsageLogRetentionDays);
        _usageLogRetention = new ComboBox
        {
            ItemsSource = retentionChoices,
            SelectedItem = retentionChoices.First(choice => choice.Days == selectedRetention),
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _usageLogging.IsCheckedChanged += (_, _) => UpdateUsageLoggingControls();

        // Footer buttons
        _save = new Button
        {
            Content = text.OkAction,
            MinWidth = 88,
            Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(6)
        };
        _save.Click += (_, _) => SaveAndClose();
        var cancel = new Button
        {
            Content = text.CancelAction,
            MinWidth = 88,
            Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(6)
        };
        cancel.Click += (_, _) => Close(null);
        _apply = new Button
        {
            Content = text.ApplyAction,
            MinWidth = 88,
            Height = 32,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(6)
        };
        _apply.Click += (_, _) => ApplyWithoutClosing();
        _secondaryButtons.Add(cancel);
        _secondaryButtons.Add(_apply);

        // Unsaved changes indicator
        var unsavedDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = SolidColorBrush.Parse("#F59E0B"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var unsavedText = new TextBlock
        {
            Text = text.UnsavedChangesNotice,
            FontSize = 12,
            Foreground = SolidColorBrush.Parse("#F59E0B"),
            VerticalAlignment = VerticalAlignment.Center
        };
        _unsavedIndicator = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
            IsVisible = false,
            Children = { unsavedDot, unsavedText }
        };

        // Wire dirty tracking to all input controls
        HookDirtyTracking();

        // Sidebar search box
        _searchBox = new TextBox
        {
            PlaceholderText = text.SearchSettingsPlaceholder,
            Margin = new Thickness(12, 12, 12, 8),
            CornerRadius = new CornerRadius(6),
            FontSize = 12
        };
        _searchBox.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == TextBox.TextProperty)
            {
                HandleSearchChanged();
            }
        };

        // Header and content canvas
        _categoryTitle = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        _categoryDescription = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        _noSearchResults = new TextBlock
        {
            Text = text.NoSettingsMatch,
            FontSize = 14,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
            IsVisible = false
        };

        // Build Category Panels with Option B Setting Rows & Option C Widgets
        BuildCategoryPanels();

        // Content ScrollViewer
        _contentScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 8, 0)
        };

        // Layout assembly
        var sidebarContent = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        sidebarContent.Children.Add(_searchBox);

        var navButtonsPanel = new StackPanel
        {
            Spacing = 3,
            Margin = new Thickness(8, 0, 8, 12)
        };
        foreach (var item in _categoryNavItems)
        {
            navButtonsPanel.Children.Add(item.Button);
        }
        var sidebarScrollViewer = new ScrollViewer
        {
            Content = navButtonsPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(sidebarScrollViewer, 1);
        sidebarContent.Children.Add(sidebarScrollViewer);

        _sidebarBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebarContent
        };

        var mainContentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Margin = new Thickness(24, 20, 24, 16)
        };
        mainContentGrid.Children.Add(_categoryTitle);
        Grid.SetRow(_categoryDescription, 1);
        mainContentGrid.Children.Add(_categoryDescription);
        Grid.SetRow(_noSearchResults, 2);
        mainContentGrid.Children.Add(_noSearchResults);
        Grid.SetRow(_contentScrollViewer, 2);
        mainContentGrid.Children.Add(_contentScrollViewer);

        var topBodyGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,*")
        };
        topBodyGrid.Children.Add(_sidebarBorder);
        Grid.SetColumn(mainContentGrid, 1);
        topBodyGrid.Children.Add(mainContentGrid);

        var footerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Margin = new Thickness(0, 0, 16, 0),
            Children = { _save, cancel, _apply }
        };
        var footerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Height = 54
        };
        footerGrid.Children.Add(_unsavedIndicator);
        Grid.SetColumn(footerActions, 1);
        footerGrid.Children.Add(footerActions);

        _footerBorder = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = footerGrid
        };

        _rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        _rootGrid.Children.Add(topBodyGrid);
        Grid.SetRow(_footerBorder, 1);
        _rootGrid.Children.Add(_footerBorder);

        Content = _rootGrid;

        _resetDateTimeFormat.PropertyChanged += HandleDateTimeFormatChanged;
        _lastUpdatedDateTimeFormat.PropertyChanged += HandleDateTimeFormatChanged;
        _resetDateTimeFormat.SelectionChanged += HandleDateTimeFormatSelectionChanged;
        _lastUpdatedDateTimeFormat.SelectionChanged += HandleDateTimeFormatSelectionChanged;

        UpdateUsageLoggingControls();
        UpdateLowUsageAlertControls();
        UpdateMinimizeOnStartControls();
        UpdateDisplayedLimitControls();
        UpdateDateTimeFormatValidation();

        _initialSettings = CaptureSettings();

        _usageLogFilePath.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == TextBox.TextProperty)
            {
                ScheduleHistoryFileInfoRefresh();
            }
        };
        _usageLogFormat.SelectionChanged += (_, _) => ScheduleHistoryFileInfoRefresh();
        _ = RefreshHistoryFileInfoAsync();
        RefreshDiagnostics();
        AddHandler(KeyDownEvent, HandleSearchShortcut, RoutingStrategies.Tunnel);

        SelectCategory(SettingsCategory.General);
        UpdatePositionGridSelection(WindowPosition.Normalize(settings.Position));
        ApplyThemePalette();

        ActualThemeVariantChanged += (_, _) => ApplyThemePalette();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Closing += HandleClosing;
    }

    public event Action<string>? PositionPreviewRequested;

    /// <summary>Opacity (0.5-1) and margin (px) chosen so far, shown live on the overlay.</summary>
    public event Action<double, int>? AppearancePreviewRequested;

    public event Action? TestNotificationRequested;

    public event Func<CompanionSettings, bool>? ApplyRequested;

    private static ToggleSwitch CreateToggleSwitch(bool isChecked)
    {
        return new ToggleSwitch
        {
            IsChecked = isChecked,
            OnContent = null,
            OffContent = null,
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void BuildCategoryPanels()
    {
        // 1. General
        _minimizeOnStartRow = CreateSettingRow(
            _text.MinimizeOnStartOption,
            _text.MinimizeOnStartDescription,
            _minimizeOnStart);

        var generalCard1 = CreateCardGroup(
            _text.TrayTaskbarGroup,
            _text.TrayTaskbarGroupDescription,
            CreateSettingRow(_text.SystemTrayOption, _text.SystemTrayDescription, _systemTray),
            CreateSettingRow(_text.TrayIconStyleOption, _text.TrayIconStyleDescription, _trayIconStyle),
            CreateSettingRow(_text.ShowTaskbarIconOption, _text.ShowTaskbarIconDescription, _showTaskbarIcon, showDivider: false));

        var generalCard2 = CreateCardGroup(
            _text.StartupBehaviorGroup,
            _text.StartupBehaviorGroupDescription,
            CreateSettingRow(_text.StartOnBootOption, _text.StartOnBootDescription, _startOnBoot),
            _minimizeOnStartRow,
            CreateSettingRow(_text.AlwaysOnTopOption, _text.AlwaysOnTopDescription, _alwaysOnTop, showDivider: false));

        var generalPanel = new StackPanel { Spacing = 12, Children = { generalCard1, generalCard2 } };

        // 2. Providers
        var claudeCard = CreateBrandedProviderCard(
            brandName: "Anthropic Claude",
            brandColorHex: "#EA580C",
            iconData: "M12 2L13.8 8.7L20.5 7L16.2 12L21.8 15.2L15.3 16.5L16.8 23L12 18.5L7.2 23L8.7 16.5L2.2 15.2L7.8 12L3.5 7L10.2 8.7L12 2Z",
            badgeText: _text.ProviderTagClaude,
            masterSwitch: _enableClaudeUsage,
            description: _text.ClaudeProviderDescription,
            out _claudeSubOptionsContainer,
            CreateSettingRow(_text.ShowClaudeSessionOption, _text.ClaudeSessionLimitDescription, _showClaudeSession),
            CreateSettingRow(_text.ShowClaudeWeeklyOption, _text.ClaudeWeeklyLimitDescription, _showClaudeWeekly, showDivider: false));

        var codexCard = CreateBrandedProviderCard(
            brandName: "OpenAI Codex",
            brandColorHex: "#10B981",
            iconData: "M12 2C6.48 2 2 6.48 2 12S6.48 22 12 22 22 17.52 22 12 17.52 2 12 2ZM12 4C16.42 4 20 7.58 20 12C20 13.91 19.33 15.66 18.21 17.03L6.97 5.79C8.34 4.67 10.09 4 12 4ZM4 12C4 10.09 4.67 8.34 5.79 6.97L17.03 18.21C15.66 19.33 13.91 20 12 20C7.58 20 4 16.42 4 12Z",
            badgeText: _text.ProviderTagCodex,
            masterSwitch: _enableCodexUsage,
            description: _text.CodexProviderDescription,
            out _codexSubOptionsContainer,
            CreateSettingRow(_text.ShowCodexFiveHourOption, _text.CodexFiveHourLimitDescription, _showCodexFiveHour),
            CreateSettingRow(_text.ShowCodexWeeklyOption, _text.CodexWeeklyLimitDescription, _showCodexWeekly, showDivider: false));

        var antigravityCard = CreateBrandedProviderCard(
            brandName: "Google Antigravity",
            brandColorHex: "#3B82F6",
            iconData: "M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z",
            badgeText: _text.ProviderTagAntigravity,
            masterSwitch: _enableAntigravityUsage,
            description: _text.AntigravityProviderDescription,
            out _,
            null);

        var pollingCard = CreateCardGroup(
            _text.RefreshGroup,
            null,
            CreateSettingRow(_text.UpdateIntervalOption, _text.RefreshIntervalDescription, _updateInterval, showDivider: false));

        var providersPanel = new StackPanel
        {
            Spacing = 12,
            Children = { claudeCard, codexCard, antigravityCard, pollingCard }
        };

        // 3. Appearance
        var appearanceCard1 = CreateCardGroup(
            _text.LanguageThemeGroup,
            _text.LanguageThemeGroupDescription,
            CreateSettingRow(_text.LanguageOption, _text.LanguageOptionDescription, _language),
            CreateSettingRow(_text.ThemeOption, _text.ThemeOptionDescription, _theme, showDivider: false));

        var opacityControl = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _opacity, _opacityValue }
        };
        var appearanceCard2 = CreateCardGroup(
            _text.OverlayPlacementGroup,
            _text.OverlayPlacementGroupDescription,
            CreateSettingRow(_text.PositionOption, _text.PositionHint, _position),
            CreatePositionGridWidget(),
            CreateSettingRow(_text.OpacityOption, _text.OpacityDescription, opacityControl),
            CreateSettingRow(_text.MarginOption, _text.MarginDescription, _margin, showDivider: false));

        var appearancePanel = new StackPanel { Spacing = 12, Children = { appearanceCard1, appearanceCard2 } };

        // 4. Notifications
        _lowUsageThresholdRow = CreateSettingRow(
            _text.LowUsageAlertThresholdOption,
            _text.LowUsageThresholdDescription,
            _lowUsageAlertThreshold);

        var notifyAvailable = LinuxDesktopNotificationService.IsAvailable();
        _testNotificationButton = CreateSecondaryButton(_text.TestNotificationAction);
        _testNotificationButton.IsEnabled = notifyAvailable;
        _testNotificationButton.Click += async (_, _) =>
        {
            TestNotificationRequested?.Invoke();
            _testNotificationButton.Content = $"✓ {_text.TestNotificationSentNotice}";
            await Task.Delay(1800);
            _testNotificationButton.Content = _text.TestNotificationAction;
        };

        var notificationsCard = CreateCardGroup(
            _text.NotificationAlertsGroup,
            _text.NotificationAlertsGroupDescription,
            CreateSettingRow(_text.LowUsageAlertOption, _text.LowUsageAlertDescription, _lowUsageAlert),
            _lowUsageThresholdRow,
            CreateSettingRow(_text.NotifyOnResetOption, _text.NotifyOnResetDescription, _notifyOnReset),
            CreateSettingRow(_text.TestNotificationOption, _text.TestNotificationDescription, _testNotificationButton, showDivider: false));

        _notifySendWarning = CreateNotice(_text.NotifySendMissingNotice);
        _notifySendWarning.IsVisible = !notifyAvailable;
        var notificationsPanel = new StackPanel
        {
            Spacing = 12,
            Children = { _notifySendWarning, notificationsCard }
        };

        // 5. Date & time
        var resetFormatRow = CreateSettingRow(
            _text.ResetDateTimeFormatOption,
            _text.ResetDateTimeFormatDescription,
            _resetDateTimeFormat);
        _resetFormatBadge = CreateFormatBadge(_resetDateTimeFormatStatus);
        var resetPresets = CreatePresetChipsPanel(
            _resetDateTimeFormat,
            [
                DateTimeFormatOptions.MonthDay,
                DateTimeFormatOptions.MonthDayTime,
                DateTimeFormatOptions.YearMonthDay,
                DateTimeFormatOptions.YearMonthDayTime
            ]);

        var lastUpdatedFormatRow = CreateSettingRow(
            _text.LastUpdatedDateTimeFormatOption,
            _text.LastUpdatedDateTimeFormatDescription,
            _lastUpdatedDateTimeFormat);
        _lastUpdatedFormatBadge = CreateFormatBadge(_lastUpdatedDateTimeFormatStatus);
        var lastUpdatedPresets = CreatePresetChipsPanel(
            _lastUpdatedDateTimeFormat,
            [
                DateTimeFormatOptions.HourMinute,
                DateTimeFormatOptions.HourMinuteSecond,
                DateTimeFormatOptions.MonthDayTime,
                DateTimeFormatOptions.YearMonthDayTime
            ]);

        var dateTimeCard1 = CreateCardGroup(
            _text.ResetFormatGroup,
            _text.ResetFormatGroupDescription,
            resetFormatRow,
            resetPresets,
            _resetFormatBadge);
        var dateTimeCard2 = CreateCardGroup(
            _text.LastUpdatedFormatGroup,
            _text.LastUpdatedFormatGroupDescription,
            lastUpdatedFormatRow,
            lastUpdatedPresets,
            _lastUpdatedFormatBadge);

        var dateTimePanel = new StackPanel { Spacing = 12, Children = { dateTimeCard1, dateTimeCard2 } };

        // 6. Logging
        var browseButton = CreateSecondaryButton(_text.BrowseFileAction);
        browseButton.Margin = new Thickness(8, 0, 0, 0);
        browseButton.Click += async (_, _) => await BrowseLogFilePathAsync();

        _usageLogFilePath.HorizontalAlignment = HorizontalAlignment.Stretch;
        var pathBoxGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        pathBoxGrid.Children.Add(_usageLogFilePath);
        Grid.SetColumn(browseButton, 1);
        pathBoxGrid.Children.Add(browseButton);

        _historyFileInfo = new TextBlock
        {
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _textSecondaryElements.Add(_historyFileInfo);
        _openFolderButton = CreateSecondaryButton(_text.OpenFolderAction);
        _openFolderButton.Click += (_, _) => OpenHistoryFolder();
        _clearHistoryButton = CreateSecondaryButton(_text.ClearHistoryAction);
        _clearHistoryButton.Click += async (_, _) => await ClearHistoryAsync();
        var historyActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { _openFolderButton, _clearHistoryButton }
        };
        var historyFooter = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetColumn(historyActions, 1);
        historyFooter.Children.Add(_historyFileInfo);
        historyFooter.Children.Add(historyActions);

        _usageLogFilePathRow = CreateStackedRow(
            _text.UsageLogFilePathOption,
            _text.UsageLogFilePathDescription,
            new StackPanel { Spacing = 0, Children = { pathBoxGrid, historyFooter } });

        _usageLogFormatRow = CreateSettingRow(
            _text.UsageLogFormatOption,
            _text.UsageLogFormatDescription,
            _usageLogFormat);

        _usageLogRetentionRow = CreateSettingRow(
            _text.UsageLogRetentionOption,
            _text.UsageLogRetentionDescription,
            _usageLogRetention,
            showDivider: false);

        var loggingCard = CreateCardGroup(
            _text.HistoryFileGroup,
            _text.HistoryFileGroupDescription,
            CreateSettingRow(_text.UsageLoggingOption, _text.UsageLoggingDescription, _usageLogging),
            _usageLogFilePathRow,
            _usageLogFormatRow,
            _usageLogRetentionRow);

        var loggingPanel = new StackPanel { Spacing = 12, Children = { loggingCard } };

        // 7. About
        var appVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var logoBadge = new Border
        {
            Width = 52,
            Height = 52,
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Image
            {
                Source = AppBranding.CreateLogoImage().Source,
                Width = 34,
                Height = 34,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _aboutLogoBadge = logoBadge;
        var productName = new TextBlock
        {
            Text = AppBranding.ProductName,
            FontSize = 18,
            FontWeight = FontWeight.Bold
        };
        _textPrimaryElements.Add(productName);
        var versionChip = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = $"v{appVersion}",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold
            }
        };
        _aboutVersionChip = versionChip;
        var aboutDescription = new TextBlock
        {
            Text = _text.AboutDescription,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap
        };
        _textSecondaryElements.Add(aboutDescription);
        var authorText = new TextBlock
        {
            Text = _text.AuthorInfo,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _textSecondaryElements.Add(authorText);
        var aboutIdentity = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 14,
            Children =
            {
                logoBadge,
                new StackPanel
                {
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { productName, versionChip }
                }
            }
        };
        Grid.SetColumn(aboutIdentity.Children[1], 1);
        var aboutCard = CreateCardGroup(
            null,
            null,
            aboutIdentity,
            new Border { Height = 10 },
            aboutDescription,
            authorText);

        var linksWrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, url) in new[]
                 {
                     (_text.ProjectPageAction, AppBranding.ProjectUrl),
                     (_text.ChangelogAction, AppBranding.ChangelogUrl),
                     (_text.ReportIssueAction, AppBranding.IssuesUrl),
                     (_text.LicenseAction, AppBranding.LicenseUrl),
                     (_text.OriginalProjectAction, AppBranding.OriginalProjectUrl)
                 })
        {
            var link = CreateSecondaryButton(label + "  ↗");
            link.Margin = new Thickness(0, 0, 8, 8);
            ToolTip.SetTip(link, url);
            link.Click += async (_, _) => await OpenUrlAsync(url);
            linksWrap.Children.Add(link);
        }
        var linksCard = CreateCardGroup(null, null, linksWrap);

        _diagnosticsBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            MinHeight = 178,
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_diagnosticsBox, ScrollBarVisibility.Auto);
        _copyDiagnosticsButton = CreateSecondaryButton(_text.CopyDiagnosticsAction);
        _copyDiagnosticsButton.HorizontalAlignment = HorizontalAlignment.Left;
        _copyDiagnosticsButton.Margin = new Thickness(0, 10, 0, 0);
        _copyDiagnosticsButton.Click += async (_, _) => await CopyDiagnosticsAsync();
        var diagnosticsCard = CreateCardGroup(
            _text.DiagnosticsGroup,
            _text.DiagnosticsDescription,
            _diagnosticsBox,
            _copyDiagnosticsButton);

        var aboutPanel = new StackPanel
        {
            Spacing = 12,
            Children = { aboutCard, linksCard, diagnosticsCard }
        };

        // Register category nav items. Their keywords also make every card on
        // the page findable from the search box.
        _categoryNavItems.Clear();
        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.General,
            _text.WindowSettingsGroup,
            _text.WindowSettingsDescription,
            "M2 4C2 2.9 2.9 2 4 2H20C21.1 2 22 2.9 22 4V20C21.1 2 20 22 20 22H4C2.9 22 2 21.1 2 20V4ZM4 6H20V4H4V6ZM4 8V20H20V8H4ZM6 10H10V12H6V10ZM6 14H14V16H6V14Z",
            ["tray", "taskbar", "boot", "startup", "minimize", "top", "always on top", "window", "system"],
            generalPanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Providers,
            _text.UsageSettingsGroup,
            _text.UsageSettingsDescription,
            "M3 17V19H9V17H3ZM3 5V7H13V5H3ZM13 21V19H21V17H13V15H11V21H13ZM7 9V11H3V13H7V15H9V9H7ZM21 13V11H11V13H21ZM17 9H21V7H17V5H15V11H17V9Z",
            ["claude", "codex", "antigravity", "provider", "limit", "rate limit", "session", "weekly", "5-hour", "refresh", "interval"],
            providersPanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Appearance,
            _text.AppearanceSettingsGroup,
            _text.AppearanceSettingsDescription,
            "M12 3C6.5 3 2 6.5 2 12C2 17.5 6.5 21 12 21C12.8 21 13.5 20.3 13.5 19.5C13.5 19.1 13.3 18.8 13.1 18.5C12.9 18.2 12.7 17.9 12.7 17.5C12.7 16.7 13.4 16 14.2 16H16C19.3 16 22 13.3 22 10C22 5.5 17.5 3 12 3ZM6.5 12C5.7 12 5 11.3 5 10.5C5 9.7 5.7 9 6.5 9C7.3 9 8 9.7 8 10.5C8 11.3 7.3 12 6.5 12ZM9.5 8C8.7 8 8 7.3 8 6.5C8 5.7 8.7 5 9.5 5C10.3 5 11 5.7 11 6.5C11 7.3 10.3 8 9.5 8ZM14.5 8C13.7 8 13 7.3 13 6.5C13 5.7 13.7 5 14.5 5C15.3 5 16 5.7 16 6.5C16 7.3 15.3 8 14.5 8ZM17.5 12C16.7 12 16 11.3 16 10.5C16 9.7 16.7 9 17.5 9C18.3 9 19 9.7 19 10.5C19 11.3 18.3 12 17.5 12Z",
            ["language", "theme", "dark", "light", "position", "overlay", "screen", "corner", "interface", "opacity", "margin", "transparent"],
            appearancePanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Notifications,
            _text.NotificationSettingsGroup,
            _text.NotificationSettingsDescription,
            "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.37 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.64 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16ZM16 17H8V11C8 8.52 9.51 6.5 12 6.5C14.49 6.5 16 8.52 16 11V17Z",
            ["alert", "notify", "reset", "threshold", "quota", "warning", "sound", "notification", "test"],
            notificationsPanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.DateTime,
            _text.DateTimeSettingsGroup,
            _text.DateTimeSettingsDescription,
            "M11.99 2C6.47 2 2 6.48 2 12C2 17.52 6.47 22 11.99 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 11.99 2ZM12 20C7.58 20 4 16.42 4 12C4 7.58 7.58 4 12 4C16.42 4 20 7.58 20 12C20 16.42 16.42 20 12 20ZM12.5 7H11V13L16.25 16.15L17 14.92L12.5 12.25V7Z",
            ["date", "time", "format", "timestamp", "clock", "reset time", "preview"],
            dateTimePanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Logging,
            _text.LoggingSettingsGroup,
            _text.LoggingSettingsDescription,
            "M14 2H6C4.9 2 4.01 2.9 4.01 4L4 20C4 21.1 4.89 22 5.99 22H18C19.1 22 20 21.1 20 20V8L14 2ZM16 18H8V16H16V18ZM16 14H8V12H16V14ZM13 9V3.5L18.5 9H13Z",
            ["log", "logging", "csv", "jsonl", "history", "export", "file", "path", "retention", "delete", "clear"],
            loggingPanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.About,
            _text.AboutSettingsGroup,
            _text.AboutSettingsDescription,
            "M12 2C6.48 2 2 6.48 2 12C2 17.52 6.48 22 12 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 12 2ZM13 17H11V11H13V17ZM13 9H11V7H13V9Z",
            ["version", "author", "about", "license", "info", "github", "release", "diagnostics", "support", "issue"],
            aboutPanel));

        foreach (var item in _categoryNavItems)
        {
            foreach (var card in item.ContentPanel.Children.OfType<Border>())
            {
                if (_cardInfo.TryGetValue(card, out var info))
                {
                    info.Keywords = string.Join(' ', item.Keywords);
                }
            }
        }
    }

    private Control CreatePositionGridWidget()
    {
        var monitorGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,*,*"),
            ColumnDefinitions = new ColumnDefinitions("*,*,*")
        };

        var positions = new[,]
        {
            { WindowPosition.LeftTop, WindowPosition.MiddleTop, WindowPosition.RightTop },
            { WindowPosition.LeftCenter, WindowPosition.MiddleCenter, WindowPosition.RightCenter },
            { WindowPosition.LeftBottom, WindowPosition.MiddleBottom, WindowPosition.RightBottom }
        };

        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var pos = positions[row, col];
                var tile = new Button
                {
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = pos
                };

                ToolTip.SetTip(tile, _text.FormatWindowPosition(pos));
                AutomationProperties.SetName(tile, _text.FormatWindowPosition(pos));
                tile.Click += (_, _) =>
                {
                    if ((_position.SelectedItem as PositionChoice)?.Value == pos)
                    {
                        PositionPreviewRequested?.Invoke(pos);
                    }
                    else
                    {
                        _position.SelectedItem = _positionChoices.First(choice => choice.Value == pos);
                    }
                    UpdatePositionGridSelection(pos);
                    UpdateDirtyState();
                };

                _positionGridButtons[pos] = tile;
                Grid.SetRow(tile, row);
                Grid.SetColumn(tile, col);
                monitorGrid.Children.Add(tile);
            }
        }

        _screenBorder = new Border
        {
            Width = 190,
            Height = 114,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            Padding = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = monitorGrid
        };

        _screenStandStem = new Border
        {
            Width = 24,
            Height = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _screenStandBase = new Border
        {
            Width = 64,
            Height = 4,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var widgetContainer = new StackPanel
        {
            Margin = new Thickness(0, 10, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _screenBorder, _screenStandStem, _screenStandBase }
        };

        var label = new TextBlock
        {
            Text = _text.ScreenPositionVisual,
            FontSize = 11.5,
            FontWeight = FontWeight.Medium,
            Opacity = 0.75,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _textSecondaryElements.Add(label);

        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
            Children = { label, widgetContainer }
        };
    }

    private void UpdatePositionGridSelection(string selectedPosition)
    {
        var normalized = WindowPosition.Normalize(selectedPosition);
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        var accentBrush = _palette.Accent;
        var activeText = Brushes.White;

        foreach (var (pos, btn) in _positionGridButtons)
        {
            var isSelected = string.Equals(pos, normalized, StringComparison.Ordinal);
            if (isSelected)
            {
                btn.Background = accentBrush;
                btn.BorderBrush = accentBrush;
                btn.Content = new Border
                {
                    Width = 10,
                    Height = 7,
                    CornerRadius = new CornerRadius(1.5),
                    Background = activeText
                };
            }
            else
            {
                btn.Background = _palette.Control;
                btn.BorderBrush = _palette.ControlBorder;
                btn.Content = new Border
                {
                    Width = 8,
                    Height = 5,
                    CornerRadius = new CornerRadius(1),
                    BorderThickness = new Thickness(1),
                    BorderBrush = SolidColorBrush.Parse(isLight ? "#94A3B8" : "#71717A")
                };
            }
        }
    }

    private async Task BrowseLogFilePathAsync()
    {
        try
        {
            if (StorageProvider.CanSave)
            {
                var isJsonl = string.Equals(
                    _usageLogFormat.SelectedItem as string,
                    UsageLogOptions.JsonLines,
                    StringComparison.OrdinalIgnoreCase);

                var ext = isJsonl ? "jsonl" : "csv";
                var filterName = isJsonl ? "JSON Lines (*.jsonl)" : "CSV Files (*.csv)";

                var currentDir = System.IO.Path.GetDirectoryName(_usageLogFilePath.Text);
                var suggestedDir = !string.IsNullOrEmpty(currentDir) && System.IO.Directory.Exists(currentDir)
                    ? await StorageProvider.TryGetFolderFromPathAsync(currentDir)
                    : null;

                var options = new FilePickerSaveOptions
                {
                    Title = _text.UsageLogFilePathOption,
                    DefaultExtension = ext,
                    SuggestedStartLocation = suggestedDir,
                    SuggestedFileName = System.IO.Path.GetFileName(_usageLogFilePath.Text) is { Length: > 0 } existing
                        ? existing
                        : $"usage-history.{ext}",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType(filterName)
                        {
                            Patterns = new[] { $"*.{ext}" }
                        }
                    }
                };

                var file = await StorageProvider.SaveFilePickerAsync(options);
                if (file is not null)
                {
                    _usageLogFilePath.Text = file.Path.LocalPath;
                    UpdateDirtyState();
                }
            }
        }
        catch
        {
            // Fallback gracefully if StorageProvider is unavailable
        }
    }

    private static Border CreateFormatBadge(TextBlock statusBlock)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10),
            Margin = new Thickness(0, 6, 0, 4),
            Child = statusBlock
        };
    }

    private Control CreatePresetChipsPanel(ComboBox targetComboBox, string[] presets)
    {
        var wrapPanel = new WrapPanel
        {
            Margin = new Thickness(0, 4, 0, 6),
            Orientation = Orientation.Horizontal
        };

        var label = new TextBlock
        {
            Text = $"{_text.QuickPresetsLabel}:",
            FontSize = 11,
            Opacity = 0.65,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 4)
        };
        _textSecondaryElements.Add(label);
        wrapPanel.Children.Add(label);

        foreach (var preset in presets)
        {
            var chip = new Button
            {
                Content = _text.PreviewDateTime(preset, DateTimeOffset.Now),
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                Padding = new Thickness(8, 3),
                Margin = new Thickness(0, 0, 6, 4),
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            _presetChips.Add(chip);
            ToolTip.SetTip(chip, preset);
            chip.Click += (_, _) =>
            {
                targetComboBox.SelectedItem = preset;
                targetComboBox.Text = preset;
                UpdateDateTimeFormatValidation();
                UpdateDirtyState();
            };
            wrapPanel.Children.Add(chip);
        }

        return wrapPanel;
    }

    private Border CreateSettingRow(string title, string? subtitle, Control control, bool showDivider = true)
    {
        var labelPanel = CreateRowLabel(title, subtitle);
        // A switch or slider has no visible text of its own; name it for
        // screen readers after the row it sits in.
        AutomationProperties.SetName(control, title);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Margin = new Thickness(0, 8, 0, 8)
        };
        grid.Children.Add(labelPanel);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return FinishRow(grid, title, subtitle, showDivider);
    }

    // Label on top, the control using the full width underneath. For wide
    // controls such as a file path that would be cramped beside the label.
    private Border CreateStackedRow(string title, string? subtitle, Control content, bool showDivider = true)
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 8),
            Children = { CreateRowLabel(title, subtitle), content }
        };
        return FinishRow(panel, title, subtitle, showDivider);
    }

    private StackPanel CreateRowLabel(string title, string? subtitle)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Medium,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _textPrimaryElements.Add(titleBlock);

        var labelPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        labelPanel.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subBlock = new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            _textSecondaryElements.Add(subBlock);
            labelPanel.Children.Add(subBlock);
        }

        return labelPanel;
    }

    private Border FinishRow(Control content, string title, string? subtitle, bool showDivider)
    {
        var rowContainer = new StackPanel();
        rowContainer.Children.Add(content);
        if (showDivider)
        {
            var divider = new Border { Height = 1 };
            _dividers.Add(divider);
            rowContainer.Children.Add(divider);
        }

        var row = new Border { Child = rowContainer };
        _rowInfo[row] = new SettingRowInfo($"{title} {subtitle}");
        return row;
    }

    private Button CreateSecondaryButton(string text)
    {
        var button = CreateSecondaryButtonCore(text);
        _secondaryButtons.Add(button);
        return button;
    }

    private static Button CreateSecondaryButtonCore(string text) => new()
    {
        Content = text,
        Height = 32,
        Padding = new Thickness(14, 0),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        CornerRadius = new CornerRadius(8),
        Cursor = new Cursor(StandardCursorType.Hand)
    };

    private Border CreateNotice(string message)
    {
        var text = new TextBlock
        {
            Text = message,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var icon = new TextBlock
        {
            Text = "⚠",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0)
        };
        var notice = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 10),
            Tag = (icon, text),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 10,
                Children = { icon, text }
            }
        };
        Grid.SetColumn(text, 1);
        _notices.Add(notice);
        return notice;
    }

    private void PreviewAppearanceFromControls() =>
        AppearancePreviewRequested?.Invoke(
            Math.Round(_opacity.Value / 100d, 2),
            (int)(_margin.Value ?? _settings.Margin));

    private Border CreateCardGroup(string? title, string? subtitle, params Control[] items)
    {
        var panel = new StackPanel { Spacing = 2 };

        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(subtitle) ? 6 : 2)
            };
            _textPrimaryElements.Add(titleBlock);
            panel.Children.Add(titleBlock);
        }

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subBlock = new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _textSecondaryElements.Add(subBlock);
            panel.Children.Add(subBlock);
        }

        var info = new CardInfo { Text = $"{title} {subtitle}" };
        foreach (var item in items)
        {
            panel.Children.Add(item);
            info.Items.Add(item);
            if (item is Border row && _rowInfo.ContainsKey(row))
            {
                info.Rows.Add(row);
            }
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 14),
            Margin = new Thickness(0, 0, 0, 2),
            Child = panel
        };
        _cards.Add(card);
        _cardInfo[card] = info;
        return card;
    }

    private Border CreateBrandedProviderCard(
        string brandName,
        string brandColorHex,
        string iconData,
        string badgeText,
        ToggleSwitch masterSwitch,
        string description,
        out Border subOptionsContainer,
        params Border[]? subOptionRows)
    {
        var icon = new AvaloniaPath
        {
            Data = Geometry.Parse(iconData),
            Width = 16,
            Height = 16,
            Fill = SolidColorBrush.Parse(brandColorHex),
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var titleBlock = new TextBlock
        {
            Text = brandName,
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        _textPrimaryElements.Add(titleBlock);

        var brandColor = Color.Parse(brandColorHex);
        var badge = new Border
        {
            Background = new SolidColorBrush(brandColor, 0.16),
            BorderBrush = new SolidColorBrush(brandColor, 0.45),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2),
            Margin = new Thickness(0, 5, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(brandColor)
            }
        };

        var providerTitle = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Children = { icon, titleBlock }
        };
        Grid.SetColumn(titleBlock, 1);
        var headerLeft = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { providerTitle, badge }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
        headerGrid.Children.Add(headerLeft);
        Grid.SetColumn(masterSwitch, 1);
        headerGrid.Children.Add(masterSwitch);

        var descBlock = new TextBlock
        {
            Text = description,
            FontSize = 11.5,
            Opacity = 0.65,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        _textSecondaryElements.Add(descBlock);

        var cardPanel = new StackPanel
        {
            Spacing = 2,
            Children = { headerGrid, descBlock }
        };

        if (subOptionRows is { Length: > 0 })
        {
            var subRowsPanel = new StackPanel
            {
                Spacing = 2
            };
            foreach (var row in subOptionRows)
            {
                subRowsPanel.Children.Add(row);
            }

            subOptionsContainer = new Border
            {
                BorderBrush = SolidColorBrush.Parse(brandColorHex),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(14, 2, 0, 2),
                Margin = new Thickness(4, 4, 0, 0),
                Child = subRowsPanel
            };
            cardPanel.Children.Add(subOptionsContainer);
        }
        else
        {
            subOptionsContainer = new Border { IsVisible = false };
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 14),
            Margin = new Thickness(0, 0, 0, 2),
            Child = cardPanel
        };
        _cards.Add(card);
        var info = new CardInfo { Text = $"{brandName} {badgeText} {description}" };
        if (subOptionRows is { Length: > 0 })
        {
            info.Rows.AddRange(subOptionRows);
        }

        _cardInfo[card] = info;
        return card;
    }

    private CategoryNavItem CreateCategoryNavItem(
        SettingsCategory category,
        string title,
        string description,
        string iconData,
        string[] keywords,
        StackPanel contentPanel)
    {
        var indicator = new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(1.5),
            Background = SolidColorBrush.Parse("#FF3DD598"),
            Margin = new Thickness(0, 0, 8, 0),
            IsVisible = false
        };

        var iconPath = new AvaloniaPath
        {
            Data = Geometry.Parse(iconData),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };
        contentGrid.Children.Add(indicator);
        Grid.SetColumn(iconPath, 1);
        contentGrid.Children.Add(iconPath);
        Grid.SetColumn(titleBlock, 2);
        contentGrid.Children.Add(titleBlock);

        var button = new Button
        {
            Content = contentGrid,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(6)
        };
        button.Click += (_, _) => SelectCategory(category);

        return new CategoryNavItem
        {
            Category = category,
            Title = title,
            Description = description,
            IconData = iconData,
            Keywords = keywords,
            Button = button,
            Indicator = indicator,
            IconPath = iconPath,
            TitleBlock = titleBlock,
            ContentPanel = contentPanel
        };
    }

    private void SelectCategory(SettingsCategory category)
    {
        _currentCategory = category;
        var selectedItem = _categoryNavItems.FirstOrDefault(c => c.Category == category);
        if (selectedItem is null)
        {
            return;
        }

        _categoryTitle.Text = selectedItem.Title;
        _categoryDescription.Text = selectedItem.Description;
        _contentScrollViewer.Content = selectedItem.ContentPanel;
        _contentScrollViewer.Offset = new Vector(0, 0);

        UpdateNavSelectionStyles();
    }

    private void UpdateNavSelectionStyles()
    {
        var normalIcon = _palette.TextSecondary;
        foreach (var item in _categoryNavItems)
        {
            // While searching, results from every page are shown together, so
            // no single page is "selected".
            var isSelected = !_searching && item.Category == _currentCategory;
            item.Indicator.IsVisible = isSelected;
            item.Indicator.Background = _palette.Accent;
            item.Button.Background = isSelected ? _palette.AccentSoft : Brushes.Transparent;
            item.TitleBlock.FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal;
            item.TitleBlock.Foreground = isSelected ? _palette.Accent : _palette.TextPrimary;
            item.IconPath.Fill = isSelected ? _palette.Accent : normalIcon;
        }
    }

    private void HandleSearchChanged()
    {
        var query = (_searchBox.Text ?? string.Empty).Trim();
        RestoreSearchedCards();
        if (string.IsNullOrEmpty(query))
        {
            _searching = false;
            _noSearchResults.IsVisible = false;
            _contentScrollViewer.IsVisible = true;
            SelectCategory(_currentCategory);
            return;
        }

        _searching = true;
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var matches = 0;
        foreach (var item in _categoryNavItems)
        {
            var cards = item.ContentPanel.Children.OfType<Border>().ToList();
            var shown = new List<Border>();
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (!_cardInfo.TryGetValue(card, out var info))
                {
                    continue;
                }

                var cardMatches = Matches(info.Text, terms) || Matches(info.Keywords, terms);
                var visibleRows = 0;
                foreach (var row in info.Rows)
                {
                    var rowMatches = cardMatches ||
                        (_rowInfo.TryGetValue(row, out var rowInfo) && Matches(rowInfo.Text, terms));
                    row.IsVisible = rowMatches;
                    visibleRows += rowMatches ? 1 : 0;
                }

                var cardShown = info.Rows.Count == 0 ? cardMatches : visibleRows > 0;
                if (!cardShown)
                {
                    continue;
                }

                // Controls that are not rows (a preview widget, format chips)
                // belong to the card and stay with it.
                foreach (var extra in info.Items.Where(extra => extra is not Border b || !info.Rows.Contains(b)))
                {
                    extra.IsVisible = true;
                }

                matches += info.Rows.Count == 0 ? 1 : visibleRows;
                shown.Add(card);
                _searchOrigin.Add((item.ContentPanel, card, index));
            }

            if (shown.Count == 0)
            {
                continue;
            }

            var heading = new TextBlock
            {
                Text = item.Title,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(2, _searchResults.Children.Count == 0 ? 0 : 14, 0, 6)
            };
            _textSecondaryElements.Add(heading);
            heading.Foreground = _palette.TextSecondary;
            _searchResults.Children.Add(heading);
            foreach (var card in shown)
            {
                item.ContentPanel.Children.Remove(card);
                card.Margin = new Thickness(0, 0, 0, 10);
                _searchResults.Children.Add(card);
            }
        }

        _categoryTitle.Text = _text.SearchResultsHeading;
        _categoryDescription.Text = $"“{query}”";
        _noSearchResults.Text = _text.NoSettingsMatch;
        _noSearchResults.IsVisible = matches == 0;
        _contentScrollViewer.IsVisible = matches > 0;
        _contentScrollViewer.Content = _searchResults;
        _contentScrollViewer.Offset = new Vector(0, 0);
        UpdateNavSelectionStyles();
    }

    private static bool Matches(string text, string[] terms) =>
        terms.All(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    // Put every card that search pulled out back where it came from, and make
    // every row visible again.
    private void RestoreSearchedCards()
    {
        foreach (var (panel, card, index) in _searchOrigin)
        {
            _searchResults.Children.Remove(card);
            panel.Children.Insert(Math.Min(index, panel.Children.Count), card);
            card.Margin = new Thickness(0, 0, 0, 2);
        }

        _searchOrigin.Clear();
        foreach (var heading in _searchResults.Children.OfType<TextBlock>().ToList())
        {
            _textSecondaryElements.Remove(heading);
        }

        _searchResults.Children.Clear();
        foreach (var info in _cardInfo.Values)
        {
            foreach (var row in info.Rows)
            {
                row.IsVisible = true;
            }
        }
    }

    private void ApplyThemePalette()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        _palette = isLight ? SettingsPalette.Light : SettingsPalette.Dark;

        Background = _palette.Window;
        _sidebarBorder.Background = _palette.Sidebar;
        _sidebarBorder.BorderBrush = _palette.SidebarBorder;
        _footerBorder.Background = _palette.Sidebar;
        _footerBorder.BorderBrush = _palette.SidebarBorder;

        _categoryTitle.Foreground = _palette.TextPrimary;
        _categoryDescription.Foreground = _palette.TextSecondary;
        _noSearchResults.Foreground = _palette.TextSecondary;

        _save.Background = _palette.PrimaryButton;
        _save.Foreground = Brushes.White;

        // Fluent paints toggles, sliders and focus rings with the system
        // accent; point them at the app's own so controls match the chrome.
        var accent = (SolidColorBrush)_palette.Accent;
        foreach (var key in new[]
                 {
                     "ToggleSwitchFillOn", "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed",
                     "ToggleSwitchStrokeOn", "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchStrokeOnPressed",
                     "SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
                     "SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed",
                     "TextControlBorderBrushFocused", "ComboBoxBorderBrushFocused"
                 })
        {
            Resources[key] = accent;
        }

        Resources["ToggleSwitchKnobFillOn"] = isLight ? Brushes.White : SolidColorBrush.Parse("#FF0F1A15");
        Resources["ToggleSwitchKnobFillOnPointerOver"] = Resources["ToggleSwitchKnobFillOn"];
        Resources["ToggleSwitchKnobFillOnPressed"] = Resources["ToggleSwitchKnobFillOn"];

        foreach (var card in _cards)
        {
            card.Background = _palette.Card;
            card.BorderBrush = _palette.CardBorder;
        }

        foreach (var divider in _dividers)
        {
            divider.Background = _palette.Divider;
        }

        foreach (var el in _textPrimaryElements)
        {
            el.Foreground = _palette.TextPrimary;
        }

        foreach (var el in _textSecondaryElements)
        {
            el.Foreground = _palette.TextSecondary;
        }

        foreach (var button in _secondaryButtons)
        {
            button.Background = _palette.Control;
            button.BorderBrush = _palette.ControlBorder;
            button.Foreground = _palette.TextPrimary;
            button.BorderThickness = new Thickness(1);
        }

        foreach (var notice in _notices)
        {
            notice.Background = new SolidColorBrush(_palette.NoticeBackground);
            notice.BorderBrush = new SolidColorBrush(_palette.NoticeBorder);
            if (notice.Tag is ValueTuple<TextBlock, TextBlock> parts)
            {
                parts.Item1.Foreground = _palette.NoticeText;
                parts.Item2.Foreground = _palette.NoticeText;
            }
        }

        if (_screenBorder is not null)
        {
            _screenBorder.Background = isLight ? SolidColorBrush.Parse("#FFF8FAFC") : SolidColorBrush.Parse("#FF101315");
            _screenBorder.BorderBrush = isLight ? SolidColorBrush.Parse("#FF94A3B8") : SolidColorBrush.Parse("#FF4B5563");
            _screenStandStem.Background = isLight ? SolidColorBrush.Parse("#FFCBD5E1") : SolidColorBrush.Parse("#FF374151");
            _screenStandBase.Background = isLight ? SolidColorBrush.Parse("#FF94A3B8") : SolidColorBrush.Parse("#FF4B5563");
        }

        foreach (var chip in _presetChips)
        {
            chip.BorderBrush = _palette.ControlBorder;
            chip.Foreground = _palette.TextSecondary;
        }

        if (_aboutLogoBadge is not null)
        {
            _aboutLogoBadge.Background = _palette.AccentSoft;
            _aboutVersionChip.Background = _palette.AccentSoft;
            ((TextBlock)_aboutVersionChip.Child!).Foreground = _palette.Accent;
        }

        _diagnosticsBox.Background = _palette.Window;
        _diagnosticsBox.BorderBrush = _palette.CardBorder;
        _diagnosticsBox.Foreground = _palette.TextPrimary;
        _opacityValue.Foreground = _palette.TextPrimary;
        _unsavedIndicator.Opacity = 1;

        UpdateNavSelectionStyles();
        if (_position.SelectedItem is PositionChoice currentPosition)
        {
            UpdatePositionGridSelection(currentPosition.Value);
        }

        UpdateDateTimeFormatValidation();
    }

    private void HookDirtyTracking()
    {
        _systemTray.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _trayIconStyle.SelectionChanged += (_, _) => UpdateDirtyState();
        _showTaskbarIcon.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _startOnBoot.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _minimizeOnStart.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _alwaysOnTop.IsCheckedChanged += (_, _) => UpdateDirtyState();

        _enableClaudeUsage.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _enableCodexUsage.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _enableAntigravityUsage.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _showClaudeSession.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _showClaudeWeekly.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _showCodexFiveHour.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _showCodexWeekly.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _updateInterval.SelectionChanged += (_, _) => UpdateDirtyState();

        _language.SelectionChanged += (_, _) => UpdateDirtyState();
        _theme.SelectionChanged += (_, _) => UpdateDirtyState();
        _position.SelectionChanged += (_, _) => UpdateDirtyState();
        _opacity.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == RangeBase.ValueProperty)
            {
                UpdateDirtyState();
            }
        };
        _margin.ValueChanged += (_, _) => UpdateDirtyState();

        _lowUsageAlert.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _lowUsageAlertThreshold.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == NumericUpDown.ValueProperty)
            {
                UpdateDirtyState();
            }
        };
        _notifyOnReset.IsCheckedChanged += (_, _) => UpdateDirtyState();

        _resetDateTimeFormat.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == ComboBox.TextProperty)
            {
                UpdateDirtyState();
            }
        };
        _lastUpdatedDateTimeFormat.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == ComboBox.TextProperty)
            {
                UpdateDirtyState();
            }
        };

        _usageLogging.IsCheckedChanged += (_, _) => UpdateDirtyState();
        _usageLogFilePath.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.Property == TextBox.TextProperty)
            {
                UpdateDirtyState();
            }
        };
        _usageLogFormat.SelectionChanged += (_, _) => UpdateDirtyState();
        _usageLogRetention.SelectionChanged += (_, _) => UpdateDirtyState();
    }

    private void UpdateDirtyState()
    {
        if (_initialSettings is null)
        {
            return;
        }

        var isDirty = CaptureSettings() != _initialSettings;
        _unsavedIndicator.IsVisible = isDirty;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            Close(null);
            return;
        }

        if (eventArgs.Key != Key.S ||
            (eventArgs.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        eventArgs.Handled = true;
        SaveAndClose();
    }

    private void HandleClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_allowClose ||
            eventArgs.CloseReason == WindowCloseReason.OSShutdown ||
            CaptureSettings() == _initialSettings)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (!_discardPromptOpen)
        {
            _ = ConfirmDiscardChangesAsync();
        }
    }

    private async Task ConfirmDiscardChangesAsync()
    {
        _discardPromptOpen = true;
        try
        {
            var discard = await ConfirmAsync(
                _text.UnsavedChangesTitle,
                _text.UnsavedChangesMessage,
                _text.DiscardChangesAction,
                _text.KeepEditingAction);
            if (!discard)
            {
                return;
            }

            _allowClose = true;
            Close(null);
        }
        finally
        {
            _discardPromptOpen = false;
        }
    }

    private void SaveAndClose()
    {
        if (!UpdateDateTimeFormatValidation())
        {
            return;
        }

        _allowClose = true;
        Close(CaptureSettings());
    }

    private void ApplyWithoutClosing()
    {
        if (!UpdateDateTimeFormatValidation())
        {
            return;
        }

        var settings = CaptureSettings();
        if (ApplyRequested?.Invoke(settings) != true)
        {
            return;
        }

        _initialSettings = settings;
        UpdateDirtyState();
    }

    private CompanionSettings CaptureSettings()
    {
        var settings = ApplyUsageProviderEnablement(
            _settings,
            _enableClaudeUsage.IsChecked == true,
            _enableCodexUsage.IsChecked == true,
            _enableAntigravityUsage.IsChecked == true);
        return settings with
        {
            EnableSystemTray = _systemTray.IsChecked == true,
            TrayIconStyle =
                (_trayIconStyle.SelectedItem as TrayIconStyleChoice)?.Value ??
                TrayIconStyleOptions.Original,
            ShowTaskbarIcon = _showTaskbarIcon.IsChecked == true,
            StartOnBoot = _startOnBoot.IsChecked == true,
            MinimizeOnStart = _minimizeOnStart.IsChecked == true,
            AlwaysOnTop = _alwaysOnTop.IsChecked == true,
            ShowClaudeSession = _showClaudeSession.IsChecked == true,
            ShowClaudeWeekly = _showClaudeWeekly.IsChecked == true,
            ShowCodexFiveHour = _showCodexFiveHour.IsChecked == true,
            ShowCodexWeekly = _showCodexWeekly.IsChecked == true,
            Language = (_language.SelectedItem as LanguageChoice)?.Value ?? "en-US",
            Theme = (_theme.SelectedItem as ThemeChoice)?.Value ?? UiThemeOptions.System,
            Position = (_position.SelectedItem as PositionChoice)?.Value ?? WindowPosition.RightBottom,
            Opacity = Math.Round(_opacity.Value / 100d, 2),
            Margin = (int)(_margin.Value ?? _settings.Margin),
            RefreshIntervalSeconds =
                (_updateInterval.SelectedItem as UpdateIntervalChoice)?.Seconds ??
                UpdateIntervalOptions.MinimumSeconds,
            EnableLowUsageAlert = _lowUsageAlert.IsChecked == true,
            LowUsageAlertThresholdPercent = (int)(
                _lowUsageAlertThreshold.Value ??
                UsageAlertOptions.DefaultThresholdPercent),
            NotifyOnReset = _notifyOnReset.IsChecked == true,
            ResetDateTimeFormat = CurrentFormat(_resetDateTimeFormat),
            LastUpdatedDateTimeFormat = CurrentFormat(_lastUpdatedDateTimeFormat),
            EnableUsageLogging = _usageLogging.IsChecked == true,
            UsageLogFilePath = _usageLogFilePath.Text ?? string.Empty,
            UsageLogFormat =
                _usageLogFormat.SelectedItem as string ?? UsageLogOptions.Csv,
            UsageLogRetentionDays =
                (_usageLogRetention.SelectedItem as LogRetentionChoice)?.Days ??
                UsageLogRetentionOptions.DefaultDays
        };
    }

    internal static CompanionSettings ApplyUsageProviderEnablement(
        CompanionSettings settings,
        bool enableClaudeUsage,
        bool enableCodexUsage,
        bool enableAntigravityUsage)
    {
        return settings with
        {
            EnableClaudeUsage = enableClaudeUsage,
            EnableCodexUsage = enableCodexUsage,
            EnableAntigravityUsage = enableAntigravityUsage
        };
    }

    private void UpdateUsageLoggingControls()
    {
        var enabled = _usageLogging.IsChecked == true;
        _usageLogFilePathRow.IsEnabled = enabled;
        _usageLogFilePathRow.Opacity = enabled ? 1.0 : 0.45;
        _usageLogFormatRow.IsEnabled = enabled;
        _usageLogFormatRow.Opacity = enabled ? 1.0 : 0.45;
        _usageLogRetentionRow.IsEnabled = enabled;
        _usageLogRetentionRow.Opacity = enabled ? 1.0 : 0.45;
        _usageLogFilePath.IsEnabled = enabled;
        _usageLogFormat.IsEnabled = enabled;
        _usageLogRetention.IsEnabled = enabled;
    }

    private void UpdateLowUsageAlertControls()
    {
        var alertEnabled = _lowUsageAlert.IsChecked == true;
        _lowUsageThresholdRow.IsEnabled = alertEnabled;
        _lowUsageThresholdRow.Opacity = alertEnabled ? 1.0 : 0.45;
        _lowUsageAlertThreshold.IsEnabled = alertEnabled;
    }

    private void UpdateDisplayedLimitControls()
    {
        var claudeEnabled = _enableClaudeUsage.IsChecked == true;
        _claudeSubOptionsContainer.IsEnabled = claudeEnabled;
        _claudeSubOptionsContainer.Opacity = claudeEnabled ? 1.0 : 0.45;
        _showClaudeSession.IsEnabled = claudeEnabled;
        _showClaudeWeekly.IsEnabled = claudeEnabled;

        var codexEnabled = _enableCodexUsage.IsChecked == true;
        _codexSubOptionsContainer.IsEnabled = codexEnabled;
        _codexSubOptionsContainer.Opacity = codexEnabled ? 1.0 : 0.45;
        _showCodexFiveHour.IsEnabled = codexEnabled;
        _showCodexWeekly.IsEnabled = codexEnabled;
    }

    private void UpdateMinimizeOnStartControls()
    {
        _minimizeOnStartRow.IsVisible = _startOnBoot.IsChecked == true;
    }

    private void HandleDateTimeFormatChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == ComboBox.TextProperty)
        {
            UpdateDateTimeFormatValidation();
        }
    }

    private void HandleDateTimeFormatSelectionChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is string selected)
        {
            comboBox.Text = selected;
        }

        UpdateDateTimeFormatValidation();
    }

    private bool UpdateDateTimeFormatValidation()
    {
        var now = DateTimeOffset.Now;
        var resetValid = UpdateDateTimeFormatStatus(
            CurrentFormat(_resetDateTimeFormat),
            _resetDateTimeFormatStatus,
            _resetFormatBadge,
            now,
            format =>
            {
                // How a countdown and its reset moment read together on a card.
                var preview = UiText.For(_text.Language, format, _text.LastUpdatedDateTimeFormat);
                var reset = now.AddDays(2).AddHours(3);
                return $"{preview.FormatResetsIn(reset, now)}  ·  {preview.FormatResetMoment(reset, now)}";
            });
        var lastUpdatedValid = UpdateDateTimeFormatStatus(
            CurrentFormat(_lastUpdatedDateTimeFormat),
            _lastUpdatedDateTimeFormatStatus,
            _lastUpdatedFormatBadge,
            now,
            format => UiText.For(_text.Language, _text.ResetDateTimeFormat, format).FormatUpdatedTime(now));
        _save.IsEnabled = resetValid && lastUpdatedValid;
        _apply.IsEnabled = _save.IsEnabled;
        return _save.IsEnabled;
    }

    private bool UpdateDateTimeFormatStatus(
        string format,
        TextBlock status,
        Border? badge,
        DateTimeOffset sample,
        Func<string, string> inContext)
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;

        if (_text.TryFormatDateTime(sample, format, out var preview))
        {
            status.Text = $"✓ {_text.FormatPreview}: {preview}\n{inContext(format)}";
            status.Foreground = SolidColorBrush.Parse(isLight ? "#FF0F7A55" : "#FF3DD598");

            if (badge is not null)
            {
                badge.Background = SolidColorBrush.Parse(isLight ? "#FFEAF7F1" : "#1F3DD598");
                badge.BorderBrush = SolidColorBrush.Parse(isLight ? "#FFB5E2CD" : "#663DD598");
            }

            return true;
        }

        status.Text = $"⚠ {_text.InvalidDateTimeFormat}";
        status.Foreground = SolidColorBrush.Parse(isLight ? "#FFC62828" : "#FFFF8A80");

        if (badge is not null)
        {
            badge.Background = SolidColorBrush.Parse(isLight ? "#FFFDEEEE" : "#26FF6B6B");
            badge.BorderBrush = SolidColorBrush.Parse(isLight ? "#FFF3C4C4" : "#59FF6B6B");
        }

        return false;
    }

    private static string CurrentFormat(ComboBox comboBox)
    {
        return (comboBox.Text ?? comboBox.SelectedItem as string ?? string.Empty).Trim();
    }

    private void HandleSearchShortcut(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.F && (eventArgs.KeyModifiers & KeyModifiers.Control) != 0)
        {
            eventArgs.Handled = true;
            _searchBox.Focus();
            _searchBox.SelectAll();
        }
    }

    // ---- Usage history file (Logging page) --------------------------------

    private string SelectedLogFormat() =>
        _usageLogFormat.SelectedItem as string ?? UsageLogOptions.Csv;

    private string CurrentLogPath() =>
        UsageLogOptions.NormalizeFilePath(_usageLogFilePath.Text, SelectedLogFormat());

    private DispatcherTimer? _historyInfoTimer;

    // Typing a path fires a change per key; wait for a pause before touching the disk.
    private void ScheduleHistoryFileInfoRefresh()
    {
        _historyInfoTimer?.Stop();
        _historyInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _historyInfoTimer.Tick += (_, _) =>
        {
            _historyInfoTimer?.Stop();
            _ = RefreshHistoryFileInfoAsync();
        };
        _historyInfoTimer.Start();
    }

    private async Task RefreshHistoryFileInfoAsync()
    {
        var version = ++_historyInfoVersion;
        var path = CurrentLogPath();
        var info = await Task.Run(() => ReadHistoryFileInfo(path));
        if (version != _historyInfoVersion)
        {
            return;
        }

        _historyFileInfo.Text = info is { } value
            ? _text.FormatHistoryFileInfo(value.Bytes, value.Records)
            : _text.NoHistoryFileYet;
        _clearHistoryButton.IsEnabled = info is not null;
        var directory = System.IO.Path.GetDirectoryName(path);
        _openFolderButton.IsEnabled = !string.IsNullOrEmpty(directory) && System.IO.Directory.Exists(directory);
        ToolTip.SetTip(_historyFileInfo, path);
    }

    // Size and record count without parsing: records are lines, less the CSV header.
    internal static (long Bytes, int Records)? ReadHistoryFileInfo(string path)
    {
        try
        {
            var file = new System.IO.FileInfo(path);
            if (!file.Exists)
            {
                return null;
            }

            var lines = 0;
            using (var stream = new System.IO.FileStream(
                       path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var index = 0; index < read; index++)
                    {
                        if (buffer[index] == (byte)10)
                        {
                            lines++;
                        }
                    }
                }
            }

            var isCsv = string.Equals(
                System.IO.Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase);
            return (file.Length, Math.Max(0, isCsv ? lines - 1 : lines));
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void OpenHistoryFolder()
    {
        var directory = System.IO.Path.GetDirectoryName(CurrentLogPath());
        if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory))
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo { FileName = "xdg-open", UseShellExecute = false };
            startInfo.ArgumentList.Add(directory);
            using var process = Process.Start(startInfo);
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            CompanionLog.Shared.Write("settings-open-folder", exception);
        }
    }

    private async Task ClearHistoryAsync()
    {
        var path = CurrentLogPath();
        var info = ReadHistoryFileInfo(path);
        if (info is not { } value)
        {
            await RefreshHistoryFileInfoAsync();
            return;
        }

        var confirmed = await ConfirmAsync(
            _text.ClearHistoryConfirmTitle,
            _text.FormatClearHistoryConfirm(path, _text.FormatHistoryFileInfo(value.Bytes, value.Records)),
            _text.ClearHistoryConfirmAction);
        if (!confirmed)
        {
            return;
        }

        try
        {
            System.IO.File.Delete(path);
            UsageHistoryReader.ClearCache();
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            CompanionLog.Shared.Write("settings-clear-history", exception);
            _historyFileInfo.Text = exception.Message;
            return;
        }

        await RefreshHistoryFileInfoAsync();
    }

    private async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmLabel,
        string? cancelLabel = null)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = _palette.Window
        };
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = _palette.TextPrimary
        };
        var body = new TextBlock
        {
            Text = message,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _palette.TextSecondary
        };
        var cancel = CreateSecondaryButtonCore(cancelLabel ?? _text.CancelAction);
        cancel.MinWidth = 96;
        cancel.Background = _palette.Control;
        cancel.BorderBrush = _palette.ControlBorder;
        cancel.Foreground = _palette.TextPrimary;
        cancel.BorderThickness = new Thickness(1);
        cancel.Click += (_, _) => dialog.Close(false);
        var confirm = CreateSecondaryButtonCore(confirmLabel);
        confirm.MinWidth = 96;
        confirm.Background = SolidColorBrush.Parse("#FFC0392B");
        confirm.Foreground = Brushes.White;
        confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(24, 22, 24, 20),
            Children =
            {
                heading,
                body,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(0, 12, 0, 0),
                    Children = { cancel, confirm }
                }
            }
        };
        dialog.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key == Key.Escape)
            {
                eventArgs.Handled = true;
                dialog.Close(false);
            }
        };
        return await dialog.ShowDialog<bool>(this);
    }

    // ---- About page --------------------------------------------------------

    private void RefreshDiagnostics()
    {
        var version = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        long? logBytes = null;
        try
        {
            var path = UsageLogOptions.NormalizeFilePath(_settings.UsageLogFilePath, _settings.UsageLogFormat);
            if (System.IO.File.Exists(path))
            {
                logBytes = new System.IO.FileInfo(path).Length;
            }
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            // The size is a nicety for the report; leave it out.
        }

        _diagnosticsBox.Text = SettingsDiagnostics.Build(_settings, version, logBytes);
    }

    private async Task CopyDiagnosticsAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(DataTransferItem.CreateText(_diagnosticsBox.Text ?? string.Empty));
        await clipboard.SetDataAsync(dataTransfer);
        _copyDiagnosticsButton.Content = $"✓ {_text.CopiedNotice}";
        await Task.Delay(1800);
        _copyDiagnosticsButton.Content = _text.CopyDiagnosticsAction;
    }

    private async Task OpenUrlAsync(string url)
    {
        try
        {
            var launcher = TopLevel.GetTopLevel(this)?.Launcher;
            if (launcher is not null)
            {
                await launcher.LaunchUriAsync(new Uri(url));
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or UriFormatException)
        {
            CompanionLog.Shared.Write("settings-open-url", exception);
        }
    }

    private sealed record LanguageChoice(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record ThemeChoice(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record TrayIconStyleChoice(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record LogRetentionChoice(int Days, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record UpdateIntervalChoice(int Seconds, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
