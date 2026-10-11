using CodexUsageCompanion.Diagnostics;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageRecordsTests
{
    private static readonly DateTimeOffset Origin = new(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ChangedOnlyCollapsesRepeatedIdenticalSamples()
    {
        var rows = NewestFirst(
            Sample("codex", 0, 90, 80),
            Sample("codex", 1, 90, 80),
            Sample("codex", 2, 90, 80),
            Sample("codex", 3, 88, 80),
            Sample("codex", 4, 88, 80));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ChangedOnly = true });

        // The first sample, then the one where the 5-hour value moved.
        Assert.Equal([Origin.AddMinutes(3), Origin], kept.Select(entry => entry.UpdatedAt));
    }

    [Fact]
    public void ChangedOnlyComparesEachProviderWithItsOwnPreviousSample()
    {
        var rows = NewestFirst(
            Sample("codex", 0, 90, 80),
            Sample("claude", 0, 50, 40),
            Sample("codex", 1, 90, 80),
            Sample("claude", 1, 50, 40));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ChangedOnly = true });

        Assert.Equal(2, kept.Count);
        Assert.Equal(["claude", "codex"], kept.Select(entry => entry.Provider).OrderBy(name => name));
    }

    [Fact]
    public void ChangedOnlyIgnoresAnIdleWindowRecomputingItsResetTime()
    {
        // Antigravity reports "now + 5h" for an unused window, so the reset time
        // drifts a minute at a time without anything having changed.
        var rows = NewestFirst(
            Sample("antigravity-gemini", 0, 100, 92, fiveHourReset: Origin.AddHours(5)),
            Sample("antigravity-gemini", 1, 100, 92, fiveHourReset: Origin.AddHours(5).AddMinutes(1)),
            Sample("antigravity-gemini", 2, 100, 92, fiveHourReset: Origin.AddHours(5).AddMinutes(2)));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ChangedOnly = true });

        Assert.Single(kept);
    }

    [Fact]
    public void ChangedOnlyKeepsARealResetTimeJump()
    {
        var rows = NewestFirst(
            Sample("codex", 0, 100, 80, fiveHourReset: Origin.AddHours(1)),
            Sample("codex", 1, 100, 80, fiveHourReset: Origin.AddHours(6)));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ChangedOnly = true });

        Assert.Equal(2, kept.Count);
    }

    [Fact]
    public void ChangedOnlyKeepsFailuresAndTheRecoveryAfterThem()
    {
        var rows = NewestFirst(
            Sample("codex", 0, 90, 80),
            Failure("codex", 1, "offline"),
            Sample("codex", 2, 90, 80),
            Sample("codex", 3, 90, 80));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ChangedOnly = true });

        Assert.Equal(
            [Origin.AddMinutes(2), Origin.AddMinutes(1), Origin],
            kept.Select(entry => entry.UpdatedAt));
    }

    [Fact]
    public void ErrorsOnlyKeepsFailedRefreshes()
    {
        var rows = NewestFirst(
            Sample("codex", 0, 90, 80),
            Failure("claude", 1, "session expired"),
            Sample("codex", 2, 88, 80));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { ErrorsOnly = true });

        var failure = Assert.Single(kept);
        Assert.Equal("session expired", failure.Error);
    }

    [Theory]
    [InlineData("claude", 1)]
    [InlineData("CLAUDE", 1)]
    [InlineData("expired", 1)]
    [InlineData("codex", 2)]
    [InlineData("claude expired", 1)]
    [InlineData("claude nonsense", 0)]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    public void SearchMatchesEveryTermAnywhereInTheRow(string search, int expected)
    {
        var rows = NewestFirst(
            Sample("codex", 0, 90, 80),
            Failure("claude", 1, "session expired"),
            Sample("codex", 2, 88, 80));

        var kept = UsageRecordFilter.Apply(rows, new UsageRecordFilterOptions { Search = search });

        Assert.Equal(expected, kept.Count);
    }

    [Fact]
    public void SearchCanUseTheDisplayNameOfAProvider()
    {
        var rows = NewestFirst(
            Sample("antigravity-gemini", 0, 90, 80),
            Sample("codex", 1, 90, 80));

        var kept = UsageRecordFilter.Apply(
            rows,
            new UsageRecordFilterOptions { Search = "gemini" },
            provider => provider == "antigravity-gemini" ? "Antigravity · Gemini" : provider);

        Assert.Equal("antigravity-gemini", Assert.Single(kept).Provider);
    }

    [Fact]
    public void SearchMatchesTheLocalDate()
    {
        var rows = NewestFirst(Sample("codex", 0, 90, 80));
        var local = Origin.ToLocalTime();

        var byDate = UsageRecordFilter.Apply(
            rows,
            new UsageRecordFilterOptions { Search = local.ToString("yyyy-MM-dd") });
        var other = UsageRecordFilter.Apply(
            rows,
            new UsageRecordFilterOptions { Search = "1999-01-01" });

        Assert.Single(byDate);
        Assert.Empty(other);
    }

    [Fact]
    public void FiltersCompose()
    {
        var rows = NewestFirst(
            Failure("claude", 0, "offline"),
            Failure("claude", 1, "offline"),
            Failure("codex", 2, "timeout"),
            Sample("claude", 3, 90, 80));

        var kept = UsageRecordFilter.Apply(
            rows,
            new UsageRecordFilterOptions { ChangedOnly = true, ErrorsOnly = true, Search = "claude" });

        Assert.Equal("offline", Assert.Single(kept).Error);
    }

    [Fact]
    public void ExportWritesAHeaderAndEscapesFields()
    {
        var csv = UsageRecordsExporter.ToCsv(
        [
            Sample("codex", 0, 90, 80),
            Failure("claude", 1, "failed, \"quoted\"\nsecond line")
        ]);

        var lines = csv.Split('\n');
        Assert.Equal(UsageRecordsExporter.Header, lines[0]);
        Assert.Contains(",codex,success,90,", lines[1]);
        Assert.Contains("\"failed, \"\"quoted\"\"\nsecond line\"", csv);
        Assert.EndsWith("\n", csv);
    }

    [Fact]
    public void ExportOfNothingIsJustTheHeader()
    {
        Assert.Equal(UsageRecordsExporter.Header + "\n", UsageRecordsExporter.ToCsv([]));
    }

    [Fact]
    public void ExportedCsvCanBeReadBackByTheHistoryReader()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "export.csv");
            File.WriteAllText(path, UsageRecordsExporter.ToCsv(
            [
                Sample("codex", 0, 90, 80),
                Failure("claude", 1, "offline")
            ]));

            var result = new UsageHistoryReader().Read(path, "csv");

            Assert.Null(result.Error);
            Assert.Equal(2, result.Entries.Count);
            Assert.Equal(90, result.Entries.Single(entry => entry.Provider == "codex").FiveHourRemainingPercent);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IReadOnlyList<UsageHistoryEntry> NewestFirst(params UsageHistoryEntry[] entries) =>
        entries.OrderByDescending(entry => entry.UpdatedAt).ToArray();

    private static UsageHistoryEntry Sample(
        string provider,
        int minute,
        int fiveHour,
        int weekly,
        DateTimeOffset? fiveHourReset = null) =>
        new(
            Origin.AddMinutes(minute),
            provider,
            "success",
            fiveHour,
            fiveHourReset ?? Origin.AddHours(4),
            weekly,
            Origin.AddDays(4),
            null,
            null);

    private static UsageHistoryEntry Failure(string provider, int minute, string error) =>
        new(Origin.AddMinutes(minute), provider, "error", null, null, null, null, error, null);
}
