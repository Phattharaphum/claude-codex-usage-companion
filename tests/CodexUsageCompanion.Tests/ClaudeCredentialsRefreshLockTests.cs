using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ClaudeCredentialsRefreshLockTests
{
    private static readonly TimeSpan NoWait = TimeSpan.Zero;

    [Fact]
    public void SecondAcquireFailsWhileFirstIsHeld()
    {
        var path = CreateCredentialsPath();
        using var first = ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait);

        Assert.NotNull(first);
        Assert.Null(ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait));
    }

    [Fact]
    public void ReleasingLetsTheNextWaiterIn()
    {
        var path = CreateCredentialsPath();
        using (var first = ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait))
        {
            Assert.NotNull(first);
        }

        using var second = ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait);
        Assert.NotNull(second);
    }

    [Fact]
    public void AcquireTakesOverALockAbandonedLongAgo()
    {
        var path = CreateCredentialsPath();
        var lockPath = Path.Combine(
            Path.GetDirectoryName(path)!,
            ClaudeCredentialsRefreshLock.LockFileName);
        File.WriteAllText(lockPath, string.Empty);
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddMinutes(-5));

        using var taken = ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait);

        Assert.NotNull(taken);
    }

    [Fact]
    public void AcquireIsBlockedByTheDirectoryLockTheClaudeCliUses()
    {
        var path = CreateCredentialsPath();
        Directory.CreateDirectory(Path.Combine(
            Path.GetDirectoryName(path)!,
            ClaudeCredentialsRefreshLock.LockFileName));

        Assert.Null(ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait));
    }

    [Fact]
    public void FailedAcquireLeavesNoPartiallyHeldLocks()
    {
        var path = CreateCredentialsPath();
        File.WriteAllText(path + ".lock", string.Empty);

        Assert.Null(ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait));

        // The first lock must have been handed back, or nothing could ever refresh again.
        File.Delete(path + ".lock");
        using var next = ClaudeCredentialsRefreshLock.TryAcquire(path, NoWait);
        Assert.NotNull(next);
    }

    private static string CreateCredentialsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"claude-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, ".credentials.json");
    }
}
