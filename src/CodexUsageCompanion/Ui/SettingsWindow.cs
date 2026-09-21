using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;
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

    private readonly CompanionSettings _settings;
    private readonly UiText _text;
    private readonly CheckBox _systemTray;
    private readonly ComboBox _trayIconStyle;
    private readonly CheckBox _showTaskbarIcon;
    private readonly CheckBox _startOnBoot;
    private readonly CheckBox _minimizeOnStart;
    private readonly CheckBox _alwaysOnTop;
    private readonly CheckBox _enableClaudeUsage;
    private readonly CheckBox _enableCodexUsage;
    private readonly CheckBox _enableAntigravityUsage;
    private readonly CheckBox _showClaudeSession;
    private readonly CheckBox _showClaudeWeekly;
    private readonly CheckBox _showCodexFiveHour;
    private readonly CheckBox _showCodexWeekly;
    private readonly CheckBox _lowUsageAlert;
    private readonly NumericUpDown _lowUsageAlertThreshold;
    private readonly CheckBox _notifyOnReset;
    private readonly CheckBox _usageLogging;
    private readonly ComboBox _language;
    private readonly ComboBox _theme;
    private readonly ComboBox _position;
    private readonly ComboBox _updateInterval;
    private readonly ComboBox _resetDateTimeFormat;
    private readonly ComboBox _lastUpdatedDateTimeFormat;
    private readonly ComboBox _usageLogFormat;
    private readonly TextBox _usageLogFilePath;
    private readonly TextBlock _resetDateTimeFormatStatus;
    private readonly TextBlock _lastUpdatedDateTimeFormatStatus;
    private readonly Button _save;
    private readonly Button _apply;
    private readonly StackPanel _unsavedIndicator;

    private readonly Grid _rootGrid;
    private readonly Border _sidebarBorder;
    private readonly Border _footerBorder;
    private readonly TextBox _searchBox;
    private readonly TextBlock _categoryTitle;
    private readonly TextBlock _categoryDescription;
    private readonly ScrollViewer _contentScrollViewer;
    private readonly TextBlock _noSearchResults;
    private readonly List<Border> _cards = new();
    private readonly List<CategoryNavItem> _categoryNavItems = new();
    private SettingsCategory _currentCategory = SettingsCategory.General;

    private CompanionSettings _initialSettings = null!;
    private bool _allowClose;
    private bool _discardPromptOpen;

    public SettingsWindow(CompanionSettings settings, UiText text)
    {
        _settings = settings;
        _text = text;
        Title = text.SettingsTitle;
        Width = 740;
        Height = 580;
        MinWidth = 640;
        MinHeight = 480;
        CanResize = true;
        ShowInTaskbar = settings.ShowTaskbarIcon;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        // General controls
        _systemTray = new CheckBox
        {
            Content = text.SystemTrayOption,
            IsChecked = settings.EnableSystemTray
        };
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
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _showTaskbarIcon = new CheckBox
        {
            Content = text.ShowTaskbarIconOption,
            IsChecked = settings.ShowTaskbarIcon
        };
        _startOnBoot = new CheckBox
        {
            Content = text.StartOnBootOption,
            IsChecked = settings.StartOnBoot
        };
        _minimizeOnStart = new CheckBox
        {
            Content = text.MinimizeOnStartOption,
            IsChecked = settings.MinimizeOnStart
        };
        _startOnBoot.IsCheckedChanged += (_, _) => UpdateMinimizeOnStartControls();
        _alwaysOnTop = new CheckBox
        {
            Content = text.AlwaysOnTopOption,
            IsChecked = settings.AlwaysOnTop
        };

        // Providers controls
        _enableClaudeUsage = new CheckBox
        {
            Content = text.EnableClaudeUsageOption,
            IsChecked = settings.EnableClaudeUsage
        };
        _enableCodexUsage = new CheckBox
        {
            Content = text.EnableCodexUsageOption,
            IsChecked = settings.EnableCodexUsage
        };
        _enableAntigravityUsage = new CheckBox
        {
            Content = text.EnableAntigravityUsageOption,
            IsChecked = settings.EnableAntigravityUsage
        };
        _showClaudeSession = new CheckBox
        {
            Content = text.ShowClaudeSessionOption,
            IsChecked = settings.ShowClaudeSession,
            Margin = new Thickness(16, 0, 0, 0)
        };
        _showClaudeWeekly = new CheckBox
        {
            Content = text.ShowClaudeWeeklyOption,
            IsChecked = settings.ShowClaudeWeekly,
            Margin = new Thickness(16, 0, 0, 0)
        };
        _showCodexFiveHour = new CheckBox
        {
            Content = text.ShowCodexFiveHourOption,
            IsChecked = settings.ShowCodexFiveHour,
            Margin = new Thickness(16, 0, 0, 0)
        };
        _showCodexWeekly = new CheckBox
        {
            Content = text.ShowCodexWeeklyOption,
            IsChecked = settings.ShowCodexWeekly,
            Margin = new Thickness(16, 0, 0, 0)
        };
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
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Appearance controls
        var languages = new[]
        {
            new LanguageChoice("en-US", text.EnglishLanguage),
            new LanguageChoice("zh-tw", text.TraditionalChineseLanguage),
            new LanguageChoice("zh-cn", text.SimplifiedChineseLanguage)
        };
        var selectedLanguage = settings.Language?.ToLowerInvariant() switch
        {
            "zh-tw" => "zh-tw",
            "zh-cn" => "zh-cn",
            "auto" when text.Language == UiLanguage.TraditionalChinese => "zh-tw",
            "auto" when text.Language == UiLanguage.SimplifiedChinese => "zh-cn",
            _ => "en-US"
        };
        _language = new ComboBox
        {
            ItemsSource = languages,
            SelectedItem = languages.First(choice => choice.Value == selectedLanguage),
            HorizontalAlignment = HorizontalAlignment.Stretch
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
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _position = new ComboBox
        {
            ItemsSource = WindowPosition.Values,
            SelectedItem = WindowPosition.Normalize(settings.Position),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _position.SelectionChanged += (_, _) =>
        {
            if (_position.SelectedItem is string position)
            {
                PositionPreviewRequested?.Invoke(position);
            }
        };

        // Notification controls
        _lowUsageAlert = new CheckBox
        {
            Content = text.LowUsageAlertOption,
            IsChecked = settings.EnableLowUsageAlert
        };
        _lowUsageAlertThreshold = new NumericUpDown
        {
            Value = UsageAlertOptions.NormalizeThreshold(settings.LowUsageAlertThresholdPercent),
            Minimum = UsageAlertOptions.MinimumThresholdPercent,
            Maximum = UsageAlertOptions.MaximumThresholdPercent,
            Increment = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _notifyOnReset = new CheckBox
        {
            Content = text.NotifyOnResetOption,
            IsChecked = settings.NotifyOnReset
        };
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
            HorizontalAlignment = HorizontalAlignment.Stretch
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
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _resetDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        _lastUpdatedDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        // Logging controls
        _usageLogging = new CheckBox
        {
            Content = text.UsageLoggingOption,
            IsChecked = settings.EnableUsageLogging
        };
        _usageLogFilePath = new TextBox
        {
            Text = UsageLogOptions.NormalizeFilePath(settings.UsageLogFilePath, settings.UsageLogFormat),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _usageLogFormat = new ComboBox
        {
            ItemsSource = UsageLogOptions.Formats,
            SelectedItem = UsageLogOptions.NormalizeFormat(settings.UsageLogFormat),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _usageLogFormat.SelectionChanged += (_, _) =>
        {
            if (_usageLogFormat.SelectedItem is not string newFormat)
            {
                return;
            }

            _usageLogFilePath.Text = UsageLogOptions.ChangeFileExtension(_usageLogFilePath.Text, newFormat);
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
            Text = "No settings match your search.",
            FontSize = 14,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 40, 0, 0),
            IsVisible = false
        };

        // Build Category Panels
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
            ColumnDefinitions = new ColumnDefinitions("210,*")
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

        SelectCategory(SettingsCategory.General);
        ApplyThemePalette();

        ActualThemeVariantChanged += (_, _) => ApplyThemePalette();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
        Closing += HandleClosing;
    }

    public event Action<string>? PositionPreviewRequested;

    public event Func<CompanionSettings, bool>? ApplyRequested;

    private void BuildCategoryPanels()
    {
        // 1. General Panel
        var generalCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.WindowSettingsGroup,
                    "Configure system tray presence, startup behavior, and window pin state.",
                    _systemTray,
                    CreateFieldGroup(_text.TrayIconStyleOption, _trayIconStyle),
                    _showTaskbarIcon,
                    _startOnBoot,
                    _minimizeOnStart,
                    _alwaysOnTop)
            }
        };

        // 2. Providers Panel
        var providerLimitsLabel = new TextBlock
        {
            Text = _text.DisplayedLimitsOption,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 4, 0, 2)
        };
        var providersCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.UsageSettingsGroup,
                    "Select which AI assistants to monitor and track rate limits for.",
                    _enableClaudeUsage,
                    _enableCodexUsage,
                    _enableAntigravityUsage),
                CreateSettingCard(
                    _text.DisplayedLimitsOption,
                    "Customize visible windows and quota limit meters on the overlay.",
                    _showClaudeSession,
                    _showClaudeWeekly,
                    _showCodexFiveHour,
                    _showCodexWeekly),
                CreateSettingCard(
                    _text.UpdateIntervalOption,
                    "Control how frequently usage statistics are refreshed from the provider APIs.",
                    CreateFieldGroup(_text.UpdateIntervalOption, _updateInterval))
            }
        };

        // 3. Appearance Panel
        var appearanceCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.AppearanceSettingsGroup,
                    "Customize language, theme mode, and default screen corner docking.",
                    CreateFieldGroup(_text.LanguageOption, _language),
                    CreateFieldGroup(_text.ThemeOption, _theme),
                    CreateFieldGroup(_text.PositionOption, _position))
            }
        };

        // 4. Notifications Panel
        var notificationsCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.NotificationSettingsGroup,
                    "Get notified when quota drops below warning thresholds or resets.",
                    _lowUsageAlert,
                    CreateFieldGroup(_text.LowUsageAlertThresholdOption, _lowUsageAlertThreshold),
                    _notifyOnReset)
            }
        };

        // 5. Date & Time Panel
        var dateTimeCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.DateTimeSettingsGroup,
                    "Specify timestamp formats for rate limit resets and last refresh time.",
                    CreateFieldGroup(_text.ResetDateTimeFormatOption, _resetDateTimeFormat),
                    _resetDateTimeFormatStatus,
                    CreateFieldGroup(_text.LastUpdatedDateTimeFormatOption, _lastUpdatedDateTimeFormat),
                    _lastUpdatedDateTimeFormatStatus)
            }
        };

        // 6. Logging Panel
        var loggingCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.LoggingSettingsGroup,
                    "Log usage samples over time to a CSV or JSONL file for audit and charts.",
                    _usageLogging,
                    CreateFieldGroup(_text.UsageLogFilePathOption, _usageLogFilePath),
                    CreateFieldGroup(_text.UsageLogFormatOption, _usageLogFormat))
            }
        };

        // 7. About Panel
        var appVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var appHeader = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                new TextBlock
                {
                    Text = $"Claude Codex Usage Companion v{appVersion}",
                    FontSize = 16,
                    FontWeight = FontWeight.Bold
                },
                new TextBlock
                {
                    Text = "Desktop companion for real-time monitoring and quota tracking of Claude, Codex, and Antigravity.",
                    FontSize = 12,
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        var authorText = new TextBlock
        {
            Text = _text.AuthorInfo,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };
        var aboutCards = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSettingCard(
                    _text.AboutSettingsGroup,
                    null,
                    appHeader,
                    authorText)
            }
        };

        // Register Category Nav Items
        _categoryNavItems.Clear();
        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.General,
            _text.WindowSettingsGroup,
            _text.WindowSettingsDescription,
            "M2 4C2 2.9 2.9 2 4 2H20C21.1 2 22 2.9 22 4V20C21.1 22 20 22 20 22H4C2.9 22 2 21.1 2 20V4ZM4 6H20V4H4V6ZM4 8V20H20V8H4ZM6 10H10V12H6V10ZM6 14H14V16H6V14Z",
            ["tray", "taskbar", "boot", "startup", "minimize", "top", "always on top", "window", "system"],
            generalCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Providers,
            _text.UsageSettingsGroup,
            _text.UsageSettingsDescription,
            "M3 17V19H9V17H3ZM3 5V7H13V5H3ZM13 21V19H21V17H13V15H11V21H13ZM7 9V11H3V13H7V15H9V9H7ZM21 13V11H11V13H21ZM17 9H21V7H17V5H15V11H17V9Z",
            ["claude", "codex", "antigravity", "provider", "limit", "rate limit", "session", "weekly", "5-hour", "refresh", "interval"],
            providersCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Appearance,
            _text.AppearanceSettingsGroup,
            _text.AppearanceSettingsDescription,
            "M12 3C6.5 3 2 6.5 2 12C2 17.5 6.5 21 12 21C12.8 21 13.5 20.3 13.5 19.5C13.5 19.1 13.3 18.8 13.1 18.5C12.9 18.2 12.7 17.9 12.7 17.5C12.7 16.7 13.4 16 14.2 16H16C19.3 16 22 13.3 22 10C22 5.5 17.5 3 12 3ZM6.5 12C5.7 12 5 11.3 5 10.5C5 9.7 5.7 9 6.5 9C7.3 9 8 9.7 8 10.5C8 11.3 7.3 12 6.5 12ZM9.5 8C8.7 8 8 7.3 8 6.5C8 5.7 8.7 5 9.5 5C10.3 5 11 5.7 11 6.5C11 7.3 10.3 8 9.5 8ZM14.5 8C13.7 8 13 7.3 13 6.5C13 5.7 13.7 5 14.5 5C15.3 5 16 5.7 16 6.5C16 7.3 15.3 8 14.5 8ZM17.5 12C16.7 12 16 11.3 16 10.5C16 9.7 16.7 9 17.5 9C18.3 9 19 9.7 19 10.5C19 11.3 18.3 12 17.5 12Z",
            ["language", "theme", "dark", "light", "position", "overlay", "screen", "corner", "interface"],
            appearanceCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Notifications,
            _text.NotificationSettingsGroup,
            _text.NotificationSettingsDescription,
            "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.37 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.64 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16ZM16 17H8V11C8 8.52 9.51 6.5 12 6.5C14.49 6.5 16 8.52 16 11V17Z",
            ["alert", "notify", "reset", "threshold", "quota", "warning", "sound", "notification"],
            notificationsCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.DateTime,
            _text.DateTimeSettingsGroup,
            _text.DateTimeSettingsDescription,
            "M11.99 2C6.47 2 2 6.48 2 12C2 17.52 6.47 22 11.99 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 11.99 2ZM12 20C7.58 20 4 16.42 4 12C4 7.58 7.58 4 12 4C16.42 4 20 7.58 20 12C20 16.42 16.42 20 12 20ZM12.5 7H11V13L16.25 16.15L17 14.92L12.5 12.25V7Z",
            ["date", "time", "format", "timestamp", "clock", "reset time", "preview"],
            dateTimeCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Logging,
            _text.LoggingSettingsGroup,
            _text.LoggingSettingsDescription,
            "M14 2H6C4.9 2 4.01 2.9 4.01 4L4 20C4 21.1 4.89 22 5.99 22H18C19.1 22 20 21.1 20 20V8L14 2ZM16 18H8V16H16V18ZM16 14H8V12H16V14ZM13 9V3.5L18.5 9H13Z",
            ["log", "logging", "csv", "jsonl", "history", "export", "file", "path"],
            loggingCards));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.About,
            _text.AboutSettingsGroup,
            _text.AboutSettingsDescription,
            "M12 2C6.48 2 2 6.48 2 12C2 17.52 6.48 22 12 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 12 2ZM13 17H11V11H13V17ZM13 9H11V7H13V9Z",
            ["version", "author", "about", "license", "info", "github", "release"],
            aboutCards));
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
            Background = SolidColorBrush.Parse("#0284C7"),
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
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        var activeBg = isLight ? SolidColorBrush.Parse("#E2E8F0") : SolidColorBrush.Parse("#2D2D35");
        var activeAccent = isLight ? SolidColorBrush.Parse("#0284C7") : SolidColorBrush.Parse("#38BDF8");
        var normalText = isLight ? SolidColorBrush.Parse("#0F172A") : SolidColorBrush.Parse("#F4F4F5");
        var normalIcon = isLight ? SolidColorBrush.Parse("#64748B") : SolidColorBrush.Parse("#A1A1AA");

        foreach (var item in _categoryNavItems)
        {
            var isSelected = item.Category == _currentCategory;
            item.Indicator.IsVisible = isSelected;
            item.Indicator.Background = activeAccent;
            item.Button.Background = isSelected ? activeBg : Brushes.Transparent;
            item.TitleBlock.FontWeight = isSelected ? FontWeight.SemiBold : FontWeight.Normal;
            item.TitleBlock.Foreground = isSelected ? activeAccent : normalText;
            item.IconPath.Fill = isSelected ? activeAccent : normalIcon;
        }
    }

    private void HandleSearchChanged()
    {
        var query = (_searchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(query))
        {
            foreach (var item in _categoryNavItems)
            {
                item.Button.IsVisible = true;
            }
            _noSearchResults.IsVisible = false;
            _contentScrollViewer.IsVisible = true;
            return;
        }

        var visibleCount = 0;
        foreach (var item in _categoryNavItems)
        {
            var match = item.Title.ToLowerInvariant().Contains(query) ||
                        item.Description.ToLowerInvariant().Contains(query) ||
                        item.Keywords.Any(k => k.ToLowerInvariant().Contains(query));

            item.Button.IsVisible = match;
            if (match)
            {
                visibleCount++;
            }
        }

        var currentVisible = _categoryNavItems.FirstOrDefault(c => c.Category == _currentCategory)?.Button.IsVisible == true;
        if (!currentVisible && visibleCount > 0)
        {
            var firstMatch = _categoryNavItems.First(c => c.Button.IsVisible);
            SelectCategory(firstMatch.Category);
        }

        _noSearchResults.IsVisible = visibleCount == 0;
        _contentScrollViewer.IsVisible = visibleCount > 0;
    }

    private Border CreateSettingCard(string? title, string? subtitle, params Control[] controls)
    {
        var panel = new StackPanel
        {
            Spacing = 10
        };

        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(subtitle) ? 8 : 2)
            };
            panel.Children.Add(titleBlock);
        }

        if (!string.IsNullOrEmpty(subtitle))
        {
            var subBlock = new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(subBlock);
        }

        foreach (var ctrl in controls)
        {
            panel.Children.Add(ctrl);
        }

        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = panel
        };
        _cards.Add(card);
        return card;
    }

    private static StackPanel CreateFieldGroup(string label, Control inputControl)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.85,
            Margin = new Thickness(0, 2, 0, 4)
        };
        return new StackPanel
        {
            Spacing = 2,
            Children = { labelBlock, inputControl }
        };
    }

    private void ApplyThemePalette()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;

        var windowBg = isLight ? SolidColorBrush.Parse("#FFF8FAFC") : SolidColorBrush.Parse("#FF18181B");
        var sidebarBg = isLight ? SolidColorBrush.Parse("#FFF1F5F9") : SolidColorBrush.Parse("#FF1E1E22");
        var sidebarBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF2D2D34");
        var cardBg = isLight ? SolidColorBrush.Parse("#FFFFFFFF") : SolidColorBrush.Parse("#FF242428");
        var cardBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF34343C");
        var footerBg = isLight ? SolidColorBrush.Parse("#FFF1F5F9") : SolidColorBrush.Parse("#FF1E1E22");
        var footerBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF2D2D34");
        var textPrimary = isLight ? SolidColorBrush.Parse("#FF0F172A") : SolidColorBrush.Parse("#FFF4F4F5");
        var textSecondary = isLight ? SolidColorBrush.Parse("#FF64748B") : SolidColorBrush.Parse("#FFA1A1AA");
        var primaryButtonBg = isLight ? SolidColorBrush.Parse("#FF0284C7") : SolidColorBrush.Parse("#FF0284C7");

        Background = windowBg;
        _sidebarBorder.Background = sidebarBg;
        _sidebarBorder.BorderBrush = sidebarBorderBrush;
        _footerBorder.Background = footerBg;
        _footerBorder.BorderBrush = footerBorderBrush;

        _categoryTitle.Foreground = textPrimary;
        _categoryDescription.Foreground = textSecondary;

        _save.Background = primaryButtonBg;
        _save.Foreground = Brushes.White;

        foreach (var card in _cards)
        {
            card.Background = cardBg;
            card.BorderBrush = cardBorderBrush;
        }

        UpdateNavSelectionStyles();
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
            var dialog = CreateUnsavedChangesDialog();
            if (!await dialog.ShowDialog<bool>(this))
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

    private Window CreateUnsavedChangesDialog()
    {
        var dialog = new Window
        {
            Title = _text.UnsavedChangesTitle,
            Width = 420,
            Height = 180,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var message = new TextBlock
        {
            Text = _text.UnsavedChangesMessage,
            TextWrapping = TextWrapping.Wrap
        };
        var discard = new Button
        {
            Content = _text.DiscardChangesAction,
            MinWidth = 112,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        discard.Click += (_, _) => dialog.Close(true);
        var keepEditing = new Button
        {
            Content = _text.KeepEditingAction,
            MinWidth = 112,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        keepEditing.Click += (_, _) => dialog.Close(false);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { discard, keepEditing }
        };
        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(24)
        };
        Grid.SetRow(actions, 1);
        layout.Children.Add(message);
        layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key != Key.Escape)
            {
                return;
            }

            eventArgs.Handled = true;
            dialog.Close(false);
        };
        return dialog;
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
            Position = _position.SelectedItem as string ?? WindowPosition.RightBottom,
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
                _usageLogFormat.SelectedItem as string ?? UsageLogOptions.Csv
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
        _usageLogFilePath.IsEnabled = enabled;
        _usageLogFormat.IsEnabled = enabled;
    }

    private void UpdateLowUsageAlertControls()
    {
        _lowUsageAlertThreshold.IsEnabled = _lowUsageAlert.IsChecked == true;
    }

    private void UpdateDisplayedLimitControls()
    {
        var claudeEnabled = _enableClaudeUsage.IsChecked == true;
        _showClaudeSession.IsEnabled = claudeEnabled;
        _showClaudeWeekly.IsEnabled = claudeEnabled;

        var codexEnabled = _enableCodexUsage.IsChecked == true;
        _showCodexFiveHour.IsEnabled = codexEnabled;
        _showCodexWeekly.IsEnabled = codexEnabled;
    }

    private void UpdateMinimizeOnStartControls()
    {
        _minimizeOnStart.IsVisible = _startOnBoot.IsChecked == true;
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
        var sample = new DateTimeOffset(
            2026,
            8,
            6,
            13,
            55,
            9,
            TimeSpan.Zero);
        var resetValid = UpdateDateTimeFormatStatus(
            CurrentFormat(_resetDateTimeFormat),
            _resetDateTimeFormatStatus,
            sample);
        var lastUpdatedValid = UpdateDateTimeFormatStatus(
            CurrentFormat(_lastUpdatedDateTimeFormat),
            _lastUpdatedDateTimeFormatStatus,
            sample);
        _save.IsEnabled = resetValid && lastUpdatedValid;
        _apply.IsEnabled = _save.IsEnabled;
        return _save.IsEnabled;
    }

    private bool UpdateDateTimeFormatStatus(
        string format,
        TextBlock status,
        DateTimeOffset sample)
    {
        if (_text.TryFormatDateTime(sample, format, out var preview))
        {
            status.Text = $"{_text.FormatPreview}: {preview}";
            status.Foreground = Brushes.Gray;
            return true;
        }

        status.Text = _text.InvalidDateTimeFormat;
        status.Foreground = Brushes.IndianRed;
        return false;
    }

    private static string CurrentFormat(ComboBox comboBox)
    {
        return (comboBox.Text ?? comboBox.SelectedItem as string ?? string.Empty).Trim();
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

    private sealed record UpdateIntervalChoice(int Seconds, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
