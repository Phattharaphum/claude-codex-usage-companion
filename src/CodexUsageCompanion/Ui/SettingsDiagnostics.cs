using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CodexUsageCompanion.Configuration;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// The summary "About" copies for bug reports. It is built only from version,
/// platform and non-secret settings: no tokens, account names or usage data.
/// </summary>
public static class SettingsDiagnostics
{
    public static string Build(
        CompanionSettings settings,
        string version,
        long? usageLogBytes,
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        var builder = new StringBuilder();
        builder.AppendLine($"{AppBranding.ProductName} {version}");
        builder.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        builder.AppendLine($".NET: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine(
            $"Session: {Describe(environment("XDG_SESSION_TYPE"))} / {Describe(environment("XDG_CURRENT_DESKTOP"))}");
        builder.AppendLine($"Language: {settings.Language}, theme: {settings.Theme}");
        builder.AppendLine(
            $"Providers: Claude {OnOff(settings.EnableClaudeUsage)}, " +
            $"Codex {OnOff(settings.EnableCodexUsage)}, " +
            $"Antigravity {OnOff(settings.EnableAntigravityUsage)}");
        builder.AppendLine($"Refresh interval: {settings.RefreshIntervalSeconds} s");
        builder.AppendLine(
            $"Tray: {OnOff(settings.EnableSystemTray)}, autostart: {OnOff(settings.StartOnBoot)}, " +
            $"always on top: {OnOff(settings.AlwaysOnTop)}");
        builder.AppendLine($"Position: {settings.Position}, opacity: {settings.Opacity * 100:0}%, margin: {settings.Margin}px");
        builder.AppendLine(
            $"Usage log: {OnOff(settings.EnableUsageLogging)}, format {settings.UsageLogFormat}, " +
            $"retention {(settings.UsageLogRetentionDays == 0 ? "forever" : $"{settings.UsageLogRetentionDays} days")}" +
            (usageLogBytes is { } bytes ? $", {bytes / 1_048_576d:0.0} MB" : string.Empty));
        builder.AppendLine($"Settings file: {CompanionSettingsStore.GetDefaultPath()}");
        return builder.ToString().TrimEnd();
    }

    private static string OnOff(bool value) => value ? "on" : "off";

    private static string Describe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
}
