using System.Text;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageLogPrunerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void CsvKeepsTheHeaderAndDropsOnlyOldRecords()
    {
        WithFile("usage.csv", path =>
        {
            File.WriteAllLines(path,
            [
                "updated_at,provider,status",
                $"{Stamp(-200)},codex,success",
                $"{Stamp(-120)},codex,success",
                $"{Stamp(-30)},codex,success",
                $"{Stamp(-1)},codex,success"
            ]);

            var result = UsageLogPruner.Prune(path, UsageLogOptions.Csv, 90, Now);

            Assert.Equal(2, result.RemovedRecords);
            Assert.True(result.BytesAfter < result.BytesBefore);
            var lines = File.ReadAllLines(path);
            Assert.Equal("updated_at,provider,status", lines[0]);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith(Stamp(-30), lines[1]);
        });
    }

    [Fact]
    public void AQuotedMultiLineErrorFollowsItsRecord()
    {
        WithFile("usage.csv", path =>
        {
            File.WriteAllLines(path,
            [
                "updated_at,provider,status,error",
                $"{Stamp(-200)},claude,error,\"first line",
                "second line of the old error\"",
                $"{Stamp(-2)},claude,error,\"kept first",
                "kept second\""
            ]);

            var result = UsageLogPruner.Prune(path, UsageLogOptions.Csv, 90, Now);

            Assert.Equal(1, result.RemovedRecords);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("second line of the old error", text);
            Assert.Contains("kept first", text);
            Assert.Contains("kept second", text);
        });
    }

    [Fact]
    public void NothingIsRewrittenWhenTheOldestRecordIsStillRecent()
    {
        WithFile("usage.csv", path =>
        {
            File.WriteAllLines(path, ["updated_at,provider,status", $"{Stamp(-10)},codex,success"]);
            var before = File.GetLastWriteTimeUtc(path);

            var result = UsageLogPruner.Prune(path, UsageLogOptions.Csv, 90, Now);

            Assert.Equal(0, result.RemovedRecords);
            Assert.Equal(before, File.GetLastWriteTimeUtc(path));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".*.prune"));
        });
    }

    [Fact]
    public void ZeroRetentionNeverPrunes()
    {
        WithFile("usage.csv", path =>
        {
            File.WriteAllLines(path, ["updated_at,provider,status", $"{Stamp(-2000)},codex,success"]);

            var result = UsageLogPruner.Prune(path, UsageLogOptions.Csv, 0, Now);

            Assert.Equal(0, result.RemovedRecords);
            Assert.Equal(2, File.ReadAllLines(path).Length);
        });
    }

    [Fact]
    public void JsonLinesAndTextLogsArePrunedByTheirOwnTimestampField()
    {
        WithFile("usage.jsonl", path =>
        {
            File.WriteAllLines(path,
            [
                $"{{\"updatedAt\":\"{Stamp(-300)}\",\"provider\":\"codex\"}}",
                $"{{\"updatedAt\":\"{Stamp(-3)}\",\"provider\":\"codex\"}}"
            ]);

            Assert.Equal(1, UsageLogPruner.Prune(path, UsageLogOptions.JsonLines, 90, Now).RemovedRecords);
            Assert.Single(File.ReadAllLines(path));
        });
        WithFile("usage.txt", path =>
        {
            File.WriteAllLines(path,
            [
                $"updated_at={Stamp(-300)} | provider=codex",
                $"updated_at={Stamp(-3)} | provider=codex"
            ]);

            Assert.Equal(1, UsageLogPruner.Prune(path, UsageLogOptions.Text, 90, Now).RemovedRecords);
            Assert.Single(File.ReadAllLines(path));
        });
    }

    [Fact]
    public void MissingAndEmptyFilesAreHarmless()
    {
        WithFile("usage.csv", path =>
        {
            Assert.Equal(0, UsageLogPruner.Prune(path, UsageLogOptions.Csv, 90, Now).RemovedRecords);
            File.WriteAllText(path, string.Empty);
            Assert.Equal(0, UsageLogPruner.Prune(path, UsageLogOptions.Csv, 90, Now).RemovedRecords);
        });
    }

    [Theory]
    [InlineData(-5, 90)]
    [InlineData(0, 0)]
    [InlineData(30, 30)]
    [InlineData(400, 400)]
    [InlineData(99999, 3650)]
    public void RetentionDaysNormalize(int value, int expected)
    {
        Assert.Equal(expected, UsageLogRetentionOptions.Normalize(value));
    }

    [Fact]
    public void RetentionDefaultsToNinetyDaysAndSurvivesNormalization()
    {
        Assert.Equal(90, new CompanionSettings().UsageLogRetentionDays);
        var normalized = CompanionSettingsStore.Normalize(new CompanionSettings { UsageLogRetentionDays = -1 });
        Assert.Equal(90, normalized.UsageLogRetentionDays);
    }

    private static string Stamp(int daysFromNow) =>
        Now.AddDays(daysFromNow).ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static void WithFile(string name, Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(Path.Combine(directory, name));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
