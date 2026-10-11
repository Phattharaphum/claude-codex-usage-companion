using System.ComponentModel;
using System.Diagnostics;
using CodexUsageCompanion.Diagnostics;

namespace CodexUsageCompanion.Platform;

public sealed class LinuxDesktopNotificationService
{
    /// <summary>
    /// True when the notify-send helper this service relies on is on PATH.
    /// Without it notifications silently go nowhere, so settings can say so.
    /// </summary>
    public static bool IsAvailable() => IsAvailable(
        Environment.GetEnvironmentVariable("PATH"),
        File.Exists);

    internal static bool IsAvailable(string? path, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var directory in path.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (fileExists(System.IO.Path.Combine(directory, "notify-send")))
            {
                return true;
            }
        }

        return false;
    }

    public void Show(string title, string body, bool critical)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--app-name=Claude Codex Usage Companion");
            startInfo.ArgumentList.Add("--icon=claude-codex-usage-companion");
            startInfo.ArgumentList.Add(
                critical ? "--urgency=critical" : "--urgency=normal");
            startInfo.ArgumentList.Add(title);
            startInfo.ArgumentList.Add(body);
            using var process = Process.Start(startInfo);
        }
        catch (Exception exception) when (
            exception is Win32Exception or
            InvalidOperationException or
            NotSupportedException)
        {
            CompanionLog.Shared.Write("notification", exception);
        }
    }
}
