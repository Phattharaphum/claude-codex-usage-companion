using System.Text;
using System.Text.Json;
using CodexUsageCompanion.Diagnostics;

namespace CodexUsageCompanion.Platform;

/// <summary>
/// Publishes the optional GNOME Shell extension's presentation-only state.
/// It neither reads usage nor starts background work.
/// </summary>
public sealed class LinuxGnomeTopBarBridge
{
    private const string DirectoryName = "claude-codex-usage-companion";
    private const string FileName = "gnome-top-bar.json";
    private const UnixFileMode OwnerDirectory =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string? _statePath;

    public LinuxGnomeTopBarBridge(string? statePath = null)
    {
        _statePath = statePath ?? GetDefaultStatePath();
    }

    public bool IsAvailable => _statePath is not null;

    public void Publish(GnomeTopBarState state)
    {
        if (_statePath is null)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_statePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var temporaryPath = Path.Combine(
            directory,
            $".{FileName}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            SetDirectoryPermissions(directory);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                SetFilePermissions(temporaryPath);
                var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            PlatformNotSupportedException or
            NotSupportedException)
        {
            TryDelete(temporaryPath);
            CompanionLog.Shared.Write("gnome-top-bar", exception);
        }
    }

    public void Clear()
    {
        if (_statePath is null)
        {
            return;
        }

        TryDelete(_statePath);
    }

    internal static string? GetDefaultStatePath()
    {
        if (!OperatingSystem.IsLinux() || !IsGnomeSession())
        {
            return null;
        }

        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        return string.IsNullOrWhiteSpace(runtimeDirectory) ||
               !Path.IsPathFullyQualified(runtimeDirectory)
            ? null
            : Path.Combine(runtimeDirectory, DirectoryName, FileName);
    }

    private static bool IsGnomeSession()
    {
        return new[]
            {
                Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
                Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP")
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(':', StringSplitOptions.RemoveEmptyEntries))
            .Any(value => value.StartsWith("GNOME", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetDirectoryPermissions(string directory)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(directory, OwnerDirectory);
        }
    }

    private static void SetFilePermissions(string path)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, OwnerFile);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException)
        {
            CompanionLog.Shared.Write("gnome-top-bar", exception);
        }
    }
}
