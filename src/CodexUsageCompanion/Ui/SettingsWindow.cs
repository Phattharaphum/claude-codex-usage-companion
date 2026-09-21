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

    // Notification controls
    private readonly ToggleSwitch _lowUsageAlert;
    private readonly NumericUpDown _lowUsageAlertThreshold;
    private readonly ToggleSwitch _notifyOnReset;
    private Border _lowUsageThresholdRow = null!;

    // DateTime controls
    private readonly ComboBox _resetDateTimeFormat;
    private readonly ComboBox _lastUpdatedDateTimeFormat;
    private readonly TextBlock _resetDateTimeFormatStatus;
    private readonly TextBlock _lastUpdatedDateTimeFormatStatus;

    // Logging controls
    private readonly ToggleSwitch _usageLogging;
    private readonly TextBox _usageLogFilePath;
    private readonly ComboBox _usageLogFormat;
    private Border _usageLogFilePathRow = null!;
    private Border _usageLogFormatRow = null!;

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
            MinWidth = 180,
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
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Right
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
            MinWidth = 180,
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
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _position = new ComboBox
        {
            ItemsSource = WindowPosition.Values,
            SelectedItem = WindowPosition.Normalize(settings.Position),
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _position.SelectionChanged += (_, _) =>
        {
            if (_position.SelectedItem is string position)
            {
                PositionPreviewRequested?.Invoke(position);
            }
        };

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
            MinWidth = 200,
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
            MinWidth = 200,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _resetDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 2)
        };
        _lastUpdatedDateTimeFormatStatus = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 2)
        };

        // Logging controls
        _usageLogging = CreateToggleSwitch(settings.EnableUsageLogging);
        _usageLogFilePath = new TextBox
        {
            Text = UsageLogOptions.NormalizeFilePath(settings.UsageLogFilePath, settings.UsageLogFormat),
            MinWidth = 240,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _usageLogFormat = new ComboBox
        {
            ItemsSource = UsageLogOptions.Formats,
            SelectedItem = UsageLogOptions.NormalizeFormat(settings.UsageLogFormat),
            MinWidth = 120,
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

        // Build Category Panels with Option B Setting Rows
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
        // 1. General Panel
        _minimizeOnStartRow = CreateSettingRow(
            _text.MinimizeOnStartOption,
            _text.MinimizeOnStartDescription,
            _minimizeOnStart);

        var generalCard1 = CreateCardGroup(
            _text.WindowSettingsGroup,
            "Manage system tray integration and taskbar visibility.",
            CreateSettingRow(_text.SystemTrayOption, _text.SystemTrayDescription, _systemTray),
            CreateSettingRow(_text.TrayIconStyleOption, _text.TrayIconStyleDescription, _trayIconStyle),
            CreateSettingRow(_text.ShowTaskbarIconOption, _text.ShowTaskbarIconDescription, _showTaskbarIcon, showDivider: false));

        var generalCard2 = CreateCardGroup(
            "Startup & Placement",
            "Configure how the application launches and behaves on the desktop.",
            CreateSettingRow(_text.StartOnBootOption, _text.StartOnBootDescription, _startOnBoot),
            _minimizeOnStartRow,
            CreateSettingRow(_text.AlwaysOnTopOption, _text.AlwaysOnTopDescription, _alwaysOnTop, showDivider: false));

        var generalPanel = new StackPanel
        {
            Spacing = 12,
            Children = { generalCard1, generalCard2 }
        };

        // 2. Providers Panel (Branded Provider Cards)
        var claudeCard = CreateBrandedProviderCard(
            brandName: "Anthropic Claude",
            brandColorHex: "#EA580C",
            iconData: "M12 2L13.8 8.7L20.5 7L16.2 12L21.8 15.2L15.3 16.5L16.8 23L12 18.5L7.2 23L8.7 16.5L2.2 15.2L7.8 12L3.5 7L10.2 8.7L12 2Z",
            badgeText: "Claude 3.5 / 3.7 Sonnet & Opus",
            masterSwitch: _enableClaudeUsage,
            description: _text.ClaudeProviderDescription,
            out _claudeSubOptionsContainer,
            CreateSettingRow(_text.ShowClaudeSessionOption, _text.ClaudeSessionLimitDescription, _showClaudeSession),
            CreateSettingRow(_text.ShowClaudeWeeklyOption, _text.ClaudeWeeklyLimitDescription, _showClaudeWeekly, showDivider: false));

        var codexCard = CreateBrandedProviderCard(
            brandName: "OpenAI Codex",
            brandColorHex: "#10B981",
            iconData: "M12 2C6.48 2 2 6.48 2 12S6.48 22 12 22 22 17.52 22 12 17.52 2 12 2ZM12 4C16.42 4 20 7.58 20 12C20 13.91 19.33 15.66 18.21 17.03L6.97 5.79C8.34 4.67 10.09 4 12 4ZM4 12C4 10.09 4.67 8.34 5.79 6.97L17.03 18.21C15.66 19.33 13.91 20 12 20C7.58 20 4 16.42 4 12Z",
            badgeText: "Codex & GPT-4o",
            masterSwitch: _enableCodexUsage,
            description: _text.CodexProviderDescription,
            out _codexSubOptionsContainer,
            CreateSettingRow(_text.ShowCodexFiveHourOption, _text.CodexFiveHourLimitDescription, _showCodexFiveHour),
            CreateSettingRow(_text.ShowCodexWeeklyOption, _text.CodexWeeklyLimitDescription, _showCodexWeekly, showDivider: false));

        var antigravityCard = CreateBrandedProviderCard(
            brandName: "Google Antigravity",
            brandColorHex: "#3B82F6",
            iconData: "M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z",
            badgeText: "Gemini 2.5 & Flash",
            masterSwitch: _enableAntigravityUsage,
            description: _text.AntigravityProviderDescription,
            out _,
            null);

        var pollingCard = CreateCardGroup(
            _text.UpdateIntervalOption,
            _text.RefreshIntervalDescription,
            CreateSettingRow(_text.UpdateIntervalOption, _text.RefreshIntervalDescription, _updateInterval, showDivider: false));

        var providersPanel = new StackPanel
        {
            Spacing = 12,
            Children = { claudeCard, codexCard, antigravityCard, pollingCard }
        };

        // 3. Appearance Panel
        var appearanceCard1 = CreateCardGroup(
            _text.AppearanceSettingsGroup,
            "Personalize visual presentation, active language, and color theme.",
            CreateSettingRow(_text.LanguageOption, _text.LanguageOptionDescription, _language),
            CreateSettingRow(_text.ThemeOption, _text.ThemeOptionDescription, _theme, showDivider: false));

        var appearanceCard2 = CreateCardGroup(
            _text.PositionOption,
            _text.PositionOptionDescription,
            CreateSettingRow(_text.PositionOption, "Select where the companion overlay docks on your desktop.", _position, showDivider: false));

        var appearancePanel = new StackPanel
        {
            Spacing = 12,
            Children = { appearanceCard1, appearanceCard2 }
        };

        // 4. Notifications Panel
        _lowUsageThresholdRow = CreateSettingRow(
            _text.LowUsageAlertThresholdOption,
            _text.LowUsageThresholdDescription,
            _lowUsageAlertThreshold);

        var notificationsCard = CreateCardGroup(
            _text.NotificationSettingsGroup,
            "Manage quota alerts, threshold levels, and reset notifications.",
            CreateSettingRow(_text.LowUsageAlertOption, _text.LowUsageAlertDescription, _lowUsageAlert),
            _lowUsageThresholdRow,
            CreateSettingRow(_text.NotifyOnResetOption, _text.NotifyOnResetDescription, _notifyOnReset, showDivider: false));

        var notificationsPanel = new StackPanel
        {
            Spacing = 12,
            Children = { notificationsCard }
        };

        // 5. Date & Time Panel
        var resetFormatRow = CreateSettingRow(
            _text.ResetDateTimeFormatOption,
            _text.ResetDateTimeFormatDescription,
            _resetDateTimeFormat);

        var lastUpdatedFormatRow = CreateSettingRow(
            _text.LastUpdatedDateTimeFormatOption,
            _text.LastUpdatedDateTimeFormatDescription,
            _lastUpdatedDateTimeFormat);

        var dateTimeCard1 = CreateCardGroup(
            "Rate Limit Reset Format",
            "Formatting string for when session or weekly rate limits expire.",
            resetFormatRow,
            _resetDateTimeFormatStatus);

        var dateTimeCard2 = CreateCardGroup(
            "Last Refresh Format",
            "Formatting string for when usage statistics were last retrieved.",
            lastUpdatedFormatRow,
            _lastUpdatedDateTimeFormatStatus);

        var dateTimePanel = new StackPanel
        {
            Spacing = 12,
            Children = { dateTimeCard1, dateTimeCard2 }
        };

        // 6. Logging Panel
        _usageLogFilePathRow = CreateSettingRow(
            _text.UsageLogFilePathOption,
            _text.UsageLogFilePathDescription,
            _usageLogFilePath);

        _usageLogFormatRow = CreateSettingRow(
            _text.UsageLogFormatOption,
            _text.UsageLogFormatDescription,
            _usageLogFormat,
            showDivider: false);

        var loggingCard = CreateCardGroup(
            _text.LoggingSettingsGroup,
            "Audit and record historical rate limit usage samples to local storage.",
            CreateSettingRow(_text.UsageLoggingOption, _text.UsageLoggingDescription, _usageLogging),
            _usageLogFilePathRow,
            _usageLogFormatRow);

        var loggingPanel = new StackPanel
        {
            Spacing = 12,
            Children = { loggingCard }
        };

        // 7. About Panel
        var appVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var appHeader = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 4, 0, 12),
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
        var aboutCard = CreateCardGroup(
            _text.AboutSettingsGroup,
            null,
            appHeader,
            authorText);

        var aboutPanel = new StackPanel
        {
            Spacing = 12,
            Children = { aboutCard }
        };

        // Register Category Nav Items
        _categoryNavItems.Clear();
        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.General,
            _text.WindowSettingsGroup,
            _text.WindowSettingsDescription,
            "M2 4C2 2.9 2.9 2 4 2H20C21.1 2 22 2.9 22 4V20C21.1 22 20 22 20 22H4C2.9 22 2 21.1 2 20V4ZM4 6H20V4H4V6ZM4 8V20H20V8H4ZM6 10H10V12H6V10ZM6 14H14V16H6V14Z",
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
            ["language", "theme", "dark", "light", "position", "overlay", "screen", "corner", "interface"],
            appearancePanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.Notifications,
            _text.NotificationSettingsGroup,
            _text.NotificationSettingsDescription,
            "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.37 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.64 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16ZM16 17H8V11C8 8.52 9.51 6.5 12 6.5C14.49 6.5 16 8.52 16 11V17Z",
            ["alert", "notify", "reset", "threshold", "quota", "warning", "sound", "notification"],
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
            ["log", "logging", "csv", "jsonl", "history", "export", "file", "path"],
            loggingPanel));

        _categoryNavItems.Add(CreateCategoryNavItem(
            SettingsCategory.About,
            _text.AboutSettingsGroup,
            _text.AboutSettingsDescription,
            "M12 2C6.48 2 2 6.48 2 12C2 17.52 6.48 22 12 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 12 2ZM13 17H11V11H13V17ZM13 9H11V7H13V9Z",
            ["version", "author", "about", "license", "info", "github", "release"],
            aboutPanel));
    }

    private Border CreateSettingRow(string title, string? subtitle, Control control, bool showDivider = true)
    {
        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = FontWeight.Medium,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
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
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap
            };
            _textSecondaryElements.Add(subBlock);
            labelPanel.Children.Add(subBlock);
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 6, 0, 6)
        };
        grid.Children.Add(labelPanel);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        var rowContainer = new StackPanel();
        rowContainer.Children.Add(grid);

        if (showDivider)
        {
            var divider = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _dividers.Add(divider);
            rowContainer.Children.Add(divider);
        }

        return new Border { Child = rowContainer };
    }

    private Border CreateCardGroup(string? title, string? subtitle, params Control[] items)
    {
        var panel = new StackPanel
        {
            Spacing = 2
        };

        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, string.IsNullOrEmpty(subtitle) ? 8 : 2)
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
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _textSecondaryElements.Add(subBlock);
            panel.Children.Add(subBlock);
        }

        foreach (var item in items)
        {
            panel.Children.Add(item);
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
            VerticalAlignment = VerticalAlignment.Center
        };
        _textPrimaryElements.Add(titleBlock);

        var badge = new Border
        {
            Background = SolidColorBrush.Parse(brandColorHex),
            Opacity = 0.85,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 10,
                FontWeight = FontWeight.Medium,
                Foreground = Brushes.White
            }
        };

        var headerLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, titleBlock, badge }
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
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
                Padding = new Thickness(12, 4, 0, 4),
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
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = cardPanel
        };
        _cards.Add(card);
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

    private void ApplyThemePalette()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;

        var windowBg = isLight ? SolidColorBrush.Parse("#FFF8FAFC") : SolidColorBrush.Parse("#FF18181B");
        var sidebarBg = isLight ? SolidColorBrush.Parse("#FFF1F5F9") : SolidColorBrush.Parse("#FF1E1E22");
        var sidebarBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF2D2D34");
        var cardBg = isLight ? SolidColorBrush.Parse("#FFFFFFFF") : SolidColorBrush.Parse("#FF242428");
        var cardBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF34343C");
        var dividerBrush = isLight ? SolidColorBrush.Parse("#FFF1F5F9") : SolidColorBrush.Parse("#FF2F2F37");
        var footerBg = isLight ? SolidColorBrush.Parse("#FFF1F5F9") : SolidColorBrush.Parse("#FF1E1E22");
        var footerBorderBrush = isLight ? SolidColorBrush.Parse("#FFE2E8F0") : SolidColorBrush.Parse("#FF2D2D34");
        var textPrimary = isLight ? SolidColorBrush.Parse("#FF0F172A") : SolidColorBrush.Parse("#FFF4F4F5");
        var textSecondary = isLight ? SolidColorBrush.Parse("#FF64748B") : SolidColorBrush.Parse("#FFA1A1AA");
        var primaryButtonBg = SolidColorBrush.Parse("#FF0284C7");

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

        foreach (var divider in _dividers)
        {
            divider.Background = dividerBrush;
        }

        foreach (var el in _textPrimaryElements)
        {
            el.Foreground = textPrimary;
        }

        foreach (var el in _textSecondaryElements)
        {
            el.Foreground = textSecondary;
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
        _usageLogFilePathRow.IsEnabled = enabled;
        _usageLogFilePathRow.Opacity = enabled ? 1.0 : 0.45;
        _usageLogFormatRow.IsEnabled = enabled;
        _usageLogFormatRow.Opacity = enabled ? 1.0 : 0.45;
        _usageLogFilePath.IsEnabled = enabled;
        _usageLogFormat.IsEnabled = enabled;
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
