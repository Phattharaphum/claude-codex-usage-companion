using Avalonia.Threading;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.RateLimits;
using CodexUsageCompanion.Ui;

namespace CodexUsageCompanion.Lifecycle;

public sealed class CompanionRuntime : IAsyncDisposable
{
    private readonly ResidentLease _lease;
    private readonly UsageOverlayWindow _window;
    private readonly CodexAppServerClient _appServerClient;
    private readonly ClaudeUsageClient _claudeClient;
    private readonly UsageProviderActivation _codexActivation;
    private readonly UsageProviderActivation _claudeActivation;
    private readonly RefreshCoordinator _codexRefresher;
    private readonly RefreshCoordinator _claudeRefresher;
    private readonly RefreshCoordinator _antigravityRefresher;
    private readonly AntigravityRuntimeProvider _antigravityProvider;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly UsageStatusTracker _status = new();
    private TimeSpan _refreshInterval;
    private PeriodicTimer? _periodicTimer;
    private Task? _periodicTask;
    private bool _hasCodexUsageState;
    private bool _hasClaudeUsageState;
    private DateTimeOffset? _lastClaudeFetchAt;
    private CancellationToken _lastClaudeActivationToken;
    private int _activeRefreshCount;
    private int _stopping;
    private int _disposed;
    private bool _started;

    public CompanionRuntime(
        ResidentLease lease,
        UsageOverlayWindow window,
        CompanionSettings settings)
        : this(lease, window, settings, null)
    {
    }

    internal CompanionRuntime(
        ResidentLease lease,
        UsageOverlayWindow window,
        CompanionSettings settings,
        Func<IAntigravityUsageReader>? createAntigravityReader)
    {
        _lease = lease;
        _window = window;
        _refreshInterval = TimeSpan.FromSeconds(UpdateIntervalOptions.Normalize(settings.RefreshIntervalSeconds));
        // Constructing a client does not connect it. Keep it available so settings
        // can enable a provider immediately without restarting the application.
        _appServerClient = new CodexAppServerClient();
        _claudeClient = new ClaudeUsageClient();
        _codexActivation = new UsageProviderActivation(settings.EnableCodexUsage);
        _claudeActivation = new UsageProviderActivation(settings.EnableClaudeUsage);
        _antigravityProvider = new AntigravityRuntimeProvider(
            settings.EnableAntigravityUsage,
            createAntigravityReader ?? (() => new AntigravityUsageClient()),
            Program.FriendlyAntigravityError);
        _codexRefresher = new RefreshCoordinator(RefreshCodexAsync);
        _claudeRefresher = new RefreshCoordinator(RefreshClaudeAsync);
        _antigravityRefresher = new RefreshCoordinator(RefreshAntigravityAsync);
    }

    public event Action<UsageProvider, RateLimitState, DateTimeOffset>? UsageUpdated;
    public event Action<UsageProvider, string, DateTimeOffset>? UsageUpdateFailed;
    public event Action<AntigravityUsageState?, string?, DateTimeOffset>? AntigravityUsageChanged
    {
        add => _antigravityProvider.UsageChanged += value;
        remove => _antigravityProvider.UsageChanged -= value;
    }

