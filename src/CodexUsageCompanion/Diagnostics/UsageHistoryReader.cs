using System.Globalization;
using System.Text;
using CodexUsageCompanion.Configuration;

namespace CodexUsageCompanion.Diagnostics;

public sealed record UsageHistoryEntry(
    DateTimeOffset UpdatedAt,
    string Provider,
    string Status,
    int? FiveHourRemainingPercent,
    DateTimeOffset? FiveHourResetAt,
    int? WeeklyRemainingPercent,
    DateTimeOffset? WeeklyResetAt,
    string? Error,
    string? Scope);

public sealed record UsageHistoryReadResult(
    IReadOnlyList<UsageHistoryEntry> Entries,
    string? Error);

/// <summary>
/// Reads the local usage-history CSV for the history dashboard. It accepts both
/// the original nine-column CSV and the current extended schema.
/// </summary>
/// <remarks>
/// The log grows by a few kilobytes per refresh and reaches tens of megabytes,
/// and several windows read it. Parsed rows are kept per file and, while the
/// file only grew, just the appended bytes are parsed. The cache is dropped
/// whenever the start of the file or the end of what was already read changed
/// (for example after the retention pruner rewrote it).
/// </remarks>
public sealed class UsageHistoryReader
{
    private const int FingerprintBytes = 64;
    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, FileCache> Caches = new(StringComparer.Ordinal);

    public UsageHistoryReadResult Read(string filePath, string format)
    {
        if (UsageLogOptions.NormalizeFormat(format) != UsageLogOptions.Csv)
        {
            return new UsageHistoryReadResult(
                [],
                "Usage history is available when logging format is CSV.");
        }

        var path = UsageLogOptions.NormalizeFilePath(filePath, UsageLogOptions.Csv);
        try
        {
            lock (CacheGate)
            {
                return ReadCached(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            ArgumentException or
            NotSupportedException)
        {
            return new UsageHistoryReadResult([], $"Unable to read usage history: {exception.Message}");
        }
    }

    /// <summary>Forgets cached rows so the next read parses the file from scratch.</summary>
    public static void ClearCache()
    {
        lock (CacheGate)
        {
            Caches.Clear();
        }
    }

    private static UsageHistoryReadResult ReadCached(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            Caches.Remove(path);
            return new UsageHistoryReadResult([], null);
        }

        if (Caches.TryGetValue(path, out var cache) && !cache.IsStillPrefixOf(path, info.Length))
        {
            Caches.Remove(path);
            cache = null;
        }

        var fresh = cache is null;
        cache ??= new FileCache();
        if (cache.Offset < info.Length)
        {
            var chunk = ReadBytes(path, cache.Offset, info.Length - cache.Offset);
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(chunk);
            var rows = ParseCsv(text);
            var added = new List<UsageHistoryEntry>();
            var columns = cache.Columns;
            var dataRows = rows.AsEnumerable();
            if (columns is null)
            {
                if (rows.Count == 0)
                {
                    return new UsageHistoryReadResult([], null);
                }

                columns = rows[0]
                    .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
                    .ToDictionary(column => column.Name, column => column.Index, StringComparer.OrdinalIgnoreCase);
                if (!columns.ContainsKey("updated_at") || !columns.ContainsKey("provider"))
                {
                    return new UsageHistoryReadResult([], "The history file does not contain a recognized CSV header.");
                }

                dataRows = rows.Skip(1);
            }

            foreach (var row in dataRows)
            {
                if (TryCreateEntry(row, columns) is { } entry)
                {
                    added.Add(entry);
                }
            }

            // Commit only after the whole chunk parsed, so a bad chunk cannot
            // leave a half-updated cache behind.
            cache.Columns = columns;
            cache.Entries.AddRange(added);
            cache.Offset = info.Length;
            cache.Remember(path);
            cache.Sorted = null;
            Caches[path] = cache;
        }
        else if (fresh)
        {
            // An empty file: nothing to cache and nothing to show.
            return new UsageHistoryReadResult([], null);
        }

        return new UsageHistoryReadResult(cache.Sorted ??= cache.Entries
            .OrderByDescending(entry => entry.UpdatedAt)
            .ToArray(), null);
    }

    private static UsageHistoryEntry? TryCreateEntry(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columns)
    {
        var provider = Value(row, columns, "provider");
        if (string.IsNullOrWhiteSpace(provider) ||
            !TryDate(Value(row, columns, "updated_at"), out var updatedAt))
        {
            return null;
        }

        var scope = Value(row, columns, "scope");
        // Older development builds could write observed model rows.
        // They are intentionally not shown in the two-pool history UI.
        if (string.Equals(provider, "antigravity", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(scope, "model", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new UsageHistoryEntry(
            updatedAt,
            provider,
            Value(row, columns, "status") ?? "success",
            Integer(Value(row, columns, "five_hour_remaining_percent")),
            Date(Value(row, columns, "five_hour_reset_at")),
            Integer(Value(row, columns, "weekly_remaining_percent")),
            Date(Value(row, columns, "weekly_reset_at")),
            Value(row, columns, "error"),
            scope);
    }

    private static byte[] ReadBytes(string path, long offset, long count)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[count];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private sealed class FileCache
    {
        public long Offset { get; set; }
        public Dictionary<string, int>? Columns { get; set; }
        public List<UsageHistoryEntry> Entries { get; } = [];
        public UsageHistoryEntry[]? Sorted { get; set; }
        private byte[] _head = [];
        private byte[] _tail = [];

        /// <summary>Remembers the file's first bytes and the bytes just before the read offset.</summary>
        public void Remember(string path)
        {
            _head = ReadSlice(path, 0, Math.Min(FingerprintBytes, Offset));
            var tailLength = Math.Min(FingerprintBytes, Offset);
            _tail = ReadSlice(path, Offset - tailLength, tailLength);
        }

        /// <summary>
        /// True while the file still begins with what was read before, so only
        /// the bytes after <see cref="Offset"/> are new.
        /// </summary>
        public bool IsStillPrefixOf(string path, long currentLength)
        {
            if (currentLength < Offset)
            {
                return false;
            }

            if (Offset == 0)
            {
                return true;
            }

            return ReadSlice(path, 0, _head.Length).AsSpan().SequenceEqual(_head) &&
                   ReadSlice(path, Offset - _tail.Length, _tail.Length).AsSpan().SequenceEqual(_tail);
        }

        private static byte[] ReadSlice(string path, long offset, long count)
        {
            if (count <= 0)
            {
                return [];
            }

            return ReadBytes(path, offset, count);
        }
    }

    private static string? Value(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columns,
        string name)
    {
        if (!columns.TryGetValue(name, out var index) || index >= row.Count)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(row[index]) ? null : row[index];
    }

    private static int? Integer(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static DateTimeOffset? Date(string? value) =>
        TryDate(value, out var parsed) ? parsed : null;

    private static bool TryDate(string? value, out DateTimeOffset parsed) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out parsed);

    private static List<List<string>> ParseCsv(string contents)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < contents.Length; index++)
        {
            var character = contents[index];
            if (inQuotes)
            {
                if (character == '"')
                {
                    if (index + 1 < contents.Length && contents[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    if (character == '\r' && index + 1 < contents.Length && contents[index + 1] == '\n')
                    {
                        index++;
                    }

                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (inQuotes)
        {
            throw new InvalidDataException("The usage-history CSV contains an unterminated quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
