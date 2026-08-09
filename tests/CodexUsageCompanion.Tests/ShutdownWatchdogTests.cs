using CodexUsageCompanion.Lifecycle;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ShutdownWatchdogTests
{
    [Fact]
    public async Task WatchdogRunsTheCallbackOnceAfterTheTimeout()
    {
        var calls = 0;
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watchdog = new ShutdownWatchdog(
            () =>
            {
                Interlocked.Increment(ref calls);
                fired.TrySetResult();
            },
            TimeSpan.FromMilliseconds(50));

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(watchdog.Fired);
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public async Task DisposedWatchdogNeverRunsTheCallback()
    {
        var calls = 0;
        var watchdog = new ShutdownWatchdog(
            () => Interlocked.Increment(ref calls),
            TimeSpan.FromMilliseconds(100));

        watchdog.Dispose();
        await Task.Delay(TimeSpan.FromMilliseconds(400));

        Assert.False(watchdog.Fired);
        Assert.Equal(0, Volatile.Read(ref calls));
    }

    [Fact]
    public void WatchdogRequiresACallback()
    {
        Assert.Throws<ArgumentNullException>(() => new ShutdownWatchdog(null!));
    }
}
