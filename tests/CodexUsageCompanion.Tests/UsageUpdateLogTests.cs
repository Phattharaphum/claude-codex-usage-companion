using System.Text.Json;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageUpdateLogTests
{
    private static readonly DateTimeOffset UpdatedAt =
        new(2026, 7, 31, 2, 10, 0, TimeSpan.FromHours(8));

    private static readonly RateLimitState State = new(
        new RateLimitWindowState(71, 300, 1785435000),
        new RateLimitWindowState(58, 10080, 1786039800),
        2);

    private static readonly AntigravityUsageState AntigravityState = new(
        "user@example.test",
        "Pro",
        [
            new AntigravityModelQuotaState(
                "gemini-2.5-pro",
                "Gemini 2.5 Pro",
                83,
                new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero)),
            new AntigravityModelQuotaState(
                "claude-sonnet-4",
                "Claude Sonnet 4",
                79,
                new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero))
        ],
        [
            new AntigravityQuotaPoolState(
                "gemini-models",
                "Gemini Models",
                new AntigravityQuotaWindowState(
                    "gemini-5hr",
                    "Five Hour Limit",
                    AntigravityQuotaCadence.FiveHour,
                    83,
                    new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
                    TimeSpan.FromHours(5)),
                new AntigravityQuotaWindowState(
                    "gemini-week",
                    "Weekly Limit",
                    AntigravityQuotaCadence.Weekly,
                    88,
                    new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero),
                    TimeSpan.FromDays(7)),
                ["gemini-2.5-pro"]),
            new AntigravityQuotaPoolState(
                "claude-and-gpt-models",
                "Claude and GPT models",
                new AntigravityQuotaWindowState(
                    "claude-gpt-5hr",
                    "Five Hour Limit",
                    AntigravityQuotaCadence.FiveHour,
                    79,
                    new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                    TimeSpan.FromHours(5)),
                new AntigravityQuotaWindowState(
                    "claude-gpt-week",
                    "Weekly Limit",
                    AntigravityQuotaCadence.Weekly,
                    81,
                    new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero),
                    TimeSpan.FromDays(7)),
                ["claude-sonnet-4"])
        ]);

    [Fact]
    public void WritesTextSuccessAndFailureEntries()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage.txt");
            var log = new UsageUpdateLog();

            log.WriteSuccess(path, UsageLogOptions.Text, UsageProvider.Codex, State, UpdatedAt);
            log.WriteFailure(path, UsageLogOptions.Text, UsageProvider.Claude, "Claude unavailable", UpdatedAt);

            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Contains("provider=codex", lines[0]);
            Assert.Contains("status=success", lines[0]);
            Assert.Contains("five_hour_remaining_percent=71", lines[0]);
            Assert.Contains("weekly_remaining_percent=58", lines[0]);
            Assert.Contains("available_reset_credits=2", lines[0]);
            Assert.Contains("provider=claude", lines[1]);
            Assert.Contains("status=error", lines[1]);
            Assert.Contains("error=Claude unavailable", lines[1]);
        });
    }

    [Fact]
    public void WritesOneCsvHeaderAndEscapesErrors()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage.csv");
            var log = new UsageUpdateLog();

            log.WriteSuccess(path, UsageLogOptions.Csv, UsageProvider.Codex, State, UpdatedAt);
            log.WriteFailure(path, UsageLogOptions.Csv, UsageProvider.Claude, "failed, retry", UpdatedAt);

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("updated_at,provider,status,", lines[0]);
            Assert.Contains(",codex,success,71,", lines[1]);
            Assert.Contains(",claude,error,", lines[2]);
            Assert.Contains("\"failed, retry\"", lines[2]);
        });
    }

    [Fact]
    public void WritesJsonLinesWithStableFieldNames()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage.jsonl");
            var log = new UsageUpdateLog();

            log.WriteSuccess(path, UsageLogOptions.JsonLines, UsageProvider.Claude, State, UpdatedAt);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            Assert.Equal("claude", root.GetProperty("provider").GetString());
            Assert.Equal("success", root.GetProperty("status").GetString());
            Assert.Equal(71, root.GetProperty("fiveHourRemainingPercent").GetInt32());
            Assert.Equal(58, root.GetProperty("weeklyRemainingPercent").GetInt32());
            Assert.Equal(2, root.GetProperty("availableResetCredits").GetInt32());
        });
    }

    [Fact]
    public void WritesOnlyTheTwoAuthoritativeAntigravityPoolHistories()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage.csv");
            var log = new UsageUpdateLog();

            log.WriteAntigravity(path, UsageLogOptions.Csv, AntigravityState, null, UpdatedAt);

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length); // Header, Gemini pool, Claude/GPT pool.
            Assert.Contains("scope,scope_id,scope_name,account,plan,model_ids,model_count", lines[0]);
            Assert.Contains(",Antigravity-Gemini,success,83,2026-08-01T09:00:00.0000000+00:00,88,", lines[1]);
            Assert.Contains(",Antigravity-ClaudeAndChatGPT,success,79,2026-08-01T10:00:00.0000000+00:00,81,", lines[2]);
            Assert.DoesNotContain("Gemini 2.5 Pro", string.Join('\n', lines));
            Assert.DoesNotContain("Claude Sonnet 4", string.Join('\n', lines));
            Assert.DoesNotContain("user@example.test", string.Join('\n', lines));
        });
    }

    [Fact]
    public void MigratesExistingCsvBeforeAppendingAntigravityHistory()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage.csv");
            File.WriteAllText(
                path,
                "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at," +
                "weekly_remaining_percent,weekly_reset_at,available_reset_credits,error\n" +
                "2026-07-31T02:10:00.0000000+08:00,codex,success,71,,58,,2,\n");
            var log = new UsageUpdateLog();

            log.WriteAntigravity(path, UsageLogOptions.Csv, AntigravityState, null, UpdatedAt);

            var lines = File.ReadAllLines(path);
            Assert.Contains("observed_remaining_percent", lines[0]);
            Assert.Equal(22, lines[1].Split(',').Length);
            Assert.StartsWith("2026-07-31T02:10:00.0000000+08:00,codex,success,71", lines[1]);
            Assert.Equal(4, lines.Length);
        });
    }

    private static void WithTemporaryDirectory(Action<string> test)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
