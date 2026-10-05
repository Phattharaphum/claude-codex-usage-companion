using CodexUsageCompanion.Diagnostics;

namespace CodexUsageCompanion.Lifecycle;

public sealed class RefreshCoordinator : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _worker;
    private int _pending;
    private int _disposed;

    public RefreshCoordinator(Func<CancellationToken, Task> refresh)
    {
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        _worker = Task.Run(RunAsync);
    }

    public void Request()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _pending, 1) != 0)
        {
            return;
        }

        try
        {
            _signal.Release();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
        }

        _signal.Dispose();
        _cancellation.Dispose();
    }

    private async Task RunAsync()
    {
        while (true)
        {
            await _signal.WaitAsync(_cancellation.Token);
            Interlocked.Exchange(ref _pending, 0);
            try
            {
                await _refresh(_cancellation.Token);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed attempt must not permanently stop future manual or timer refreshes.
                CompanionLog.Shared.Write("refresh-worker", exception);
            }
        }
    }
}
