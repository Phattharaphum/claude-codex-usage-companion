using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Ui;

public sealed class UsageOverlayWindow : Window
{
    // Header, root padding, and the breathing room around provider sections.
    private const double BaseHeight = 77;
    private const double CardSpacing = 7;
    private const double FiveHourCardHeight = 60;
    private const double WeeklyCardHeight = 60;
    private const double UnavailableCardHeight = 44;
    private const double AntigravityWindowCardHeight = 60;
    private const double ProviderSectionTitleHeight = 24;
    private const double AntigravitySectionTitleHeight = 24;
    private const double AntigravityGroupTitleHeight = 20;
    private const double AntigravityMessageCardHeight = 46;
    private const double AntigravityErrorCardHeight = 52;
    private const double ContentBottomPadding = 4;
    private const double IconSize = 14;
    private const double HeaderGroupSpacing = 6;
    private const string CodexAccentColor = "#10A37F";
    private const string ClaudeIconBackground = "#D77655";
    private const string ClaudeIconForeground = "#FCF2EE";
    private const string ClaudeIconPath =
        "M142.27 316.619l73.655-41.326 1.238-3.589-1.238-1.996-3.589-.001-12.31-.759-42.084-1.138-36.498-1.516-35.361-1.896-8.897-1.895-8.34-10.995.859-5.484 7.482-5.03 10.717.935 23.683 1.617 35.537 2.452 25.782 1.517 38.193 3.968h6.064l.86-2.451-2.073-1.517-1.618-1.517-36.776-24.922-39.81-26.338-20.852-15.166-11.273-7.683-5.687-7.204-2.451-15.721 10.237-11.273 13.75.935 3.513.936 13.928 10.716 29.749 23.027 38.848 28.612 5.687 4.727 2.275-1.617.278-1.138-2.553-4.271-21.13-38.193-22.546-38.848-10.035-16.101-2.654-9.655c-.935-3.968-1.617-7.304-1.617-11.374l11.652-15.823 6.445-2.073 15.545 2.073 6.547 5.687 9.655 22.092 15.646 34.78 24.265 47.291 7.103 14.028 3.791 12.992 1.416 3.968 2.449-.001v-2.275l1.997-26.641 3.69-32.707 3.589-42.084 1.239-11.854 5.863-14.206 11.652-7.683 9.099 4.348 7.482 10.716-1.036 6.926-4.449 28.915-8.72 45.294-5.687 30.331h3.313l3.792-3.791 15.342-20.372 25.782-32.227 11.374-12.789 13.27-14.129 8.517-6.724 16.1-.001 11.854 17.617-5.307 18.199-16.581 21.029-13.75 17.819-19.716 26.54-12.309 21.231 1.138 1.694 2.932-.278 44.536-9.479 24.062-4.347 28.714-4.928 12.992 6.066 1.416 6.167-5.106 12.613-30.71 7.583-36.018 7.204-53.636 12.689-.657.48.758.935 24.164 2.275 10.337.556h25.301l47.114 3.514 12.309 8.139 7.381 9.959-1.238 7.583-18.957 9.655-25.579-6.066-59.702-14.205-20.474-5.106-2.83-.001v1.694l17.061 16.682 31.266 28.233 39.152 36.397 1.997 8.999-5.03 7.102-5.307-.758-34.401-25.883-13.27-11.651-30.053-25.302-1.996-.001v2.654l6.926 10.136 36.574 54.975 1.895 16.859-2.653 5.485-9.479 3.311-10.414-1.895-21.408-30.054-22.092-33.844-17.819-30.331-2.173 1.238-10.515 113.261-4.929 5.788-11.374 4.348-9.478-7.204-5.03-11.652 5.03-23.027 6.066-30.052 4.928-23.886 4.449-29.674 2.654-9.858-.177-.657-2.173.278-22.37 30.71-34.021 45.977-26.919 28.815-6.445 2.553-11.173-5.789 1.037-10.337 6.243-9.2 37.257-47.392 22.47-29.371 14.508-16.961-.101-2.451h-.859l-98.954 64.251-17.618 2.275-7.583-7.103.936-11.652 3.589-3.791 29.749-20.474-.101.102.024.101z";
    private const string CodexIconPath =
        "M9.205 8.658v-2.26c0-.19.072-.333.238-.428l4.543-2.616c.619-.357 1.356-.523 2.117-.523 2.854 0 4.662 2.212 4.662 4.566 0 .167 0 .357-.024.547l-4.71-2.759a.797.797 0 00-.856 0l-5.97 3.473zm10.609 8.8V12.06c0-.333-.143-.57-.429-.737l-5.97-3.473 1.95-1.118a.433.433 0 01.476 0l4.543 2.617c1.309.76 2.189 2.378 2.189 3.948 0 1.808-1.07 3.473-2.76 4.163zM7.802 12.703l-1.95-1.142c-.167-.095-.239-.238-.239-.428V5.899c0-2.545 1.95-4.472 4.591-4.472 1 0 1.927.333 2.712.928L8.23 5.067c-.285.166-.428.404-.428.737v6.898zM12 15.128l-2.795-1.57v-3.33L12 8.658l2.795 1.57v3.33L12 15.128zm1.796 7.23c-1 0-1.927-.332-2.712-.927l4.686-2.712c.285-.166.428-.404.428-.737v-6.898l1.974 1.142c.167.095.238.238.238.428v5.233c0 2.545-1.974 4.472-4.614 4.472zm-5.637-5.303l-4.544-2.617c-1.308-.761-2.188-2.378-2.188-3.948A4.482 4.482 0 014.21 6.327v5.423c0 .333.143.571.428.738l5.947 3.449-1.95 1.118a.432.432 0 01-.476 0zm-.262 3.9c-2.688 0-4.662-2.021-4.662-4.519 0-.19.024-.38.047-.57l4.686 2.71c.286.167.571.167.856 0l5.97-3.448v2.26c0 .19-.07.333-.237.428l-4.543 2.616c-.619.357-1.356.523-2.117.523zm5.899 2.83a5.947 5.947 0 005.827-4.756C22.287 18.339 24 15.84 24 13.296c0-1.665-.713-3.282-1.998-4.448.119-.5.19-.999.19-1.498 0-3.401-2.759-5.947-5.946-5.947-.642 0-1.26.095-1.88.31A5.962 5.962 0 0010.205 0a5.947 5.947 0 00-5.827 4.757C1.713 5.447 0 7.945 0 10.49c0 1.666.713 3.283 1.998 4.448-.119.5-.19 1-.19 1.499 0 3.401 2.759 5.946 5.946 5.946.642 0 1.26-.095 1.88-.309a5.96 5.96 0 004.162 1.713z";
    private readonly UsageCardControls _codexFiveHourCard;
    private readonly UsageCardControls _codexWeeklyCard;
    private readonly UsageCardControls _claudeFiveHourCard;
    private readonly UsageCardControls _claudeWeeklyCard;
    private readonly StackPanel _claudeSection;
    private readonly StackPanel _codexSection;
    private readonly StackPanel _antigravitySection;
    private readonly TextBlock _claudeHeading;
    private readonly TextBlock _codexHeading;
    private readonly List<UsageCardControls> _antigravityCards = [];
    private readonly List<TextBlock> _antigravityHeadings = [];
    private readonly List<TextBlock> _antigravityMessages = [];
    private readonly List<TextBlock> _antigravityErrorMessages = [];
    private readonly List<Border> _antigravityMessageCards = [];
    private readonly List<Border> _antigravityGroupHeaders = [];
    private UiText _text;
    private readonly Border _root;
    private Border _headerSurface = null!;
    private TextBlock _headerTitle = null!;
    private readonly TextBlock _status;
    private Ellipse _statusDot = null!;
    private readonly DispatcherTimer _countdownTimer;
    private Button _minimizeButton = null!;
    private ToggleButton _pinButton = null!;
    private Button _shortcutsButton = null!;
    private Button _historyButton = null!;
    private Button _resetEfficiencyButton = null!;
    private Button _resetPositionButton = null!;
    private Button _settingsButton = null!;
    private Button _refreshButton = null!;
    private Control _refreshIcon = null!;
    private Button _closeButton = null!;
    private string _position;
    private readonly int _margin;
    private bool _trayEnabled;
    private bool _showTaskbarIcon;
    private bool _showClaudeSession;
    private bool _showClaudeWeekly;
    private bool _showCodexFiveHour;
    private bool _showCodexWeekly;
    private bool _codexEnabled;
    private bool _claudeEnabled;
    private bool _antigravityEnabled;
    private RateLimitState? _lastCodexState;
    private RateLimitState? _lastClaudeState;
    private AntigravityUsageState? _lastAntigravityState;
    private string? _lastAntigravityError;
    private DateTimeOffset? _lastUpdatedAt;
    private string? _lastError;
    private bool _repositionAfterResize;
    private OverlayThemePalette _palette = OverlayThemePalette.Dark;

