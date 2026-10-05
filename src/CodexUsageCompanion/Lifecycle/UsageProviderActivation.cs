namespace CodexUsageCompanion.Lifecycle;

/// <summary>
/// Tracks an enabled provider's lifetime so an in-flight response cannot outlive
/// the settings that requested it, including a quick disable/enable sequence.
/// </summary>
internal sealed class UsageProviderActivation : IDisposable
{
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    internal UsageProviderActivation(bool enabled)
    {
        if (enabled)
        {
            _cancellation = new CancellationTokenSource();
        }
    }

    internal bool SetEnabled(bool enabled)
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            if (_disposed || enabled == (_cancellation is not null))
            {
                return false;
            }

            previous = _cancellation;
            _cancellation = enabled ? new CancellationTokenSource() : null;
        }

        previous?.Cancel();
        previous?.Dispose();
        return true;
    }

    internal bool TryGetToken(out CancellationToken token)
    {
        lock (_sync)
        {
            token = _cancellation?.Token ?? default;
            return !_disposed && _cancellation is not null;
        }
    }

    internal bool IsCurrent(CancellationToken token)
    {
        lock (_sync)
        {
            return !_disposed && _cancellation is not null && _cancellation.Token == token;
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            previous = _cancellation;
            _cancellation = null;
        }

        previous?.Cancel();
        previous?.Dispose();
    }
}
