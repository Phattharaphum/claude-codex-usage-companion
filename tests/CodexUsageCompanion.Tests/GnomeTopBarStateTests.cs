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
        var geminiFiveHourReset = updatedAt.AddHours(2);
        var geminiWeeklyReset = updatedAt.AddDays(5);
        var claudeFiveHourReset = updatedAt.AddHours(3);
        var claudeWeeklyReset = updatedAt.AddDays(4);
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
                Pool(
                    "claude-and-gpt-models",
                    "Claude and GPT models",
                    100,
                    80,
                    claudeFiveHourReset,
                    claudeWeeklyReset),
                Pool(
                    "gemini-models",
                    "Gemini Models",
                    89,
                    88,
                    geminiFiveHourReset,
                    geminiWeeklyReset)
            ]);

        var presentation = GnomeTopBarStateBuilder.Build(true, state, updatedAt);

        Assert.True(presentation.HasAntigravity);
        Assert.Equal(89, presentation.GeminiFiveHourRemaining);
        Assert.Equal(88, presentation.GeminiWeeklyRemaining);
        Assert.Equal(100, presentation.ClaudeGptFiveHourRemaining);
        Assert.Equal(80, presentation.ClaudeGptWeeklyRemaining);
        Assert.Equal(89, presentation.AntigravityRemaining);
        Assert.Equal(80, presentation.AntigravityWeeklyRemaining);
        Assert.Equal(
            geminiFiveHourReset.ToUnixTimeMilliseconds(),
            presentation.AntigravityFiveHourResetUnixMilliseconds);
        Assert.Equal(
            claudeWeeklyReset.ToUnixTimeMilliseconds(),
            presentation.AntigravityWeeklyResetUnixMilliseconds);
        Assert.Equal(
            geminiFiveHourReset.ToUnixTimeMilliseconds(),
            presentation.GeminiFiveHourResetUnixMilliseconds);
        Assert.Equal(
            geminiWeeklyReset.ToUnixTimeMilliseconds(),
            presentation.GeminiWeeklyResetUnixMilliseconds);
        Assert.Equal(
            claudeFiveHourReset.ToUnixTimeMilliseconds(),
            presentation.ClaudeGptFiveHourResetUnixMilliseconds);
        Assert.Equal(
            claudeWeeklyReset.ToUnixTimeMilliseconds(),
            presentation.ClaudeGptWeeklyResetUnixMilliseconds);
        Assert.Equal(updatedAt.ToUnixTimeMilliseconds(), presentation.LastUpdatedUnixMilliseconds);
        Assert.Equal(GnomeTopBarState.CurrentSchemaVersion, presentation.SchemaVersion);
    }

    [Fact]
    public void BuildsProviderWeeklyValuesResetTimesAndLatestUpdate()
    {
        var claudeUpdatedAt = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        var codexUpdatedAt = claudeUpdatedAt.AddMinutes(2);
        var claudeFiveHourReset = claudeUpdatedAt.AddHours(4).ToUnixTimeSeconds();
        var claudeWeeklyReset = claudeUpdatedAt.AddDays(4).ToUnixTimeSeconds();
        var codexFiveHourReset = codexUpdatedAt.AddHours(3).ToUnixTimeSeconds();
        var codexWeeklyReset = codexUpdatedAt.AddDays(6).ToUnixTimeSeconds();
        var claude = new RateLimitState(
            new RateLimitWindowState(73, 300, claudeFiveHourReset),
            new RateLimitWindowState(61, 10_080, claudeWeeklyReset),
            null);
        var codex = new RateLimitState(
            new RateLimitWindowState(82, 300, codexFiveHourReset),
            new RateLimitWindowState(45, 10_080, codexWeeklyReset),
            null);

        var presentation = GnomeTopBarStateBuilder.Build(
            false,
            null,
            null,
            claude,
            codex,
            claudeUpdatedAt,
            codexUpdatedAt);

        Assert.True(presentation.HasClaude);
        Assert.True(presentation.HasCodex);
        Assert.Equal(73, presentation.ClaudeFiveHourRemaining);
        Assert.Equal(61, presentation.ClaudeWeeklyRemaining);
        Assert.Equal(82, presentation.CodexFiveHourRemaining);
        Assert.Equal(45, presentation.CodexWeeklyRemaining);
        Assert.Equal(claudeFiveHourReset * 1_000, presentation.ClaudeFiveHourResetUnixMilliseconds);
        Assert.Equal(claudeWeeklyReset * 1_000, presentation.ClaudeWeeklyResetUnixMilliseconds);
        Assert.Equal(codexFiveHourReset * 1_000, presentation.CodexFiveHourResetUnixMilliseconds);
        Assert.Equal(codexWeeklyReset * 1_000, presentation.CodexWeeklyResetUnixMilliseconds);
        Assert.Equal(codexUpdatedAt.ToUnixTimeMilliseconds(), presentation.LastUpdatedUnixMilliseconds);
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
        Assert.Equal(89, presentation.AntigravityRemaining);
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
        Assert.Null(presentation.AntigravityRemaining);
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
            Assert.True(root.GetProperty("publishedAtUnixMilliseconds").GetInt64() > 0);
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
        int? weekly,
        DateTimeOffset? fiveHourReset = null,
        DateTimeOffset? weeklyReset = null) =>
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
                    fiveHourReset,
                    TimeSpan.FromHours(5)),
            weekly is null
                ? null
                : new AntigravityQuotaWindowState(
                    $"{id}-weekly",
                    "Weekly",
                    AntigravityQuotaCadence.Weekly,
                    weekly.Value,
                    weeklyReset,
                    TimeSpan.FromDays(7)),
            []);
}
