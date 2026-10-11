using System.Text;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Diagnostics;
using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageHistoryReaderCacheTests
{
    private const string Header =
        "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at,weekly_remaining_percent,weekly_reset_at,available_reset_credits,error,scope";

    [Fact]
    public void AppendedRowsAreAddedWithoutLosingEarlierOnes()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + Row(1, "codex", 90) + "\n" + Row(2, "codex", 80) + "\n");
            var reader = new UsageHistoryReader();
            Assert.Equal(2, reader.Read(path, UsageLogOptions.Csv).Entries.Count);

            File.AppendAllText(path, Row(3, "codex", 70) + "\n" + Row(4, "claude", 60) + "\n");
            var result = reader.Read(path, UsageLogOptions.Csv);

            Assert.Null(result.Error);
            Assert.Equal(4, result.Entries.Count);
            Assert.Equal(["claude", "codex", "codex", "codex"], result.Entries.Select(entry => entry.Provider));
            Assert.Equal([60, 70, 80, 90], result.Entries.Select(entry => entry.FiveHourRemainingPercent!.Value));
        });
    }

    [Fact]
    public void AnUnchangedFileReturnsTheCachedResultWithoutReparsing()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + Row(1, "codex", 90) + "\n");
            var reader = new UsageHistoryReader();

            var first = reader.Read(path, UsageLogOptions.Csv);
            var second = new UsageHistoryReader().Read(path, UsageLogOptions.Csv);

            // The cache is shared across reader instances: several windows read the same log.
            Assert.Same(first.Entries, second.Entries);
        });
    }

    [Fact]
    public void OnlyTheAppendedBytesAreParsedWhileTheFileGrows()
    {
        WithFile(path =>
        {
            var rows = Enumerable.Range(1, 40).Select(minute => Row(minute, "codex", 90)).ToList();
            File.WriteAllText(path, Header + "\n" + string.Join("\n", rows) + "\n");
            var reader = new UsageHistoryReader();
            reader.Read(path, UsageLogOptions.Csv);

            // Alter one value in the middle without changing the file's length, then append.
            // A full reparse would notice; an incremental read keeps what it already parsed.
            var text = File.ReadAllText(path).Replace(",codex,success,90,", ",codex,success,91,");
            File.WriteAllText(path, text);
            File.AppendAllText(path, Row(41, "codex", 70) + "\n");
            var result = reader.Read(path, UsageLogOptions.Csv);

            Assert.Equal(41, result.Entries.Count);
            Assert.Equal(40, result.Entries.Count(entry => entry.FiveHourRemainingPercent == 90));
        });
    }

    [Fact]
    public void ARewrittenSmallerFileIsReadFromScratch()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + string.Join("\n", Enumerable.Range(1, 30).Select(m => Row(m, "codex", 90))) + "\n");
            var reader = new UsageHistoryReader();
            Assert.Equal(30, reader.Read(path, UsageLogOptions.Csv).Entries.Count);

            File.WriteAllText(path, Header + "\n" + Row(50, "claude", 12) + "\n");
            var result = reader.Read(path, UsageLogOptions.Csv);

            var entry = Assert.Single(result.Entries);
            Assert.Equal("claude", entry.Provider);
        });
    }

    [Fact]
    public void AFileThatWasPrunedAndThenGrewPastItsOldLengthIsNotMistakenForAnAppend()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + string.Join("\n", Enumerable.Range(1, 30).Select(m => Row(m, "codex", 90))) + "\n");
            var reader = new UsageHistoryReader();
            reader.Read(path, UsageLogOptions.Csv);

            // Retention drops the oldest rows, then plenty of new ones arrive.
            var kept = Enumerable.Range(20, 11).Select(m => Row(m, "codex", 90));
            var added = Enumerable.Range(100, 60).Select(m => Row(m, "claude", 33));
            File.WriteAllText(path, Header + "\n" + string.Join("\n", kept.Concat(added)) + "\n");
            var result = reader.Read(path, UsageLogOptions.Csv);

            Assert.Equal(71, result.Entries.Count);
            Assert.Equal(60, result.Entries.Count(entry => entry.Provider == "claude"));
        });
    }

    [Fact]
    public void RowsWrittenByTheUsageLogAreVisibleToALaterRead()
    {
        WithFile(path =>
        {
            var log = new UsageUpdateLog();
            var state = new RateLimitState(new RateLimitWindowState(71, 300, 1785435000), new RateLimitWindowState(58, 10080, 1786039800), null);
            var reader = new UsageHistoryReader();
            var at = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

            log.WriteSuccess(path, UsageLogOptions.Csv, UsageProvider.Codex, state, at);
            Assert.Single(reader.Read(path, UsageLogOptions.Csv).Entries);
            log.WriteSuccess(path, UsageLogOptions.Csv, UsageProvider.Codex, state, at.AddMinutes(1));
            log.WriteFailure(path, UsageLogOptions.Csv, UsageProvider.Claude, "offline", at.AddMinutes(2));

            var result = reader.Read(path, UsageLogOptions.Csv);
            Assert.Equal(3, result.Entries.Count);
            Assert.Equal("claude", result.Entries[0].Provider);
        });
    }

    [Fact]
    public void ADeletedFileReadsAsEmpty()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + Row(1, "codex", 90) + "\n");
            var reader = new UsageHistoryReader();
            Assert.Single(reader.Read(path, UsageLogOptions.Csv).Entries);

            File.Delete(path);

            var result = reader.Read(path, UsageLogOptions.Csv);
            Assert.Empty(result.Entries);
            Assert.Null(result.Error);
        });
    }

    [Fact]
    public void AnUnrecognizedHeaderIsReportedAndNotCached()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, "not,a,usage,log\n1,2,3,4\n");
            var reader = new UsageHistoryReader();

            var result = reader.Read(path, UsageLogOptions.Csv);

            Assert.Contains("recognized CSV header", result.Error);
            File.WriteAllText(path, Header + "\n" + Row(1, "codex", 90) + "\n");
            Assert.Single(reader.Read(path, UsageLogOptions.Csv).Entries);
        });
    }

    [Fact]
    public void AnUnterminatedQuotedFieldIsReported()
    {
        WithFile(path =>
        {
            File.WriteAllText(path, Header + "\n" + Row(1, "codex", 90) + "\n2026-10-01T09:02:00+00:00,claude,error,,,,,,\"never closed\n");

            var result = new UsageHistoryReader().Read(path, UsageLogOptions.Csv);

            Assert.Contains("Unable to read usage history", result.Error);
        });
    }

    [Fact]
    public void ABomAtTheStartOfTheFileIsIgnored()
    {
        WithFile(path =>
        {
            File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(Header + "\n" + Row(1, "codex", 90) + "\n")).ToArray());

            var result = new UsageHistoryReader().Read(path, UsageLogOptions.Csv);

            Assert.Null(result.Error);
            Assert.Single(result.Entries);
        });
    }

    private static string Row(int minute, string provider, int fiveHour) =>
        $"{new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero).AddMinutes(minute):O},{provider},success,{fiveHour},2026-10-01T14:00:00.0000000+00:00,50,2026-10-08T09:00:00.0000000+00:00,,,provider";

    private static void WithFile(Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            test(Path.Combine(directory, "usage-history.csv"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
