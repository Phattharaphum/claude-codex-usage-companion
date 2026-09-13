using System.Text.Json;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class GnomeExtensionPackageTests
{
    private const string ExtensionId = "claude-codex-usage-companion@ychsieh95.github.io";

    [Fact]
    public void ExtensionUsesCurrentGnomeModuleApiAndEventDrivenRuntimeState()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "gnome", ExtensionId);
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "metadata.json")));
        var shellVersions = metadata.RootElement.GetProperty("shell-version")
            .EnumerateArray()
            .Select(version => version.GetString())
            .ToArray();
        var extension = File.ReadAllText(Path.Combine(root, "extension.js"));

        Assert.Equal(ExtensionId, metadata.RootElement.GetProperty("uuid").GetString());
        Assert.Contains("45", shellVersions);
        Assert.Contains("50", shellVersions);
        Assert.Contains("resource:///org/gnome/shell/extensions/extension.js", extension);
        Assert.Contains("monitor_directory", extension);
        Assert.Contains("load_contents_async", extension);
        Assert.Contains("_runCompanion('refresh')", extension, StringComparison.Ordinal);
        Assert.DoesNotContain("status --json", extension, StringComparison.Ordinal);
        Assert.DoesNotContain("AntigravityUsageClient", extension, StringComparison.Ordinal);
        Assert.DoesNotContain("setInterval", extension, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionKeepsFormatterPureAndIncludesTheUserInstallScript()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "gnome", ExtensionId);
        var formatter = File.ReadAllText(Path.Combine(root, "presentation.js"));
        var test = File.ReadAllText(Path.Combine(root, "presentation.test.js"));
        var installScript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "install-gnome-top-bar-extension.sh"));

        Assert.Contains("G ${formatPercent", formatter, StringComparison.Ordinal);
        Assert.Contains("C ${formatPercent", formatter, StringComparison.Ordinal);
        Assert.Contains("G 89% · C 100%", test, StringComparison.Ordinal);
        Assert.Contains("G 89% · C —", test, StringComparison.Ordinal);
        Assert.Contains("gnome-extensions enable", installScript, StringComparison.Ordinal);
        Assert.DoesNotContain("sudo", installScript, StringComparison.OrdinalIgnoreCase);
    }
}
