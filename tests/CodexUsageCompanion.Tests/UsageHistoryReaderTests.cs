using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageHistoryReaderTests
{
    [Fact]
    public void ReadsLatestFirstAndExcludesLegacyAntigravityModelRows()
    {
        WithTemporaryDirectory(directory =>
        {
            var path = Path.Combine(directory, "usage-history.csv");
            File.WriteAllText(path, string.Join('\n',
            [
                "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at,weekly_remaining_percent,weekly_reset_at,available_reset_credits,error,scope",
                "2026-09-14T09:00:00.0000000+00:00,claude,success,61,2026-09-14T12:00:00.0000000+00:00,55,2026-09-20T12:00:00.0000000+00:00,,,provider",
                "2026-09-14T10:00:00.0000000+00:00,antigravity,success,,,,,,,model",
                "2026-09-14T11:00:00.0000000+00:00,Antigravity-Gemini,success,88,2026-09-14T14:00:00.0000000+00:00,83,2026-09-18T12:00:00.0000000+00:00,,,quota_pool",
                "2026-09-14T12:00:00.0000000+00:00,codex,error,,,,,,,provider"
            ]));
            var reader = new UsageHistoryReader();

            var result = reader.Read(path, UsageLogOptions.Csv);

            Assert.Null(result.Error);
            Assert.Equal(
                ["codex", "Antigravity-Gemini", "claude"],
                result.Entries.Select(entry => entry.Provider));
            var gemini = result.Entries[1];
            Assert.Equal(88, gemini.FiveHourRemainingPercent);
            Assert.Equal(83, gemini.WeeklyRemainingPercent);
            Assert.Equal("quota_pool", gemini.Scope);
        });
    }

    [Fact]
    public void ExplainsThatTheDashboardRequiresCsvLogging()
    {
        var result = new UsageHistoryReader().Read("usage-history.txt", UsageLogOptions.Text);

        Assert.Empty(result.Entries);
        Assert.Contains("CSV", result.Error);
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
