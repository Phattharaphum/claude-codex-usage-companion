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
public sealed class UsageHistoryReader
{
    public UsageHistoryReadResult Read(string filePath, string format)
    {
        if (UsageLogOptions.NormalizeFormat(format) != UsageLogOptions.Csv)
        {
            return new UsageHistoryReadResult(
                [],
                "Usage history is available when logging format is CSV.");
        }

        var path = UsageLogOptions.NormalizeFilePath(filePath, UsageLogOptions.Csv);
        if (!File.Exists(path))
        {
            return new UsageHistoryReadResult([], null);
        }

        try
        {
            var rows = ParseCsv(File.ReadAllText(path));
            if (rows.Count == 0)
            {
                return new UsageHistoryReadResult([], null);
            }

            var columns = rows[0]
                .Select((name, index) => new { Name = name.Trim().TrimStart('\uFEFF'), Index = index })
                .ToDictionary(column => column.Name, column => column.Index, StringComparer.OrdinalIgnoreCase);
            if (!columns.ContainsKey("updated_at") || !columns.ContainsKey("provider"))
            {
                return new UsageHistoryReadResult([], "The history file does not contain a recognized CSV header.");
            }

            var entries = new List<UsageHistoryEntry>();
            foreach (var row in rows.Skip(1))
            {
                var provider = Value(row, columns, "provider");
                if (string.IsNullOrWhiteSpace(provider) ||
                    !TryDate(Value(row, columns, "updated_at"), out var updatedAt))
                {
                    continue;
                }

                var scope = Value(row, columns, "scope");
                // Older development builds could write observed model rows.
                // They are intentionally not shown in the two-pool history UI.
                if (string.Equals(provider, "antigravity", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(scope, "model", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.Add(new UsageHistoryEntry(
                    updatedAt,
                    provider,
                    Value(row, columns, "status") ?? "success",
                    Integer(Value(row, columns, "five_hour_remaining_percent")),
                    Date(Value(row, columns, "five_hour_reset_at")),
                    Integer(Value(row, columns, "weekly_remaining_percent")),
                    Date(Value(row, columns, "weekly_reset_at")),
                    Value(row, columns, "error"),
                    scope));
            }

            return new UsageHistoryReadResult(
                entries.OrderByDescending(entry => entry.UpdatedAt).ToArray(),
                null);
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
