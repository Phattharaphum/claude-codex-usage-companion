using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Lifecycle;

internal interface IAntigravityUsageReader : IAsyncDisposable
{
    Task<AntigravityUsageState> ReadUsageAsync(CancellationToken cancellationToken);
}

internal sealed class AntigravityRuntimeProvider : IAsyncDisposable
{
    private readonly Func<IAntigravityUsageReader> _createReader;
    private readonly Func<Exception, string> _friendlyError;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private IAntigravityUsageReader? _reader;
    private CancellationTokenSource? _enabledCancellation;
    private AntigravityUsageState? _state;
    private bool _enabled;
    private bool _disposed;

    internal AntigravityRuntimeProvider(
        bool enabled,
        Func<IAntigravityUsageReader> createReader,
        Func<Exception, string> friendlyError)
    {
        _createReader = createReader;
        _friendlyError = friendlyError;
        _enabled = enabled;
        if (enabled)
        {
            _enabledCancellation = new CancellationTokenSource();
        }
    }

    internal event Action<AntigravityUsageState?, string?, DateTimeOffset>? UsageChanged;

    internal bool SetEnabled(bool enabled)
    {
        CancellationTokenSource? cancellation = null;
        var changed = false;
        lock (_sync)
        {
            if (_disposed || _enabled == enabled)
            {
                return false;
            }

            _enabled = enabled;
            changed = true;
            if (enabled)
            {
                _enabledCancellation = new CancellationTokenSource();
            }
            else
            {
                cancellation = _enabledCancellation;
                _enabledCancellation = null;
                _state = null;
            }
        }

        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        if (!enabled)
        {
            UsageChanged?.Invoke(null, null, DateTimeOffset.Now);
        }

        return changed;
    }

    internal async Task RefreshAsync(CancellationToken runtimeCancellationToken)
    {
        IAntigravityUsageReader? reader;
        CancellationToken enabledCancellationToken;
        lock (_sync)
        {
            if (_disposed || !_enabled || _enabledCancellation is null)
            {
                return;
            }

            enabledCancellationToken = _enabledCancellation.Token;
        }

        using var refreshCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            runtimeCancellationToken,
            enabledCancellationToken);
        try
        {
            if (!await _refreshGate.WaitAsync(0, refreshCancellation.Token))
            {
                return;
            }
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                if (_disposed || !_enabled || _enabledCancellation is null ||
                    _enabledCancellation.Token != enabledCancellationToken)
                {
                    return;
                }

                _reader ??= _createReader();
                reader = _reader;
            }

            var state = await reader.ReadUsageAsync(refreshCancellation.Token);
            if (!IsCurrent(enabledCancellationToken))
            {
                return;
            }

            lock (_sync)
            {
                _state = state;
            }

            UsageChanged?.Invoke(state, null, DateTimeOffset.Now);
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!IsCurrent(enabledCancellationToken))
            {
                return;
            }

            AntigravityUsageState? previous;
            lock (_sync)
            {
                previous = _state;
            }

            UsageChanged?.Invoke(previous, _friendlyError(exception), DateTimeOffset.Now);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        IAntigravityUsageReader? reader;
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _enabled = false;
            reader = _reader;
            _reader = null;
            cancellation = _enabledCancellation;
            _enabledCancellation = null;
            _state = null;
        }

        cancellation?.Cancel();
        await _refreshGate.WaitAsync();
        try
        {
            if (reader is not null)
            {
                await reader.DisposeAsync();
            }
        }
        finally
        {
            _refreshGate.Release();
            _refreshGate.Dispose();
            cancellation?.Dispose();
        }
    }

    private bool IsCurrent(CancellationToken enabledCancellationToken)
    {
        lock (_sync)
        {
            return !_disposed && _enabled && _enabledCancellation is not null &&
                   _enabledCancellation.Token == enabledCancellationToken;
        }
    }
}
