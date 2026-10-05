using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.Platform;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class DetachedLauncherTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("/usr/bin/setsid")]
    public void FrameworkDependentLaunchPreservesManagedEntryPoint(string? setsid)
    {
        var startInfo = DetachedLauncher.CreateStartInfo(
            "/usr/bin/dotnet",
            "/opt/Usage Companion/CodexUsageCompanion.dll",
            setsid);

        Assert.Equal(setsid ?? "/usr/bin/dotnet", startInfo.FileName);
        var expected = new List<string>();
        if (setsid is not null)
        {
            expected.AddRange(["-f", "/usr/bin/dotnet"]);
        }

        expected.AddRange(["/opt/Usage Companion/CodexUsageCompanion.dll", "--background"]);
        Assert.Equal(expected, startInfo.ArgumentList);
        Assert.Equal(Environment.CurrentDirectory, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        if (OperatingSystem.IsLinux())
        {
            Assert.False(startInfo.Environment.ContainsKey(LinuxSessionManagement.SessionManagerVariable));
        }
    }

    [Fact]
    public void SelfContainedLaunchDoesNotRepeatExecutable()
    {
        var startInfo = DetachedLauncher.CreateStartInfo(
            "/opt/Usage Companion/CodexUsageCompanion",
            "/opt/Usage Companion/CodexUsageCompanion",
            "/usr/bin/setsid");

        Assert.Equal(
            ["-f", "/opt/Usage Companion/CodexUsageCompanion", "--background"],
            startInfo.ArgumentList);
    }
}
