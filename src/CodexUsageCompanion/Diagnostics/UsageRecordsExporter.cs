using System.Globalization;
using System.Text;

namespace CodexUsageCompanion.Diagnostics;

/// <summary>Writes the records shown in the Records tab as a standalone CSV.</summary>
public static class UsageRecordsExporter
{
    public const string Header =
        "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at," +
        "weekly_remaining_percent,weekly_reset_at,error";

    public static string ToCsv(IEnumerable<UsageHistoryEntry> records)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');
        foreach (var entry in records)
        {
            builder
                .Append(Field(entry.UpdatedAt.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Field(entry.Provider)).Append(',')
                .Append(Field(entry.Status)).Append(',')
                .Append(Field(entry.FiveHourRemainingPercent?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Field(entry.FiveHourResetAt?.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Field(entry.WeeklyRemainingPercent?.ToString(CultureInfo.InvariantCulture))).Append(',')
                .Append(Field(entry.WeeklyResetAt?.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Field(entry.Error))
                .Append('\n');
        }

        return builder.ToString();
    }

    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }
}
