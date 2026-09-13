using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class AntigravityRuntimeProviderTests
{
    private const string Secret = "test-csrf-secret-do-not-log";

    [Fact]
    public async Task DisabledProviderDoesNotCreateOrReadAClient()
    {
        var created = 0;
        var reader = new StubReader(_ => Task.FromResult(State()));
        await using var provider = Provider(false, () =>
        {
            created++;
            return reader;
        });

        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, created);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public async Task EnabledProviderPerformsInitialRefreshAndPublishesSeparateModels()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [
                new AntigravityModelQuotaState("gemini", "Gemini", 54, null),
                new AntigravityModelQuotaState("claude", "Claude", 100, null)
            ]);
        var reader = new StubReader(_ => Task.FromResult(state));
        await using var provider = Provider(true, () => reader);
        var updates = new List<(AntigravityUsageState? State, string? Error)>();
        provider.UsageChanged += (updated, error, _) => updates.Add((updated, error));

        await provider.RefreshAsync(CancellationToken.None);

        var update = Assert.Single(updates);
        Assert.Same(state, update.State);
        Assert.Null(update.Error);
        Assert.Equal(2, update.State!.Models.Count);
        Assert.Equal("gemini", update.State.Models[0].Id);
        Assert.Equal("claude", update.State.Models[1].Id);
        Assert.Null(update.State.Account);
        Assert.Null(update.State.Plan);
        Assert.Null(update.State.Models[0].ResetAt);
    }

    [Fact]
    public async Task SubsequentRefreshReplacesState()
    {
        var results = new Queue<AntigravityUsageState>([State("first"), State("second")]);
        var reader = new StubReader(_ => Task.FromResult(results.Dequeue()));
        await using var provider = Provider(true, () => reader);
        var updates = new List<(AntigravityUsageState? State, string? Error)>();
        provider.UsageChanged += (state, error, _) => updates.Add((state, error));

        await provider.RefreshAsync(CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal("first", updates[0].State!.Models[0].Id);
        Assert.Equal("second", updates[1].State!.Models[0].Id);
        Assert.All(updates, update => Assert.Null(update.Error));
    }

    [Fact]
    public async Task FailurePreservesPriorStateAndLaterSuccessRecoversWithoutSecrets()
    {
        var results = new Queue<Func<CancellationToken, Task<AntigravityUsageState>>>([
            _ => Task.FromResult(State("first")),
            _ => throw new InvalidOperationException($"failure {Secret}"),
            _ => Task.FromResult(State("recovered"))
        ]);
        var reader = new StubReader(token => results.Dequeue()(token));
        await using var provider = Provider(true, () => reader);
        var updates = new List<(AntigravityUsageState? State, string? Error)>();
        provider.UsageChanged += (state, error, _) => updates.Add((state, error));

        await provider.RefreshAsync(CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);
        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal("first", updates[0].State!.Models[0].Id);
        Assert.Equal("first", updates[1].State!.Models[0].Id);
        Assert.Equal("Antigravity local usage service was unavailable.", updates[1].Error);
        Assert.DoesNotContain(Secret, updates[1].Error);
        Assert.Equal("recovered", updates[2].State!.Models[0].Id);
        Assert.Null(updates[2].Error);
    }

    [Fact]
    public async Task RefreshesDoNotOverlap()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new StubReader(async cancellationToken =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return State();
        });
        await using var provider = Provider(true, () => reader);

        var first = provider.RefreshAsync(CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await provider.RefreshAsync(CancellationToken.None);
        release.SetResult();
        await first;

        Assert.Equal(1, reader.ReadCount);
        Assert.Equal(1, reader.MaximumConcurrentReads);
    }

    [Fact]
    public async Task CancellationStopsAnActiveRefreshWithoutPublishingFailure()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reader = new StubReader(async cancellationToken =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return State();
        });
        await using var provider = Provider(true, () => reader);
        var updates = 0;
        provider.UsageChanged += (_, _, _) => updates++;
        using var cancellation = new CancellationTokenSource();

        var refresh = provider.RefreshAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();
        await refresh;

        Assert.Equal(0, updates);
    }

    [Fact]
    public async Task DisableStopsFutureReadsClearsStateAndEnableReactivatesProvider()
    {
        var reader = new StubReader(_ => Task.FromResult(State()));
        await using var provider = Provider(false, () => reader);
        var updates = new List<(AntigravityUsageState? State, string? Error)>();
        provider.UsageChanged += (state, error, _) => updates.Add((state, error));

        Assert.True(provider.SetEnabled(true));
        await provider.RefreshAsync(CancellationToken.None);
        Assert.True(provider.SetEnabled(false));
        await provider.RefreshAsync(CancellationToken.None);
        Assert.True(provider.SetEnabled(true));
        await provider.RefreshAsync(CancellationToken.None);

        Assert.Equal(2, reader.ReadCount);
        Assert.Null(updates[1].State);
        Assert.Null(updates[1].Error);
    }

    [Fact]
    public async Task DisposalCancelsAndDisposesTheReader()
    {
        var reader = new StubReader(_ => Task.FromResult(State()));
        var provider = Provider(true, () => reader);

        await provider.RefreshAsync(CancellationToken.None);
        await provider.DisposeAsync();

        Assert.True(reader.Disposed);
    }

    private static AntigravityRuntimeProvider Provider(
        bool enabled,
        Func<IAntigravityUsageReader> createReader) =>
        new(enabled, createReader, Program.FriendlyAntigravityError);

    private static AntigravityUsageState State(string id = "model") =>
        new(
            "sanitized@example.invalid",
            "Pro",
            [new AntigravityModelQuotaState(id, "Gemini", 54, null)]);

    private sealed class StubReader(Func<CancellationToken, Task<AntigravityUsageState>> read)
        : IAntigravityUsageReader
    {
        private int _concurrentReads;

        internal int ReadCount { get; private set; }
        internal int MaximumConcurrentReads { get; private set; }
        internal bool Disposed { get; private set; }

        public async Task<AntigravityUsageState> ReadUsageAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            var concurrent = Interlocked.Increment(ref _concurrentReads);
            MaximumConcurrentReads = Math.Max(MaximumConcurrentReads, concurrent);
            try
            {
                return await read(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentReads);
            }
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
