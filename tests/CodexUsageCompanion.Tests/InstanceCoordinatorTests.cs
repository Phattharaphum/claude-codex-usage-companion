using CodexUsageCompanion.Lifecycle;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class InstanceCoordinatorTests
{
    [Fact]
    public void TryAcquireResidentAllowsOnlyOneOwner()
    {
        var path = SocketPath();
        var first = new InstanceCoordinator(path);
        var second = new InstanceCoordinator(path);

        using var firstLease = first.TryAcquireResident();
        firstLease?.Start();
        using var secondLease = second.TryAcquireResident();

        Assert.NotNull(firstLease);
        Assert.Null(secondLease);
    }

    [Fact]
    public async Task SignalRefreshWakesResidentOwner()
    {
        var path = SocketPath();
        var owner = new InstanceCoordinator(path);
        var sender = new InstanceCoordinator(path);
        using var lease = owner.TryAcquireResident();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.NotNull(lease);
        lease.MessageReceived += message => received.TrySetResult(message);
        lease.Start();
        Assert.True(sender.SignalRefresh());
        Assert.Equal("refresh", await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task StalledClientDoesNotBlockOtherSignals()
    {
        var path = SocketPath();
        var owner = new InstanceCoordinator(path);
        var sender = new InstanceCoordinator(path);
        using var lease = owner.TryAcquireResident();
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.NotNull(lease);
        lease.MessageReceived += message => received.TrySetResult(message);
        lease.Start();
        using var stalledClient = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.Unix,
            System.Net.Sockets.SocketType.Stream,
            System.Net.Sockets.ProtocolType.Unspecified);
        await stalledClient.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(path));

        Assert.True(sender.SignalRefresh());
        Assert.Equal("refresh", await received.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void SignalRefreshReturnsFalseWithoutResidentOwner()
    {
        var sender = new InstanceCoordinator(SocketPath());

        Assert.False(sender.SignalRefresh());
    }

    [Fact]
    public void DisposingResidentLeaseStopsListenerAndReleasesEndpoint()
    {
        var path = SocketPath();
        var coordinator = new InstanceCoordinator(path);
        var lease = coordinator.TryAcquireResident();

        Assert.NotNull(lease);
        lease.Start();
        lease.Dispose();

        using var replacement = coordinator.TryAcquireResident();
        Assert.NotNull(replacement);
    }

    [Fact]
    public async Task DisposeAsyncCompletesWhenTheStartingContextNeverRunsCallbacks()
    {
        var coordinator = new InstanceCoordinator(SocketPath());
        var lease = coordinator.TryAcquireResident();

        Assert.NotNull(lease);
        // The GUI starts the lease on the dispatcher thread and disposes it from
        // the same thread during shutdown. The accept loop must never need that
        // thread again, or disposal waits on a callback that can never run.
        var disposal = RunWithDroppedCallbacks(() =>
        {
            lease.Start();
            return lease.DisposeAsync().AsTask();
        });

        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DisposeCompletesWhenTheStartingContextNeverRunsCallbacks()
    {
        var coordinator = new InstanceCoordinator(SocketPath());
        var lease = coordinator.TryAcquireResident();

        Assert.NotNull(lease);
        var disposal = Task.Run(() => RunWithDroppedCallbacks(() =>
        {
            lease.Start();
            lease.Dispose();
            return Task.CompletedTask;
        }));

        var completed = await Task.WhenAny(disposal, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(disposal, completed);
    }

    private static Task RunWithDroppedCallbacks(Func<Task> action)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DroppingSynchronizationContext());
        try
        {
            return action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private static string SocketPath() =>
        Path.Combine(Path.GetTempPath(), $"claude-codex-usage-test-{Guid.NewGuid():N}.sock");

    // Stands in for a dispatcher that is blocked and can no longer run queued
    // continuations.
    private sealed class DroppingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state)
        {
        }

        public override void Send(SendOrPostCallback callback, object? state)
        {
            throw new InvalidOperationException("The dispatcher is blocked.");
        }
    }
}
