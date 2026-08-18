using System.IO;

namespace CodexUsageCompanion.RateLimits;

/// <summary>
/// Cross-process guard around a credentials refresh. The Claude CLI serialises its own
/// refreshes with a `proper-lockfile` lock beside the credentials file, so this claims the
/// same paths and honours the same staleness window to keep two writers from rotating the
/// refresh token at once.
/// </summary>
public sealed class ClaudeCredentialsRefreshLock : IDisposable
{
    public const string LockFileName = ".oauth_refresh.lock";

    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IReadOnlyList<string> _held;
    private bool _disposed;

    private ClaudeCredentialsRefreshLock(IReadOnlyList<string> held)
    {
        _held = held;
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for both the current and legacy CLI locks.
    /// Returns null while another process still holds either one.
    /// </summary>
    public static ClaudeCredentialsRefreshLock? TryAcquire(
        string credentialsPath,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        var directory = Path.GetDirectoryName(credentialsPath);
        var paths = new[]
        {
            string.IsNullOrEmpty(directory) ? LockFileName : Path.Combine(directory, LockFileName),
            credentialsPath + ".lock"
        };

        var deadline = DateTimeOffset.UtcNow + timeout;
        var held = new List<string>();
        foreach (var path in paths)
        {
            if (!TryAcquireOne(path, deadline, pollInterval ?? DefaultPollInterval))
            {
                Release(held);
                return null;
            }

            held.Add(path);
        }

        return new ClaudeCredentialsRefreshLock(held);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release(_held);
    }

    private static bool TryAcquireOne(string path, DateTimeOffset deadline, TimeSpan pollInterval)
    {
        while (true)
        {
            try
            {
                var parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                // FileMode.CreateNew is the only atomic create-or-fail this runtime offers.
                // The CLI's own lock is a directory, which surfaces here as denied access
                // rather than a collision; either way the lock counts as taken.
                using var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                return true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
            }

            if (IsStale(path))
            {
                TryRemove(path);
                continue;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(pollInterval);
        }
    }

    private static bool IsStale(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            return DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(path) > StaleAfter;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Release(IReadOnlyList<string> paths)
    {
        for (var index = paths.Count - 1; index >= 0; index--)
        {
            TryRemove(paths[index]);
        }
    }

    private static void TryRemove(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