    public void Start()
    {
        if (_started || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _started = true;
        CompanionLog.Shared.Write("lifecycle", "resident-started platform=linux");
        _window.RefreshRequested += HandleRefreshRequested;
        _appServerClient.RateLimitsChanged += HandleRateLimitsChanged;

        _lease.MessageReceived += HandleInstanceMessage;
        _lease.Start();
        _periodicTimer = new PeriodicTimer(_refreshInterval);
        _periodicTask = RunPeriodicRefreshAsync(_periodicTimer, _cancellation.Token);
        RequestRefresh();
    }

    public void UpdateRefreshInterval(int seconds)
    {
        if (Volatile.Read(ref _stopping) != 0)
        {
            return;
        }

        _refreshInterval = TimeSpan.FromSeconds(UpdateIntervalOptions.Normalize(seconds));
        if (_periodicTimer is not null)
        {
            _periodicTimer.Period = _refreshInterval;
        }
    }

    public void UpdateSettings(CompanionSettings settings)
    {
        if (Volatile.Read(ref _stopping) != 0)
        {
            return;
        }

        UpdateRefreshInterval(settings.RefreshIntervalSeconds);
        var codexChanged = _codexActivation.SetEnabled(settings.EnableCodexUsage);
        var claudeChanged = _claudeActivation.SetEnabled(settings.EnableClaudeUsage);
        var antigravityChanged = _antigravityProvider.SetEnabled(settings.EnableAntigravityUsage);
        if (codexChanged)
        {
            _hasCodexUsageState = false;
            _window.UpdateUsage(UsageProvider.Codex, null);
            _status.Update("Codex", null, null);
        }

        if (claudeChanged)
        {
            _hasClaudeUsageState = false;
            _window.UpdateUsage(UsageProvider.Claude, null);
            _status.Update("Claude", null, null);
        }

        if (antigravityChanged)
        {
            _status.Update("Antigravity", null, null);
        }

        if (codexChanged || claudeChanged || antigravityChanged)
        {
            PublishStatus();
        }

        if (_started &&
            ((codexChanged && settings.EnableCodexUsage) ||
             (claudeChanged && settings.EnableClaudeUsage) ||
             (antigravityChanged && settings.EnableAntigravityUsage)))
        {
            RequestRefresh();
        }
    }

    public void RefreshUsage()
    {
        RequestRefresh();
    }

    public void UpdateAntigravityStatus(DateTimeOffset? updatedAt, string? error)
    {
        if (Volatile.Read(ref _stopping) != 0)
        {
            return;
        }

        _status.Update("Antigravity", updatedAt, error);
        PublishStatus();
    }

    private void PublishStatus() => _window.SetStatus(_status.LastUpdatedAt, _status.Error);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _stopping, 1);
        _started = false;
        _window.RefreshRequested -= HandleRefreshRequested;
        _appServerClient.RateLimitsChanged -= HandleRateLimitsChanged;

        _lease.MessageReceived -= HandleInstanceMessage;
        _codexActivation.Dispose();
        _claudeActivation.Dispose();
        _cancellation.Cancel();
        _periodicTimer?.Dispose();
        var failures = new List<Exception>();
        if (_periodicTask is not null)
        {
            try
            {
                await _periodicTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        _periodicTimer = null;
        var refresherDisposals = new[] { _codexRefresher, _claudeRefresher, _antigravityRefresher }
            .Select(refresher => refresher.DisposeAsync().AsTask())
            .ToArray();
        foreach (var disposal in refresherDisposals)
        {
            try
            {
                await disposal;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        foreach (var client in new IAsyncDisposable[] { _appServerClient, _claudeClient, _antigravityProvider })
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }
        _cancellation.Dispose();

        try
        {
            await _lease.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        CompanionLog.Shared.Write("lifecycle", "resident-stopped");
        if (failures.Count == 1)
        {
            throw failures[0];
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private async Task RunPeriodicRefreshAsync(
        PeriodicTimer timer,
        CancellationToken cancellationToken)
    {
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            RequestRefresh();
        }
    }

    private void HandleRefreshRequested(object? sender, EventArgs eventArgs) => RequestRefresh();

    private void HandleRateLimitsChanged() => RequestRefresh();

    private void HandleInstanceMessage(string message)
    {
        if (Volatile.Read(ref _stopping) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (Volatile.Read(ref _stopping) != 0)
            {
                return;
            }

            if (message == "activate")
            {
                _window.RestoreAndActivate();
            }

            if (UsageRefreshPolicy.ShouldRefreshForInstanceMessage(message))
            {
                RequestRefresh();
            }
        });
    }

    private void RequestRefresh()
    {
        if (Volatile.Read(ref _stopping) != 0)
        {
            return;
        }

        _codexRefresher.Request();
        _claudeRefresher.Request();
        _antigravityRefresher.Request();
    }

    private async Task RefreshCodexAsync(CancellationToken cancellationToken)
    {
        if (!_codexActivation.TryGetToken(out var enabledToken))
        {
            return;
        }

        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, enabledToken);
        try
        {
            await EnterLoadingAsync();
            var state = await _appServerClient.ReadRateLimitsAsync(refreshCancellation.Token);
            var updatedAt = DateTimeOffset.Now;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_codexActivation.IsCurrent(enabledToken))
                {
                    return;
                }

                _hasCodexUsageState = true;
                _window.UpdateUsage(UsageProvider.Codex, state);
                _status.Update("Codex", updatedAt, null);
                PublishStatus();
                UsageUpdated?.Invoke(UsageProvider.Codex, state, updatedAt);
            });
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CompanionLog.Shared.Write("refresh", exception);
            var failedAt = DateTimeOffset.Now;
            var error = Program.FriendlyError(exception);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_codexActivation.IsCurrent(enabledToken))
                {
                    return;
                }

