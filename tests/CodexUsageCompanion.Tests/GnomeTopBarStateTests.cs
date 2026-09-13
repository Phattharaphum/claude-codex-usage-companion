using System.Text.Json;
using CodexUsageCompanion.Platform;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class GnomeTopBarStateTests
{
    [Fact]
    public void BuildsBothFiveHourValuesFromNamedAuthoritativePoolsNotListPosition()
    {
        var updatedAt = new DateTimeOffset(2026, 9, 13, 7, 44, 7, TimeSpan.Zero);
        var state = new AntigravityUsageState(
            "account@example.invalid",
            "Pro",
            Enumerable.Range(1, 14)
                .Select(index => new AntigravityModelQuotaState(
                    $"model-{index}",
                    $"Model {index}",
                    index,
                    null))
                .ToArray(),
            [
                Pool("claude-and-gpt-models", "Claude and GPT models", 100, 80),
                Pool("gemini-models", "Gemini Models", 89, 88)
            ]);

        var presentation = GnomeTopBarStateBuilder.Build(true, state, updatedAt);

        Assert.True(presentation.HasAntigravity);
        Assert.Equal(89, presentation.GeminiFiveHourRemaining);
        Assert.Equal(88, presentation.GeminiWeeklyRemaining);
        Assert.Equal(100, presentation.ClaudeGptFiveHourRemaining);
        Assert.Equal(80, presentation.ClaudeGptWeeklyRemaining);
        Assert.Equal(updatedAt.ToUnixTimeMilliseconds(), presentation.LastUpdatedUnixMilliseconds);
    }

    [Fact]
    public void MissingPoolOrWindowStaysUnavailableInsteadOfUsingModelObservations()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [new AntigravityModelQuotaState("gemini", "Gemini", 89, null)],
            [Pool("gemini-models", "Gemini Models", 89, null)]);

        var presentation = GnomeTopBarStateBuilder.Build(true, state, null);

        Assert.True(presentation.HasAntigravity);
        Assert.Equal(89, presentation.GeminiFiveHourRemaining);
        Assert.Null(presentation.GeminiWeeklyRemaining);
        Assert.Null(presentation.ClaudeGptFiveHourRemaining);
        Assert.Null(presentation.ClaudeGptWeeklyRemaining);
    }

    [Fact]
    public void DisabledAntigravityClearsEveryPublishedValue()
    {
        var presentation = GnomeTopBarStateBuilder.Build(
            false,
            new AntigravityUsageState(null, null, [], [Pool("gemini-models", "Gemini Models", 89, 88)]),
            DateTimeOffset.UtcNow);

        Assert.False(presentation.HasAntigravity);
        Assert.Null(presentation.GeminiFiveHourRemaining);
        Assert.Null(presentation.GeminiWeeklyRemaining);
        Assert.Null(presentation.ClaudeGptFiveHourRemaining);
        Assert.Null(presentation.ClaudeGptWeeklyRemaining);
        Assert.Null(presentation.LastUpdatedUnixMilliseconds);
    }

    [Fact]
    public void BridgeWritesOnlyTheSanitizedPresentationContractAtomically()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "gnome-top-bar.json");
        try
        {
            var bridge = new LinuxGnomeTopBarBridge(path);
            bridge.Publish(new GnomeTopBarState(1, true, 89, 88, 100, 80, 1234));

            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.True(root.GetProperty("hasAntigravity").GetBoolean());
            Assert.Equal(89, root.GetProperty("geminiFiveHourRemaining").GetInt32());
            Assert.Equal(80, root.GetProperty("claudeGptWeeklyRemaining").GetInt32());
            Assert.DoesNotContain("account", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("model", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("csrf", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("port", json, StringComparison.OrdinalIgnoreCase);

            if (OperatingSystem.IsLinux())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(path));
            }

            bridge.Clear();
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

    private static AntigravityQuotaPoolState Pool(
        string id,
        string name,
        int? fiveHour,
        int? weekly) =>
        new(
            id,
            name,
            fiveHour is null
                ? null
                : new AntigravityQuotaWindowState(
                    $"{id}-five-hour",
                    "Five Hour",
                    AntigravityQuotaCadence.FiveHour,
                    fiveHour.Value,
                    null,
                    TimeSpan.FromHours(5)),
            weekly is null
                ? null
                : new AntigravityQuotaWindowState(
                    $"{id}-weekly",
                    "Weekly",
                    AntigravityQuotaCadence.Weekly,
                    weekly.Value,
                    null,
                    TimeSpan.FromDays(7)),
            []);
}