    public UsageOverlayWindow(CompanionSettings? settings = null, UiText? text = null)
    {
        settings ??= new CompanionSettings();
        _text = text ?? UiText.For(UiLanguage.English);
        _position = WindowPosition.Normalize(settings.Position);
        _margin = settings.Margin;
        _trayEnabled = settings.EnableSystemTray;
        _showTaskbarIcon = settings.ShowTaskbarIcon;
        _showClaudeSession = settings.ShowClaudeSession;
        _showClaudeWeekly = settings.ShowClaudeWeekly;
        _showCodexFiveHour = settings.ShowCodexFiveHour;
        _showCodexWeekly = settings.ShowCodexWeekly;
        _codexEnabled = settings.EnableCodexUsage;
        _claudeEnabled = settings.EnableClaudeUsage;
        _antigravityEnabled = settings.EnableAntigravityUsage;

        Title = "Claude Codex Usage Companion";
        Width = 440;
        Height = ComputeHeight();
        MinWidth = Width;
        MaxWidth = Width;
        MinHeight = Height;
        MaxHeight = Height;
        CanResize = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        ShowInTaskbar = _showTaskbarIcon;
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;
        Background = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _root = new Border
        {
            Background = Brush("#F2272927"),
            BorderBrush = Brush("#655B605B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(14),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 22,
                OffsetY = 5,
                Color = Color.Parse("#70000000")
            })
        };
        var stack = new StackPanel
        {
            Spacing = CardSpacing,
            Margin = new Thickness(0, 0, 0, ContentBottomPadding)
        };
        _status = new TextBlock
        {
            Text = _text.WaitingForData,
            Foreground = Brush("#8E938E"),
            FontSize = 10.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        var header = CreateHeader();
        _codexFiveHourCard = CreateCard(LimitBadge.FiveHour);
        _codexWeeklyCard = CreateCard(LimitBadge.Week);
        _claudeFiveHourCard = CreateCard(LimitBadge.FiveHour);
        _claudeWeeklyCard = CreateCard(LimitBadge.Week);
        _claudeSection = CreateProviderSection(
            "Claude",
            CreateClaudeIcon,
            "#22D77655",
            out _claudeHeading);
        _claudeSection.Children.Add(_claudeFiveHourCard.Container);
        _claudeSection.Children.Add(_claudeWeeklyCard.Container);
        _codexSection = CreateProviderSection(
            "Codex",
            CreateCodexIcon,
            "#2010A37F",
            out _codexHeading);
        _codexSection.Children.Add(_codexFiveHourCard.Container);
        _codexSection.Children.Add(_codexWeeklyCard.Container);
        _antigravitySection = new StackPanel { Spacing = CardSpacing };
        ApplyCardVisibility();

        stack.Children.Add(header);
        stack.Children.Add(_claudeSection);
        stack.Children.Add(_codexSection);
        stack.Children.Add(_antigravitySection);
        // The compact layout is deliberately sized to show every quota at
        // once. A scrolling dashboard hides the values the user opened it for.
        _root.Child = stack;
        RebuildAntigravitySection(applySize: false);
        Content = _root;
        _countdownTimer = new DispatcherTimer
        {
            // The displayed value is rounded to minutes. Refreshing twice a
            // minute keeps it aligned with the current clock without fetching
            // provider data more often.
            Interval = TimeSpan.FromSeconds(30)
        };
        _countdownTimer.Tick += (_, _) => RefreshCountdowns();
        ActualThemeVariantChanged += (_, _) => ApplyThemePalette();
        SizeChanged += HandleWindowSizeChanged;

        Opened += (_, _) =>
        {
            PositionOnPrimaryScreen();
            ApplyThemePalette();
            _countdownTimer.Start();
        };
        Closed += (_, _) => _countdownTimer.Stop();
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel);
    }

    public event EventHandler? RefreshRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ShortcutsRequested;
    public event EventHandler? HistoryRequested;
    public event EventHandler? ResetEfficiencyRequested;
    public event Action<bool>? AlwaysOnTopRequested;

    private void HandleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = true;
            Close();
            return;
        }

        if (eventArgs.Key == Key.F1)
        {
            eventArgs.Handled = true;
            ShortcutsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (eventArgs.Key == Key.S && eventArgs.KeyModifiers == KeyModifiers.None)
        {
            eventArgs.Handled = true;
            SettingsRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (eventArgs.Key != Key.R ||
            (eventArgs.KeyModifiers & KeyModifiers.Control) == 0)
        {
            return;
        }

        eventArgs.Handled = true;
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateUsage(UsageProvider provider, RateLimitState? state)
    {
        if (provider == UsageProvider.Codex)
        {
            _lastCodexState = state;
            UpdateCard(
                _codexFiveHourCard,
                state?.FiveHour,
                dataAvailable: state is not null);
            UpdateCard(
                _codexWeeklyCard,
                state?.Weekly,
                dataAvailable: state is not null);
            ApplyCardVisibility();
            ApplyComputedHeight();
            return;
        }

        _lastClaudeState = state;
        UpdateCard(
            _claudeFiveHourCard,
            state?.FiveHour,
            dataAvailable: state is not null);
        UpdateCard(
            _claudeWeeklyCard,
            state?.Weekly,
            dataAvailable: state is not null);
        ApplyCardVisibility();
        ApplyComputedHeight();
    }

    public void UpdateAntigravityUsage(AntigravityUsageState? state, string? error)
    {
        _lastAntigravityState = state;
        _lastAntigravityError = error;
        RebuildAntigravitySection(applySize: true);
    }

    public void SetLoading(bool loading)
    {
        _refreshButton.IsEnabled = !loading;
        _refreshButton.Content = loading
            ? new ProgressBar
            {
                Width = 13,
                Height = 3,
                IsIndeterminate = true,
                VerticalAlignment = VerticalAlignment.Center
            }
            : _refreshIcon;
    }

    public void SetStatus(DateTimeOffset? updatedAt, string? error)
    {
        _lastUpdatedAt = updatedAt;
        _lastError = error;
        if (!string.IsNullOrWhiteSpace(error))
        {
            _status.Text = error;
            _status.Foreground = Brush(_palette.ErrorText);
            _statusDot.Fill = Brush(_palette.ErrorText);
            return;
        }

        _status.Text = updatedAt is null
            ? _text.WaitingForData
            : _text.FormatUpdatedTime(updatedAt.Value);
        _status.Foreground = Brush(_palette.StatusText);
        _statusDot.Fill = Brush(updatedAt is null ? _palette.Gray : _palette.Green);
    }

    public void ApplySettings(CompanionSettings settings, UiText text)
    {
        _text = text;
        _trayEnabled = settings.EnableSystemTray;
        _showTaskbarIcon = settings.ShowTaskbarIcon;
        ShowInTaskbar = _showTaskbarIcon;
        ApplyAlwaysOnTop(settings.AlwaysOnTop);
        _showClaudeSession = settings.ShowClaudeSession;
        _showClaudeWeekly = settings.ShowClaudeWeekly;
        _showCodexFiveHour = settings.ShowCodexFiveHour;
        _showCodexWeekly = settings.ShowCodexWeekly;
        _codexEnabled = settings.EnableCodexUsage;
        _claudeEnabled = settings.EnableClaudeUsage;
        _antigravityEnabled = settings.EnableAntigravityUsage;
        _headerTitle.Text = text.CombinedUsageHeaderTitle;
        ApplyCardVisibility();
        var requestedPosition = WindowPosition.Normalize(settings.Position);
        if (!string.Equals(requestedPosition, _position, StringComparison.Ordinal))
        {
            ApplySizeAndPosition(requestedPosition);
        }
        else
        {
            // Resizing after a refresh or an unrelated settings change must
            // preserve a position chosen by dragging the window.
            ApplyComputedHeight();
        }
        ToolTip.SetTip(_minimizeButton, text.MinimizeAction);
        UpdatePinButton();
        ToolTip.SetTip(_shortcutsButton, text.ShortcutsAction);
        ToolTip.SetTip(_historyButton, text.UsageHistoryAction);
        ToolTip.SetTip(_resetEfficiencyButton, text.ResetEfficiencyAction);
        ToolTip.SetTip(_resetPositionButton, ResetPositionTooltip());
        ToolTip.SetTip(_settingsButton, text.SettingsAction);
        ToolTip.SetTip(_refreshButton, text.RefreshAction);
        ToolTip.SetTip(_closeButton, CloseTooltip());
        ApplyThemePalette();
        UpdateUsage(UsageProvider.Codex, _lastCodexState);
        UpdateUsage(UsageProvider.Claude, _lastClaudeState);
        RebuildAntigravitySection(applySize: false);
        SetStatus(_lastUpdatedAt, _lastError);
    }

    public void ApplyAlwaysOnTop(bool enabled)
    {
        Topmost = enabled;
        if (_pinButton is not null)
        {
            _pinButton.IsChecked = enabled;
            UpdatePinButton();
        }
    }

    public void RestoreAndActivate()
    {
        var wasHidden = !IsVisible;
        var restoreTopmost = wasHidden && Topmost;
        // Some X11 window managers discard _NET_WM_STATE_ABOVE and
        // _NET_WM_STATE_SKIP_TASKBAR when a window is unmapped. Change the
        // managed values before remapping so restoring them after Show()
        // sends both states to the native window again.
        if (restoreTopmost)
        {
            Topmost = false;
        }

        if (wasHidden && !_showTaskbarIcon)
        {
            ShowInTaskbar = true;
        }

        Show();
        ShowInTaskbar = _showTaskbarIcon;
        if (restoreTopmost)
        {
            Topmost = true;
        }

        WindowState = WindowState.Normal;
        Activate();
    }

    public void ApplyPosition(string position)
    {
        _position = WindowPosition.Normalize(position);
        PositionOnPrimaryScreen();
        if (_resetPositionButton is not null)
        {
            _resetPositionButton.IsVisible = false;
        }
    }

    private void ApplyCardVisibility()
    {
        var (showCodexFiveHour, showCodexWeekly) = VisibleProviderCards(
            _codexEnabled,
            _showCodexFiveHour,
            _showCodexWeekly,
            _lastCodexState);
        var (showClaudeFiveHour, showClaudeWeekly) = VisibleProviderCards(
            _claudeEnabled,
            _showClaudeSession,
            _showClaudeWeekly,
            _lastClaudeState);
        _codexFiveHourCard.Container.IsVisible = showCodexFiveHour;
        _codexWeeklyCard.Container.IsVisible = showCodexWeekly;
        _claudeFiveHourCard.Container.IsVisible = showClaudeFiveHour;
        _claudeWeeklyCard.Container.IsVisible = showClaudeWeekly;
        _claudeSection.IsVisible =
            _claudeFiveHourCard.Container.IsVisible || _claudeWeeklyCard.Container.IsVisible;
        _codexSection.IsVisible =
            _codexFiveHourCard.Container.IsVisible || _codexWeeklyCard.Container.IsVisible;
    }

    private static (bool FiveHour, bool Weekly) VisibleProviderCards(
        bool enabled,
        bool showFiveHour,
        bool showWeekly,
        RateLimitState? state)
    {
        if (!enabled)
        {
            return (false, false);
        }

        var hasUsage = state?.FiveHour is not null || state?.Weekly is not null;
        var fiveHourVisible = showFiveHour;
        // A provider with no data needs one compact status card, not two
        // identical placeholders. Keep Weekly when it is the only enabled view.
        var weeklyVisible = showWeekly && (hasUsage || !fiveHourVisible);
        return (fiveHourVisible, weeklyVisible);
    }

    private double ComputeHeight()
    {
        var sectionHeights = new List<double>();
        var (showClaudeFiveHour, showClaudeWeekly) = VisibleProviderCards(
            _claudeEnabled,
            _showClaudeSession,
            _showClaudeWeekly,
            _lastClaudeState);
        var claudeCardHeights = new List<double>();
        if (showClaudeFiveHour)
        {
            claudeCardHeights.Add(CardHeight(_lastClaudeState?.FiveHour, FiveHourCardHeight));
        }
        if (showClaudeWeekly)
        {
            claudeCardHeights.Add(CardHeight(_lastClaudeState?.Weekly, WeeklyCardHeight));
        }
        if (claudeCardHeights.Count > 0)
        {
            sectionHeights.Add(ComputeProviderSectionHeight(claudeCardHeights));
        }

        var (showCodexFiveHour, showCodexWeekly) = VisibleProviderCards(
            _codexEnabled,
            _showCodexFiveHour,
            _showCodexWeekly,
            _lastCodexState);
        var codexCardHeights = new List<double>();
        if (showCodexFiveHour)
        {
            codexCardHeights.Add(CardHeight(_lastCodexState?.FiveHour, FiveHourCardHeight));
        }
        if (showCodexWeekly)
        {
            codexCardHeights.Add(CardHeight(_lastCodexState?.Weekly, WeeklyCardHeight));
        }
        if (codexCardHeights.Count > 0)
        {
            sectionHeights.Add(ComputeProviderSectionHeight(codexCardHeights));
        }
        var antigravityHeight = ComputeAntigravitySectionHeight();
        if (antigravityHeight > 0)
        {
            sectionHeights.Add(antigravityHeight);
        }
        return BaseHeight + ContentBottomPadding + sectionHeights.Sum() +
               (CardSpacing * sectionHeights.Count);
    }

    private static double ComputeProviderSectionHeight(IReadOnlyCollection<double> cardHeights) =>
        ProviderSectionTitleHeight + cardHeights.Sum() + (cardHeights.Count * CardSpacing);

    private static double CardHeight(RateLimitWindowState? state, double availableHeight) =>
        state is null ? UnavailableCardHeight : availableHeight;

    private double ComputeAntigravitySectionHeight()
    {
        var presentation = UsagePresentation.BuildAntigravityPresentation(
            _antigravityEnabled,
            _lastAntigravityState,
            _lastAntigravityError);
        return presentation.Kind switch
        {
            AntigravityPresentationKind.Hidden => 0,
            AntigravityPresentationKind.QuotaPools => AntigravitySectionTitleHeight +
                presentation.Pools.Sum(pool =>
                    CardSpacing + AntigravityGroupTitleHeight +
                    (pool.Windows.Count * (AntigravityWindowCardHeight + CardSpacing))) +
                (string.IsNullOrWhiteSpace(presentation.Error)
                    ? 0
                    : AntigravityErrorCardHeight + CardSpacing),
            AntigravityPresentationKind.Error =>
                AntigravitySectionTitleHeight + AntigravityErrorCardHeight + CardSpacing,
            _ => AntigravitySectionTitleHeight + AntigravityMessageCardHeight + CardSpacing
        };
    }

    private void RebuildAntigravitySection(bool applySize)
    {
        var presentation = UsagePresentation.BuildAntigravityPresentation(
            _antigravityEnabled,
            _lastAntigravityState,
            _lastAntigravityError);
        _antigravitySection.Children.Clear();
        _antigravityCards.Clear();
        _antigravityHeadings.Clear();
        _antigravityMessages.Clear();
        _antigravityErrorMessages.Clear();
        _antigravityMessageCards.Clear();
        _antigravityGroupHeaders.Clear();
        _antigravitySection.IsVisible = presentation.Kind != AntigravityPresentationKind.Hidden;
        if (_antigravitySection.IsVisible)
        {
            AddAntigravityHeading(_text.AntigravityTitle, sectionTitle: true);
            if (presentation.Kind == AntigravityPresentationKind.QuotaPools)
            {
                foreach (var pool in presentation.Pools)
                {
                    AddAntigravityHeading(CompactPoolName(pool.Name), sectionTitle: false);
                    foreach (var window in pool.Windows)
                    {
                        AddAntigravityWindow(window);
                    }
                }

                if (!string.IsNullOrWhiteSpace(presentation.Error))
                {
                    AddAntigravityMessage(presentation.Error, error: true);
                }
            }
            else
            {
                var message = presentation.Kind switch
                {
                    AntigravityPresentationKind.ObservedModelFallback => _text.AntigravityObservedFallback,
                    AntigravityPresentationKind.Error => presentation.Error ?? _text.WaitingForData,
                    _ => _text.WaitingForData
                };
                AddAntigravityMessage(message, presentation.Kind == AntigravityPresentationKind.Error);
            }
        }

        ApplyAntigravityTheme();
        if (applySize)
        {
            // Provider data can change the overlay height. Keep the current
            // top-left coordinate instead of snapping a manually moved window
            // back to its configured anchor.
            ApplyComputedHeight();
        }
    }

    private static StackPanel CreateProviderSection(
        string providerName,
        Func<Control> createIcon,
        string iconBackground,
        out TextBlock heading)
    {
        var section = new StackPanel { Spacing = CardSpacing };
        var icon = createIcon();
        icon.Width = 14;
        icon.Height = 14;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var iconBadge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(7),
            Background = Brush(iconBackground),
            Child = icon
        };
        heading = new TextBlock
        {
            Text = providerName,
            FontSize = 13.5,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var headingRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Height = ProviderSectionTitleHeight,
            Margin = new Thickness(1, 2, 0, 0)
        };
        headingRow.Children.Add(iconBadge);
        headingRow.Children.Add(heading);
        section.Children.Add(headingRow);
        return section;
    }

    private void AddAntigravityHeading(string text, bool sectionTitle)
    {
        var heading = new TextBlock
        {
            Text = text,
            FontSize = sectionTitle ? 13.5 : 10.5,
            FontWeight = sectionTitle ? FontWeight.Bold : FontWeight.SemiBold,
            Margin = new Thickness(0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _antigravityHeadings.Add(heading);
        if (!sectionTitle)
        {
            var groupHeader = new Border
            {
                Height = AntigravityGroupTitleHeight,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 1),
                Margin = new Thickness(4, 0, 0, 0),
                Child = heading
            };
            _antigravityGroupHeaders.Add(groupHeader);
            _antigravitySection.Children.Add(groupHeader);
            return;
        }

        var icon = CreateAntigravityIcon();
        icon.Width = 14;
        icon.Height = 14;
        icon.VerticalAlignment = VerticalAlignment.Center;
        var iconBadge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(7),
            Background = Brush("#204285F4"),
            Child = icon
        };
        var headingRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Height = AntigravitySectionTitleHeight,
            Margin = new Thickness(1, 2, 0, 0)
        };
        headingRow.Children.Add(iconBadge);
        headingRow.Children.Add(heading);
        _antigravitySection.Children.Add(headingRow);
    }

    private void AddAntigravityWindow(AntigravityQuotaWindowState window)
    {
        var badge = window.Cadence == AntigravityQuotaCadence.Weekly
            ? LimitBadge.Week
            : LimitBadge.FiveHour;
        var card = CreateCard(badge);
        card.Container.Margin = new Thickness(4, 0, 0, 0);
        UpdateAntigravityCard(card, window);
        _antigravityCards.Add(card);
        _antigravitySection.Children.Add(card.Container);
    }

    private static string CompactPoolName(string name) =>
        string.Equals(name, "Gemini Models", StringComparison.OrdinalIgnoreCase)
            ? "Gemini"
            : name;

    private void AddAntigravityMessage(string message, bool error)
    {
        var text = new TextBlock
        {
            Text = message,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var card = new Border
        {
            Height = error ? AntigravityErrorCardHeight : AntigravityMessageCardHeight,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6),
            Child = text
        };
        _antigravityMessages.Add(text);
        if (error)
        {
            _antigravityErrorMessages.Add(text);
        }
        _antigravityMessageCards.Add(card);
        _antigravitySection.Children.Add(card);
    }

    private void ApplyComputedHeight()
    {
        var targetHeight = ComputeHeight();
        if (Math.Abs(Height - targetHeight) < 0.1 &&
            Math.Abs(MinHeight - targetHeight) < 0.1 &&
            Math.Abs(MaxHeight - targetHeight) < 0.1)
        {
            return;
        }

        MinHeight = Math.Min(MinHeight, targetHeight);
        MaxHeight = Math.Max(MaxHeight, targetHeight);
        Height = targetHeight;
        MinHeight = targetHeight;
        MaxHeight = targetHeight;
    }

    private void ApplySizeAndPosition(string position)
    {
        _position = WindowPosition.Normalize(position);
        _repositionAfterResize = true;
        ApplyComputedHeight();
        PositionOnPrimaryScreen();
        Dispatcher.UIThread.Post(
            CompletePendingReposition,
            DispatcherPriority.Background);
    }

    private void HandleWindowSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        if (_repositionAfterResize)
        {
            PositionOnPrimaryScreen();
        }
    }

    private void CompletePendingReposition()
    {
        if (!_repositionAfterResize)
        {
            return;
        }

        _repositionAfterResize = false;
        PositionOnPrimaryScreen();
    }

    private Control CreateHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        _headerTitle = new TextBlock
        {
            Text = _text.CombinedUsageHeaderTitle,
            Foreground = Brush("#CDD1CD"),
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.7,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        _statusDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = Brush("#8E938E"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 5
        };
        Grid.SetColumn(_status, 1);
        statusRow.Children.Add(_statusDot);
        statusRow.Children.Add(_status);
        var titleArea = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleArea.Children.Add(_headerTitle);
        titleArea.Children.Add(statusRow);
        titleArea.PointerPressed += HandleHeaderPointerPressed;
        grid.Children.Add(titleArea);

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(toolbar, 1);

        _shortcutsButton = HeaderButton(CreateHeaderIcon(
            "M12 2A10 10 0 1 0 12 22A10 10 0 0 0 12 2ZM13 19H11V17H13V19ZM15.07 11.25L14.17 12.17C13.45 12.9 13 13.5 13 15H11V14.5C11 13.4 11.45 12.4 12.17 11.67L13.41 10.41C13.78 10.05 14 9.55 14 9C14 7.9 13.1 7 12 7S10 7.9 10 9H8C8 6.79 9.79 5 12 5S16 6.79 16 9C16 9.88 15.64 10.68 15.07 11.25Z"), _text.ShortcutsAction);
        _shortcutsButton.Click += (_, _) => ShortcutsRequested?.Invoke(this, EventArgs.Empty);

        _historyButton = HeaderButton(CreateHeaderIcon(
            "M12 2A10 10 0 1 0 12 22A10 10 0 0 0 12 2ZM12 4A8 8 0 1 1 12 20A8 8 0 0 1 12 4ZM11 7H13V11.4L16.8 13.6L15.8 15.3L11 12.5V7Z"), _text.UsageHistoryAction);
        _historyButton.Click += (_, _) => HistoryRequested?.Invoke(this, EventArgs.Empty);

        _resetEfficiencyButton = HeaderButton(CreateHeaderIcon(
            "M11 2V12H21C21 6.48 16.52 2 11 2ZM9 4.07C4.94 4.56 2 8.03 2 12C2 16.42 5.58 20 10 20C13.97 20 17.44 17.06 17.93 13H9V4.07Z"), _text.ResetEfficiencyAction);
        _resetEfficiencyButton.Click += (_, _) => ResetEfficiencyRequested?.Invoke(this, EventArgs.Empty);

        _resetPositionButton = HeaderButton(CreateHeaderIcon(
            "M11 2H13V5.08C16.61 5.53 19.47 8.39 19.92 12H23V14H19.92C19.47 17.61 16.61 20.47 13 20.92V24H11V20.92C7.39 20.47 4.53 17.61 4.08 14H1V12H4.08C4.53 8.39 7.39 5.53 11 5.08V2ZM12 7C8.69 7 6 9.69 6 13S8.69 19 12 19 18 16.31 18 13 15.31 7 12 7ZM12 10A3 3 0 1 0 12 16A3 3 0 0 0 12 10Z"), ResetPositionTooltip());
        _resetPositionButton.IsVisible = false;
        _resetPositionButton.Click += (_, _) =>
        {
            PositionOnPrimaryScreen();
            _resetPositionButton.IsVisible = false;
        };

        _pinButton = HeaderToggleButton(CreatePinIcon(), string.Empty);
        _pinButton.IsChecked = Topmost;
        _pinButton.Click += (_, _) =>
            AlwaysOnTopRequested?.Invoke(_pinButton.IsChecked == true);
        UpdatePinButton();

        _settingsButton = HeaderButton(CreateHeaderIcon(
            "M19.43 12.98C19.47 12.66 19.5 12.34 19.5 12S19.47 11.34 19.42 11L21.54 9.35L19.54 5.89L17.05 6.89C16.55 6.5 16 6.18 15.38 5.94L15 3.29H11L10.62 5.94C10 6.18 9.45 6.5 8.95 6.89L6.46 5.89L4.46 9.35L6.58 11C6.53 11.34 6.5 11.67 6.5 12S6.53 12.66 6.58 13L4.46 14.65L6.46 18.11L8.95 17.11C9.45 17.5 10 17.82 10.62 18.06L11 20.71H15L15.38 18.06C16 17.82 16.55 17.5 17.05 17.11L19.54 18.11L21.54 14.65L19.43 12.98ZM13 15.5A3.5 3.5 0 1 1 13 8.5A3.5 3.5 0 0 1 13 15.5Z"), _text.SettingsAction);
        _settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        _refreshIcon = CreateHeaderIcon(
            "M17.65 6.35C16.2 4.9 14.21 4 12 4C7.58 4 4 7.58 4 12S7.58 20 12 20C15.73 20 18.84 17.45 19.73 14H17.65C16.83 16.33 14.61 18 12 18C8.69 18 6 15.31 6 12S8.69 6 12 6C13.66 6 15.14 6.69 16.22 7.78L13 11H21V3L17.65 6.35Z");
        _refreshButton = HeaderButton(_refreshIcon, _text.RefreshAction);
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        _minimizeButton = HeaderButton(CreateHeaderIcon("M4 11H20V13H4Z"), _text.MinimizeAction);
        // A real unmap works on both X11 and native Wayland.  WindowState.Minimized
        // is only advisory on Wayland and can leave a borderless overlay visible.
        _minimizeButton.Click += (_, _) => Hide();
        // Sets the window controls apart from the panel actions before them.
        _minimizeButton.Margin = new Thickness(HeaderGroupSpacing, 0, 0, 0);

        _closeButton = HeaderButton(CreateHeaderIcon(
            "M6.7 5.3L12 10.6L17.3 5.3L18.7 6.7L13.4 12L18.7 17.3L17.3 18.7L12 13.4L6.7 18.7L5.3 17.3L10.6 12L5.3 6.7Z"), CloseTooltip());
        _closeButton.Click += (_, _) => Close();

        toolbar.Children.Add(_shortcutsButton);
        toolbar.Children.Add(_historyButton);
        toolbar.Children.Add(_resetEfficiencyButton);
        toolbar.Children.Add(_resetPositionButton);
        toolbar.Children.Add(_pinButton);
        toolbar.Children.Add(_settingsButton);
        toolbar.Children.Add(_refreshButton);
        toolbar.Children.Add(_minimizeButton);
        toolbar.Children.Add(_closeButton);
        grid.Children.Add(toolbar);

        _headerSurface = new Border
        {
            CornerRadius = new CornerRadius(13),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 7),
            Child = grid
        };
        return _headerSurface;
    }

    private string CloseTooltip() =>
        _trayEnabled ? _text.HideToTrayAction : _text.CloseAction;

    private string ResetPositionTooltip() => _text.Language switch
    {
        UiLanguage.TraditionalChinese => "重設為設定的位置",
        UiLanguage.SimplifiedChinese => "重置为设置的位置",
        _ => "Reset to configured position"
    };

    private static Button HeaderButton(Control content, string tooltip)
    {
        var button = new Button
        {
            Content = content,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            UseLayoutRounding = true
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static ToggleButton HeaderToggleButton(Control content, string tooltip)
    {
        var button = new ToggleButton
        {
            Content = content,
            Width = 24,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            UseLayoutRounding = true
        };
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Control CreatePinIcon() => new PathIcon
    {
        Width = 12,
        Height = 12,
        Data = Geometry.Parse(
            "M16 9V4L17 3V2H7V3L8 4V9C8 10.1 7.1 11 6 11V13H11V20L12 21L13 20V13H18V11C16.9 11 16 10.1 16 9Z")
    };

    private static Control CreateHeaderIcon(string geometry) => new PathIcon
    {
        Width = 12,
        Height = 12,
        Data = Geometry.Parse(geometry),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void UpdatePinButton()
    {
        var pinned = _pinButton.IsChecked == true;
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        _pinButton.Opacity = 1d;
        _pinButton.Foreground = Brush(pinned ? _palette.Orange : _palette.SecondaryText);
        _pinButton.Background = Brush(pinned
            ? isLight ? "#FFFFE9DA" : "#FF49301F"
            : isLight ? "#FFFFFFFF" : "#FF2C302D");
        _pinButton.BorderBrush = Brush(pinned
            ? isLight ? "#FFFFC69D" : "#FF81502C"
            : isLight ? "#FFDCE2DC" : "#FF424743");
        ToolTip.SetTip(
            _pinButton,
            pinned ? _text.UnpinFromTopAction : _text.PinOnTopAction);
    }

    private static Control CreateClaudeIcon()
    {
        var canvas = new Canvas { Width = 512, Height = 509.64 };
        canvas.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(
                "M115.612 0h280.775C459.974 0 512 52.026 512 115.612v278.415c0 63.587-52.026 115.612-115.613 115.612H115.612C52.026 509.639 0 457.614 0 394.027V115.612C0 52.026 52.026 0 115.612 0z"),
            Fill = Brush(ClaudeIconBackground)
        });
        canvas.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(ClaudeIconPath),
            Fill = Brush(ClaudeIconForeground)
        });
        return new Viewbox { Width = IconSize, Height = IconSize, Child = canvas };
    }

    private static Control CreateCodexIcon()
    {
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(CodexIconPath),
            Fill = Brush(CodexAccentColor)
        });
        return new Viewbox { Width = IconSize, Height = IconSize, Child = canvas };
    }

    private static Control CreateAntigravityIcon()
    {
        // Antigravity quota pools are Gemini pools. Use Gemini's four-point
        // sparkle rather than the temporary letter-A placeholder.
        var sparkle = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(
                "M12 0C13.4 8.6 15.4 10.6 24 12C15.4 13.4 13.4 15.4 12 24C10.6 15.4 8.6 13.4 0 12C8.6 10.6 10.6 8.6 12 0Z"),
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#4285F4"), 0),
                    new GradientStop(Color.Parse("#8E6CEF"), 0.5),
                    new GradientStop(Color.Parse("#24C1E0"), 1)
                }
            }
        };
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(sparkle);
        var icon = new Viewbox { Width = IconSize, Height = IconSize, Child = canvas };
        ToolTip.SetTip(icon, "Gemini");
        return icon;
    }

    private static UsageCardControls CreateCard(LimitBadge badgeKind)
    {
        var container = new Border
        {
            Height = UnavailableCardHeight,
            Background = Brush("#FF353835"),
            BorderBrush = Brush("#FF4A4E4A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 10),
            ClipToBounds = true
        };
        var railText = new TextBlock
        {
            Text = BadgeText(badgeKind),
            FontSize = 9.5,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 0.45,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var rail = new Border
        {
            MinWidth = 43,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = railText
        };
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };
        var informationRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };
        var remaining = new TextBlock
        {
            Foreground = Brush("#9CA09C"),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(remaining, 2);
        var reset = new TextBlock
        {
            Foreground = Brush("#B7BAB6"),
            FontSize = 11.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(9, 0, 6, 0)
        };
        Grid.SetColumn(reset, 1);
        informationRow.Children.Add(reset);
        informationRow.Children.Add(remaining);
        informationRow.Children.Add(rail);
        Grid.SetColumn(rail, 0);
        var fill = new Border
        {
            Width = 0,
            Height = 7,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(3.5)
        };
        var bar = new Border
        {
            Height = 7,
            Background = Brush("#FF4A4D49"),
            CornerRadius = new CornerRadius(3.5),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 7, 0, 0),
            Child = fill
        };

        Grid.SetRow(bar, 1);
        content.Children.Add(informationRow);
        content.Children.Add(bar);
        container.Child = content;
        var card = new UsageCardControls(
            container,
            rail,
            railText,
            badgeKind,
            remaining,
            reset,
            bar,
            fill);
        bar.SizeChanged += (_, _) => UpdateProgressWidth(card);
        return card;
    }

    private void UpdateCard(
        UsageCardControls card,
        RateLimitWindowState? state,
        bool dataAvailable)
    {
        var targetHeight = CardHeight(
            state,
            card.BadgeKind == LimitBadge.Week ? WeeklyCardHeight : FiveHourCardHeight);
        card.Container.Height = targetHeight;
        if (state is null)
        {
            card.Container.Padding = new Thickness(10, 10);
            card.BadgeText.Text = dataAvailable
                ? BadgeText(card.BadgeKind)
                : "…";
            card.Remaining.Text = "--";
            card.Reset.Text = dataAvailable ? _text.LimitUnavailable : _text.WaitingForData;
            card.ProgressTrack.IsVisible = false;
            ApplyBar(card, 0, UsageSignal.Gray);
            return;
        }

        card.Container.Padding = new Thickness(10, 8);
        card.ProgressTrack.IsVisible = true;
        card.BadgeText.Text = BadgeText(card.BadgeKind);
        card.Remaining.Text = FormatPercent(state.RemainingPercent);
        card.Reset.Text = state.ResetsAt is long unixSeconds
            ? _text.FormatResetWithCountdown(
                DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime(),
                DateTimeOffset.Now)
            : _text.ResetUnavailable;
        ApplyBar(card, state.RemainingPercent, UsagePresentation.GetSignal(state.RemainingPercent));
    }

    private void UpdateAntigravityCard(
        UsageCardControls card,
        AntigravityQuotaWindowState window)
    {
        card.Container.Height = AntigravityWindowCardHeight;
        card.Container.Padding = new Thickness(10, 8);
        card.ProgressTrack.IsVisible = true;
        card.Remaining.Text = FormatPercent(window.RemainingPercent);
        card.Reset.Text = window.ResetAt is { } resetAt
            ? _text.FormatResetWithCountdown(resetAt.ToLocalTime(), DateTimeOffset.Now)
            : _text.ResetUnavailable;
        ApplyBar(card, window.RemainingPercent, UsagePresentation.GetSignal(window.RemainingPercent));
    }

    private void RefreshCountdowns()
    {
        UpdateUsage(UsageProvider.Codex, _lastCodexState);
        UpdateUsage(UsageProvider.Claude, _lastClaudeState);
        RebuildAntigravitySection(applySize: false);
    }

    private void ApplyBar(UsageCardControls card, int remainingPercent, UsageSignal signal)
    {
        var color = SignalBrush(signal);
        card.RemainingPercent = Math.Clamp(remainingPercent, 0, 100);
        card.Remaining.Foreground = color;
        card.ProgressFill.Background = color;
        UpdateProgressWidth(card);
    }

    private static string FormatPercent(int percent) => $"{Math.Clamp(percent, 0, 100)}%";

    private static string BadgeText(LimitBadge badgeKind) =>
        badgeKind == LimitBadge.Week ? "WEEK" : "5 HR";

    private static void UpdateProgressWidth(UsageCardControls card) =>
        card.ProgressFill.Width = Math.Round(
            card.ProgressTrack.Bounds.Width * card.RemainingPercent / 100d);

    private void HandleHeaderPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _resetPositionButton.IsVisible = true;
            BeginMoveDrag(eventArgs);
        }
    }

    private void PositionOnPrimaryScreen()
    {
        var screen = Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Width * screen.Scaling);
        var height = (int)Math.Ceiling(Height * screen.Scaling);
        var margin = (int)Math.Ceiling(_margin * screen.Scaling);
        var x = _position switch
        {
            WindowPosition.LeftTop or
            WindowPosition.LeftCenter or
            WindowPosition.LeftBottom => area.X + margin,
            WindowPosition.MiddleTop or
            WindowPosition.MiddleCenter or
            WindowPosition.MiddleBottom => area.X + ((area.Width - width) / 2),
            _ => area.Right - width - margin
        };
        var y = _position switch
        {
            WindowPosition.LeftTop or
            WindowPosition.MiddleTop or
            WindowPosition.RightTop => area.Y + margin,
            WindowPosition.LeftCenter or
            WindowPosition.MiddleCenter or
            WindowPosition.RightCenter => area.Y + ((area.Height - height) / 2),
            _ => area.Bottom - height - margin
        };
        Position = new PixelPoint(x, y);
    }

    private void ApplyThemePalette()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        _palette = isLight
            ? OverlayThemePalette.Light
            : OverlayThemePalette.Dark;
        _root.Background = Brush(_palette.RootBackground);
        _root.BorderBrush = Brush(_palette.RootBorder);
        _root.BoxShadow = _palette.Shadow == "#00000000"
            ? default
            : new BoxShadows(new BoxShadow
            {
                Blur = 22,
                OffsetY = 5,
                Color = Color.Parse(_palette.Shadow)
            });
        _headerTitle.Foreground = Brush(_palette.HeaderForeground);
        _headerSurface.Background = Brush(isLight ? "#FFF5F7F5" : "#FF202321");
        _headerSurface.BorderBrush = Brush(isLight ? "#FFE0E5DF" : "#FF3B403C");
        foreach (var button in HeaderButtons())
        {
            button.Background = Brush(isLight ? "#FFFFFFFF" : "#FF2C302D");
            button.BorderBrush = Brush(isLight ? "#FFDCE2DC" : "#FF424743");
            button.Foreground = Brush(_palette.SecondaryText);
        }
        _refreshButton.Foreground = Brush(_palette.Green);
        _resetPositionButton.Foreground = Brush(_palette.Orange);
        _closeButton.Background = Brush(isLight ? "#FFFFF1F0" : "#FF3C2928");
        _closeButton.BorderBrush = Brush(isLight ? "#FFFFD0CC" : "#FF69413E");
        _closeButton.Foreground = Brush(_palette.ErrorText);
        UpdatePinButton();
        _claudeHeading.Foreground = Brush(_palette.CardTitle);
        _codexHeading.Foreground = Brush(_palette.CardTitle);
        ApplyCardTheme(_codexFiveHourCard);
        ApplyCardTheme(_codexWeeklyCard);
        ApplyCardTheme(_claudeFiveHourCard);
        ApplyCardTheme(_claudeWeeklyCard);
        ApplyAntigravityTheme();
        SetStatus(_lastUpdatedAt, _lastError);
        UpdateUsage(UsageProvider.Codex, _lastCodexState);
        UpdateUsage(UsageProvider.Claude, _lastClaudeState);
    }

    private void ApplyAntigravityTheme()
    {
        foreach (var heading in _antigravityHeadings)
        {
            heading.Foreground = Brush(_palette.CardTitle);
        }

        foreach (var card in _antigravityCards)
        {
            ApplyCardTheme(card);
        }

        var isLight = ActualThemeVariant == ThemeVariant.Light;
        foreach (var groupHeader in _antigravityGroupHeaders)
        {
            groupHeader.Background = Brush(isLight ? "#FFF3F5F2" : "#FF2B2E2B");
            groupHeader.BorderBrush = Brush(_palette.CardBorder);
            groupHeader.BorderThickness = new Thickness(1);
            if (groupHeader.Child is TextBlock groupLabel)
            {
                groupLabel.Foreground = Brush(_palette.SecondaryText);
            }
        }

        foreach (var card in _antigravityMessageCards)
        {
            card.Background = Brush(_palette.CardBackground);
            card.BorderBrush = Brush(_palette.CardBorder);
        }

        foreach (var message in _antigravityMessages)
        {
            message.Foreground = Brush(
                _antigravityErrorMessages.Contains(message)
                    ? _palette.ErrorText
                    : _palette.SecondaryText);
        }
    }

    private void ApplyCardTheme(UsageCardControls card)
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        card.Container.Background = Brush(_palette.CardBackground);
        card.Container.BorderBrush = Brush(_palette.CardBorder);
        card.Container.BoxShadow = isLight
            ? new BoxShadows(new BoxShadow
            {
                Blur = 8,
                OffsetY = 2,
                Color = Color.Parse("#12000000")
            })
            : default;
        var badgeColors = BadgeColors(card.BadgeKind, isLight);
        card.Badge.Background = Brush(badgeColors.Background);
        card.Badge.BorderBrush = Brush(badgeColors.Border);
        card.BadgeText.Foreground = Brush(badgeColors.Foreground);
        card.Reset.Foreground = Brush(_palette.SecondaryText);
        card.ProgressTrack.Background = Brush(_palette.EmptyCell);
    }

    private static BadgePalette BadgeColors(LimitBadge badgeKind, bool isLight) =>
        (badgeKind, isLight) switch
        {
            (LimitBadge.FiveHour, true) => new BadgePalette("#FFF1E5", "#D66700", "#F5C997"),
            (LimitBadge.Week, true) => new BadgePalette("#EAF0FF", "#3867CE", "#B9CBF8"),
            (LimitBadge.FiveHour, false) => new BadgePalette("#54320F", "#FFD091", "#976323"),
            _ => new BadgePalette("#222D5E", "#B8C8FF", "#5268B6")
        };

    private IEnumerable<Button> HeaderButtons()
    {
        yield return _shortcutsButton;
        yield return _historyButton;
        yield return _resetEfficiencyButton;
        yield return _resetPositionButton;
        yield return _pinButton;
        yield return _settingsButton;
        yield return _refreshButton;
        yield return _minimizeButton;
        yield return _closeButton;
    }

    private SolidColorBrush SignalBrush(UsageSignal signal) => signal switch
    {
        UsageSignal.Green => Brush(_palette.Green),
        UsageSignal.Yellow => Brush(_palette.Yellow),
        UsageSignal.Orange => Brush(_palette.Orange),
        UsageSignal.Red => Brush(_palette.Red),
        _ => Brush(_palette.Gray)
    };

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    private enum LimitBadge
    {
        FiveHour,
        Week
    }

    private sealed record BadgePalette(string Background, string Foreground, string Border);

    private sealed record UsageCardControls(
        Border Container,
        Border Badge,
        TextBlock BadgeText,
        LimitBadge BadgeKind,
        TextBlock Remaining,
        TextBlock Reset,
        Border ProgressTrack,
        Border ProgressFill)
    {
        public int RemainingPercent { get; set; }
    }
}
