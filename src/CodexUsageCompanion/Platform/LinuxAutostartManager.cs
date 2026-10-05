using System.Text;

namespace CodexUsageCompanion.Platform;

public static class LinuxAutostartManager
{
    public const string DesktopFileName = "claude-codex-usage-companion.desktop";

    public static string DesktopFilePath =>
        Path.Combine(LinuxPaths.AutostartDirectory, DesktopFileName);

    public static void SetEnabled(
        bool enabled,
        string executablePath,
        string? desktopFilePath = null,
        string? commandLineEntryPoint = null)
    {
        var path = desktopFilePath ?? DesktopFilePath;
        if (!enabled)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath) ||
            !Path.IsPathFullyQualified(executablePath) ||
            executablePath.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException(
                "The executable path must be absolute and contain no line breaks.",
                nameof(executablePath));
        }

        var command = QuoteExecArgument(executablePath);
        if (!string.IsNullOrWhiteSpace(commandLineEntryPoint) &&
            string.Equals(Path.GetExtension(commandLineEntryPoint), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            // Login does not preserve the working directory of a dotnet launch.
            command += " " + QuoteExecArgument(Path.GetFullPath(commandLineEntryPoint));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content =
            $"""
            [Desktop Entry]
            Type=Application
            Name=Claude Codex Usage Companion
            Comment=View Claude and Codex rate-limit usage
            Exec=/usr/bin/env -u SESSION_MANAGER AVALONIA_X11_USE_SESSION_MANAGEMENT=0 {command} --background
            Icon=claude-codex-usage-companion
            Terminal=false
            X-GNOME-Autostart-enabled=true

            """;
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    internal static string QuoteExecArgument(string argument)
    {
        if (argument.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            throw new ArgumentException("Desktop arguments cannot contain line breaks or null characters.", nameof(argument));
        }

        var escaped = argument
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("$", "\\$", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal)
            // Desktop string escapes are decoded before Exec argument quotes.
            .Replace("\\", "\\\\", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