                if (UsageRefreshPolicy.ShouldShowUnavailable(_hasCodexUsageState))
                {
                    _window.UpdateUsage(UsageProvider.Codex, null);
                }

                _status.Update("Codex", null, error);
                PublishStatus();
                UsageUpdateFailed?.Invoke(UsageProvider.Codex, error, failedAt);
            });
        }
        finally
        {
            await ExitLoadingAsync();
        }
    }

    private async Task RefreshClaudeAsync(CancellationToken cancellationToken)
    {
        if (!_claudeActivation.TryGetToken(out var enabledToken))
        {
            return;
        }

        if (_lastClaudeActivationToken != enabledToken)
        {
            _lastClaudeActivationToken = enabledToken;
            _lastClaudeFetchAt = null;
        }

        if (!ClaudeUsagePolicy.ShouldFetch(DateTimeOffset.UtcNow, _lastClaudeFetchAt))
        {
            return;
        }

        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, enabledToken);
        try
        {
            await EnterLoadingAsync();
            _lastClaudeFetchAt = DateTimeOffset.UtcNow;
            var state = await _claudeClient.ReadUsageAsync(refreshCancellation.Token);
            var updatedAt = DateTimeOffset.Now;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_claudeActivation.IsCurrent(enabledToken))
                {
                    return;
                }

                _hasClaudeUsageState = true;
                _window.UpdateUsage(UsageProvider.Claude, state);
                _status.Update("Claude", updatedAt, null);
                PublishStatus();
                UsageUpdated?.Invoke(UsageProvider.Claude, state, updatedAt);
            });
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            CompanionLog.Shared.Write("claude-refresh", exception);
            var failedAt = DateTimeOffset.Now;
            var error = Program.FriendlyError(exception);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_claudeActivation.IsCurrent(enabledToken))
                {
                    return;
                }

                if (UsageRefreshPolicy.ShouldShowUnavailable(_hasClaudeUsageState))
                {
                    _window.UpdateUsage(UsageProvider.Claude, null);
                }

                _status.Update("Claude", null, error);
                PublishStatus();
                UsageUpdateFailed?.Invoke(UsageProvider.Claude, error, failedAt);
            });
        }
        finally
        {
            await ExitLoadingAsync();
        }
    }

    private async Task EnterLoadingAsync()
    {
        if (Interlocked.Increment(ref _activeRefreshCount) == 1)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _window.SetLoading(Volatile.Read(ref _activeRefreshCount) > 0));
        }
    }

    private async Task RefreshAntigravityAsync(CancellationToken cancellationToken)
    {
        if (!_antigravityProvider.IsEnabled)
        {
            return;
        }

        try
        {
            await EnterLoadingAsync();
            await _antigravityProvider.RefreshAsync(cancellationToken);
        }
        finally
        {
            await ExitLoadingAsync();
        }
    }

    private async Task ExitLoadingAsync()
    {
        if (Interlocked.Decrement(ref _activeRefreshCount) == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _window.SetLoading(Volatile.Read(ref _activeRefreshCount) > 0));
        }
    }
}
