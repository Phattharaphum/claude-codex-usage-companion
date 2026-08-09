namespace CodexUsageCompanion.Lifecycle;

// A shutdown step that blocks the UI thread stalls the whole sequence, and the
// stalled process keeps owning the instance lock and socket, which silently
// blocks every later launch. The watchdog bounds that failure: the callback
// runs on the thread pool, so it fires even when the dispatcher is wedged.
public sealed class ShutdownWatchdog : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly Timer _timer;
    private int _fired;

    public ShutdownWatchdog(Action onTimeout, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(onTimeout);
        _timer = new Timer(
            _ =>
            {
                if (Interlocked.Exchange(ref _fired, 1) == 0)
                {
                    onTimeout();
                }
            },
            null,
            timeout ?? DefaultTimeout,
            Timeout.InfiniteTimeSpan);
    }

    public bool Fired => Volatile.Read(ref _fired) != 0;

    public void Dispose()
    {
        _timer.Dispose();
    }
}
