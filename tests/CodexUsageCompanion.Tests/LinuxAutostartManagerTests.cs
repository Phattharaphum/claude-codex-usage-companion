using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodexUsageCompanion.Platform;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class LinuxAutostartManagerTests
{
    [Fact]
    public void EnableWritesUserDesktopEntryAndDisableRemovesIt()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        var path = Path.Combine(directory, LinuxAutostartManager.DesktopFileName);
        try
        {
            LinuxAutostartManager.SetEnabled(
                enabled: true,
                "/opt/codex usage/claude-codex-usage-companion",
                path);

            var content = File.ReadAllText(path);
            Assert.Contains("[Desktop Entry]", content);
            Assert.Contains(
                "Exec=/usr/bin/env -u SESSION_MANAGER " +
                "AVALONIA_X11_USE_SESSION_MANAGEMENT=0 " +
                "\"/opt/codex usage/claude-codex-usage-companion\" --background",
                content);
            Assert.Contains("X-GNOME-Autostart-enabled=true", content);

            LinuxAutostartManager.SetEnabled(
                enabled: false,
                "/opt/codex usage/claude-codex-usage-companion",
                path);

            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void EnableRejectsRelativeExecutablePath()
    {
        Assert.Throws<ArgumentException>(() =>
            LinuxAutostartManager.SetEnabled(
                enabled: true,
                "claude-codex-usage-companion",
                Path.Combine(Path.GetTempPath(), "unused.desktop")));
    }

    [Theory]
    [InlineData("/opt/line\nbreak/companion")]
    [InlineData("/opt/line\rbreak/companion")]
    public void EnableRejectsDesktopEntryLineBreaks(string executable)
    {
        Assert.Throws<ArgumentException>(() => LinuxAutostartManager.SetEnabled(
            true, executable, Path.Combine(Path.GetTempPath(), "unused.desktop")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DesktopParserPreservesSpecialPathCharactersAndManagedEntryPoint(bool frameworkDependent)
    {
        // This integration check uses the installed GNOME desktop parser when
        // available; the application itself does not require GJS or Python.
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/gjs") || !File.Exists("/usr/bin/python3"))
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), $"companion-autostart-{Guid.NewGuid():N}");
        var installDirectory = Path.Combine(directory, "space \\\"quote\\\" dollar$HOME back`tick 100% literal\\slash");
        Directory.CreateDirectory(installDirectory);
        var executable = Path.Combine(installDirectory, "host");
        var managedEntryPoint = Path.Combine(installDirectory, "App.dll");
        var desktopFile = Path.Combine(directory, "autostart.desktop");
        var outputFile = Path.Combine(directory, "argv.json");
        try
        {
            File.WriteAllText(executable,
                "#!/usr/bin/python3\nimport json, os, sys\n" +
                "with open(os.environ['COMPANION_TEST_OUTPUT'], 'w') as output:\n" +
                "    json.dump({'argv': sys.argv, 'sessionManager': os.environ.get('SESSION_MANAGER'), " +
                "'avalonia': os.environ.get('AVALONIA_X11_USE_SESSION_MANAGEMENT')}, output)\n",
                new UTF8Encoding(false));
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            LinuxAutostartManager.SetEnabled(
                true, executable, desktopFile,
                commandLineEntryPoint: frameworkDependent
                    ? Path.GetRelativePath(Environment.CurrentDirectory, managedEntryPoint)
                    : executable);

            var startInfo = new ProcessStartInfo("/usr/bin/gjs")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(
                "const {GioUnix} = imports.gi; const app = GioUnix.DesktopAppInfo.new_from_filename(ARGV[0]); " +
                "if (!app || !app.launch([], null)) throw new Error('Desktop launch failed');");
            startInfo.ArgumentList.Add(desktopFile);
            startInfo.Environment["COMPANION_TEST_OUTPUT"] = outputFile;
            startInfo.Environment["SESSION_MANAGER"] = "test-session-must-be-removed";
            using var parser = Process.Start(startInfo)!;
            await parser.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(parser.ExitCode == 0, await parser.StandardError.ReadToEndAsync());

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!File.Exists(outputFile))
            {
                await Task.Delay(20, timeout.Token);
            }

            using var result = JsonDocument.Parse(File.ReadAllText(outputFile));
            var expectedArguments = frameworkDependent
                ? new[] { executable, managedEntryPoint, "--background" }
                : new[] { executable, "--background" };
            Assert.Equal(expectedArguments, result.RootElement.GetProperty("argv").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(JsonValueKind.Null, result.RootElement.GetProperty("sessionManager").ValueKind);
            Assert.Equal("0", result.RootElement.GetProperty("avalonia").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
