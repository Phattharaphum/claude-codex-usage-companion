using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodexUsageCompanion.Configuration;

namespace CodexUsageCompanion.Diagnostics;

public sealed record UsageLogPruneResult(int RemovedRecords, long BytesBefore, long BytesAfter);

/// <summary>
/// Drops records older than the retention period from a usage log. The log is
/// append-only and chronological, so pruning is a rewrite of the tail: it
/// streams the file, never builds it in memory, and swaps the result in
/// atomically so a crash cannot leave a half-written history.
/// </summary>
public static partial class UsageLogPruner
{
    // A record starts with its timestamp in every format. The CSV error column
    // can contain quoted newlines, so continuation lines (which never start
    // like this) inherit the decision made for the record they belong to.
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T[^,\s""]+")]
    private static partial Regex CsvRecordStart();

    [GeneratedRegex(@"""updatedAt""\s*:\s*""([^""]+)""")]
    private static partial Regex JsonRecordStart();

    [GeneratedRegex(@"^updated_at=(\S+)")]
    private static partial Regex TextRecordStart();

    public static UsageLogPruneResult Prune(
        string path,
        string format,
        int retentionDays,
        DateTimeOffset now)
    {
        var info = new FileInfo(path);
        if (retentionDays <= UsageLogRetentionOptions.Forever || !info.Exists || info.Length == 0)
        {
            return new UsageLogPruneResult(0, info.Exists ? info.Length : 0, info.Exists ? info.Length : 0);
        }

        var normalizedFormat = UsageLogOptions.NormalizeFormat(format);
        var cutoff = now - TimeSpan.FromDays(retentionDays);
        if (!HasRecordsOlderThan(path, normalizedFormat, cutoff))
        {
            return new UsageLogPruneResult(0, info.Length, info.Length);
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The usage log directory is unavailable.");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.prune");
        var removed = 0;
        try
        {
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            using (var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true))
            using (var writer = new StreamWriter(temporaryPath, append: false, encoding))
            {
                var keeping = true;
                var first = true;
                while (reader.ReadLine() is { } line)
                {
                    if (first && normalizedFormat == UsageLogOptions.Csv)
                    {
                        // The header is not a record.
                        writer.WriteLine(line);
                        first = false;
                        continue;
                    }

                    first = false;
                    if (TryReadTimestamp(line, normalizedFormat, out var timestamp))
                    {
                        keeping = timestamp >= cutoff;
                        if (!keeping)
                        {
                            removed++;
                        }
                    }

                    if (keeping)
                    {
                        writer.WriteLine(line);
                    }
                }
            }

            if (removed == 0)
            {
                return new UsageLogPruneResult(0, info.Length, info.Length);
            }

            File.Move(temporaryPath, path, overwrite: true);
            return new UsageLogPruneResult(removed, info.Length, new FileInfo(path).Length);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    // The log is chronological, so one old record at the front is enough to
    // know a rewrite is worthwhile; a fresh log costs a few lines of reading.
    private static bool HasRecordsOlderThan(string path, string format, DateTimeOffset cutoff)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = 0;
        while (reader.ReadLine() is { } line && lines++ < 64)
        {
            if (TryReadTimestamp(line, format, out var timestamp))
            {
                return timestamp < cutoff;
            }
        }

        return false;
    }

    private static bool TryReadTimestamp(string line, string format, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var match = format switch
        {
            UsageLogOptions.JsonLines => JsonRecordStart().Match(line),
            UsageLogOptions.Text => TextRecordStart().Match(line),
            _ => CsvRecordStart().Match(line)
        };
        if (!match.Success)
        {
            return false;
        }

        var value = format == UsageLogOptions.Csv ? match.Value : match.Groups[1].Value;
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out timestamp);
    }
}
