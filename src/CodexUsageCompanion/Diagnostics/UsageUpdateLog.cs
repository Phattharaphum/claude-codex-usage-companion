using System.Globalization;
using System.Text;
using System.Text.Json;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Lifecycle;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Diagnostics;

public sealed class UsageUpdateLog
{
    private const string CsvHeader =
        "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at," +
        "weekly_remaining_percent,weekly_reset_at,available_reset_credits,error," +
        "scope,scope_id,scope_name,account,plan,model_ids,model_count," +
        "observed_remaining_percent,observed_reset_at," +
        "five_hour_window_id,five_hour_window_name,weekly_window_id,weekly_window_name";

    // The first release of usage logging used these nine columns. Keep a
    // migration path so a user's existing history remains a valid CSV table
    // after the Antigravity-specific fields are introduced.
    private const string LegacyCsvHeader =
        "updated_at,provider,status,five_hour_remaining_percent,five_hour_reset_at," +
        "weekly_remaining_percent,weekly_reset_at,available_reset_credits,error";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();

    public void WriteSuccess(
        string filePath,
        string format,
        UsageProvider provider,
        RateLimitState state,
        DateTimeOffset updatedAt)
    {
        Write(filePath, format, CreateEntry(provider, "success", state, updatedAt, null));
    }

    public void WriteFailure(
        string filePath,
        string format,
        UsageProvider provider,
        string error,
        DateTimeOffset updatedAt)
    {
        Write(filePath, format, CreateEntry(provider, "error", null, updatedAt, error));
    }

    /// <summary>
    /// Writes the complete local Antigravity observation. Quota-pool entries
    /// are authoritative shared limits, while model entries preserve the
    /// observed fallback values supplied by GetUserStatus.
    /// </summary>
    public void WriteAntigravity(
        string filePath,
        string format,
        AntigravityUsageState? state,
        string? error,
        DateTimeOffset updatedAt)
    {
        var status = string.IsNullOrWhiteSpace(error) ? "success" : "error";
        var entries = new List<UsageUpdateLogEntry>();

        if (state is not null && string.IsNullOrWhiteSpace(error))
        {
            foreach (var pool in state.QuotaPools)
            {
                entries.Add(CreateAntigravityPoolEntry(pool, state, updatedAt));
            }

            foreach (var model in state.Models)
            {
                entries.Add(CreateAntigravityModelEntry(model, state, updatedAt));
            }
        }

        // A refresh that has no pools/models (or fails) is still significant
        // history: it lets the CSV show exactly when the local source stopped
        // providing usable Antigravity information.
        if (entries.Count == 0)
        {
            entries.Add(new UsageUpdateLogEntry(
                updatedAt.ToString("O", CultureInfo.InvariantCulture),
                "antigravity",
                status,
                null,
                null,
                null,
                null,
                null,
                error,
                "provider",
                null,
                "Antigravity",
                state?.Account,
                state?.Plan,
                null,
                state?.Models.Count,
                null,
                null,
                null,
                null,
                null,
                null));
        }

        Write(filePath, format, entries);
    }

    private void Write(string filePath, string format, UsageUpdateLogEntry entry)
    {
        Write(filePath, format, [entry]);
    }

    private void Write(string filePath, string format, IReadOnlyList<UsageUpdateLogEntry> entries)
    {
        var normalizedFormat = UsageLogOptions.NormalizeFormat(format);
        var normalizedPath = UsageLogOptions.NormalizeFilePath(filePath, normalizedFormat);

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(normalizedPath)
                ?? throw new InvalidOperationException("The usage log directory is unavailable.");
            Directory.CreateDirectory(directory);

            if (normalizedFormat == UsageLogOptions.Csv)
            {
                MigrateLegacyCsvIfNeeded(normalizedPath);
            }

            var addCsvHeader = normalizedFormat == UsageLogOptions.Csv &&
                (!File.Exists(normalizedPath) || new FileInfo(normalizedPath).Length == 0);

            using var writer = new StreamWriter(
                normalizedPath,
                append: true,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (addCsvHeader)
            {
                writer.WriteLine(CsvHeader);
            }

            foreach (var entry in entries)
            {
                var content = normalizedFormat switch
                {
                    UsageLogOptions.Csv => FormatCsv(entry),
                    UsageLogOptions.JsonLines => JsonSerializer.Serialize(entry, JsonOptions),
                    _ => FormatText(entry)
                };
                writer.WriteLine(content);
            }
        }
    }

    private static UsageUpdateLogEntry CreateEntry(
        UsageProvider provider,
        string status,
        RateLimitState? state,
        DateTimeOffset updatedAt,
        string? error)
    {
        return new UsageUpdateLogEntry(
            updatedAt.ToString("O", CultureInfo.InvariantCulture),
            provider == UsageProvider.Claude ? "claude" : "codex",
            status,
            state?.FiveHour?.RemainingPercent,
            FormatReset(state?.FiveHour?.ResetsAt),
            state?.Weekly?.RemainingPercent,
            FormatReset(state?.Weekly?.ResetsAt),
            state?.AvailableResetCredits,
            error,
            "provider",
            null,
            provider == UsageProvider.Claude ? "Claude" : "Codex",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static UsageUpdateLogEntry CreateAntigravityPoolEntry(
        AntigravityQuotaPoolState pool,
        AntigravityUsageState state,
        DateTimeOffset updatedAt)
    {
        return new UsageUpdateLogEntry(
            updatedAt.ToString("O", CultureInfo.InvariantCulture),
            "antigravity",
            "success",
            pool.FiveHour?.RemainingPercent,
            FormatReset(pool.FiveHour?.ResetAt),
            pool.Weekly?.RemainingPercent,
            FormatReset(pool.Weekly?.ResetAt),
            null,
            null,
            "quota_pool",
            pool.Id,
            pool.Name,
            state.Account,
            state.Plan,
            pool.ModelIds.Count == 0 ? null : string.Join(';', pool.ModelIds),
            state.Models.Count,
            null,
            null,
            pool.FiveHour?.Id,
            pool.FiveHour?.Name,
            pool.Weekly?.Id,
            pool.Weekly?.Name);
    }

    private static UsageUpdateLogEntry CreateAntigravityModelEntry(
        AntigravityModelQuotaState model,
        AntigravityUsageState state,
        DateTimeOffset updatedAt)
    {
        return new UsageUpdateLogEntry(
            updatedAt.ToString("O", CultureInfo.InvariantCulture),
            "antigravity",
            "success",
            null,
            null,
            null,
            null,
            null,
            null,
            "model",
            model.Id,
            model.Name,
            state.Account,
            state.Plan,
            model.Id,
            state.Models.Count,
            model.RemainingPercent,
            FormatReset(model.ResetAt),
            null,
            null,
            null,
            null);
    }

    private static string? FormatReset(long? unixSeconds)
    {
        return unixSeconds is long value
            ? DateTimeOffset.FromUnixTimeSeconds(value)
                .ToString("O", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? FormatReset(DateTimeOffset? resetAt) =>
        resetAt?.ToString("O", CultureInfo.InvariantCulture);

    private static string FormatText(UsageUpdateLogEntry entry)
    {
        return string.Join(
            " | ",
            $"updated_at={Text(entry.UpdatedAt)}",
            $"provider={Text(entry.Provider)}",
            $"status={Text(entry.Status)}",
            $"five_hour_remaining_percent={Text(entry.FiveHourRemainingPercent)}",
            $"five_hour_reset_at={Text(entry.FiveHourResetAt)}",
            $"weekly_remaining_percent={Text(entry.WeeklyRemainingPercent)}",
            $"weekly_reset_at={Text(entry.WeeklyResetAt)}",
            $"available_reset_credits={Text(entry.AvailableResetCredits)}",
            $"error={Text(entry.Error)}",
            $"scope={Text(entry.Scope)}",
            $"scope_id={Text(entry.ScopeId)}",
            $"scope_name={Text(entry.ScopeName)}",
            $"account={Text(entry.Account)}",
            $"plan={Text(entry.Plan)}",
            $"model_ids={Text(entry.ModelIds)}",
            $"model_count={Text(entry.ModelCount)}",
            $"observed_remaining_percent={Text(entry.ObservedRemainingPercent)}",
            $"observed_reset_at={Text(entry.ObservedResetAt)}",
            $"five_hour_window_id={Text(entry.FiveHourWindowId)}",
            $"five_hour_window_name={Text(entry.FiveHourWindowName)}",
            $"weekly_window_id={Text(entry.WeeklyWindowId)}",
            $"weekly_window_name={Text(entry.WeeklyWindowName)}");
    }

    private static string FormatCsv(UsageUpdateLogEntry entry)
    {
        return string.Join(
            ',',
            Csv(entry.UpdatedAt),
            Csv(entry.Provider),
            Csv(entry.Status),
            Csv(entry.FiveHourRemainingPercent),
            Csv(entry.FiveHourResetAt),
            Csv(entry.WeeklyRemainingPercent),
            Csv(entry.WeeklyResetAt),
            Csv(entry.AvailableResetCredits),
            Csv(entry.Error),
            Csv(entry.Scope),
            Csv(entry.ScopeId),
            Csv(entry.ScopeName),
            Csv(entry.Account),
            Csv(entry.Plan),
            Csv(entry.ModelIds),
            Csv(entry.ModelCount),
            Csv(entry.ObservedRemainingPercent),
            Csv(entry.ObservedResetAt),
            Csv(entry.FiveHourWindowId),
            Csv(entry.FiveHourWindowName),
            Csv(entry.WeeklyWindowId),
            Csv(entry.WeeklyWindowName));
    }

    private static void MigrateLegacyCsvIfNeeded(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return;
        }

        var contents = File.ReadAllText(path);
        var rows = ParseCsv(contents);
        if (rows.Count == 0 || rows[0].Count == 0)
        {
            return;
        }

        var header = string.Join(',', rows[0]);
        if (string.Equals(header, CsvHeader, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(header, LegacyCsvHeader, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The existing CSV header in '{path}' is not a supported usage-history schema.");
        }

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The usage log directory is unavailable.");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(
                       temporaryPath,
                       append: false,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.WriteLine(CsvHeader);
                foreach (var row in rows.Skip(1))
                {
                    if (row.Count != 9)
                    {
                        throw new InvalidDataException("A legacy usage-history CSV row has an unexpected column count.");
                    }

                    writer.WriteLine(string.Join(',', row.Concat(Enumerable.Repeat(string.Empty, 13)).Select(Csv)));
                }
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

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

    private static string Csv(object? value)
    {
        var text = Value(value);
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;
    }

    private static string Value(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Text(object? value) =>
        Value(value)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private sealed record UsageUpdateLogEntry(
        string UpdatedAt,
        string Provider,
        string Status,
        int? FiveHourRemainingPercent,
        string? FiveHourResetAt,
        int? WeeklyRemainingPercent,
        string? WeeklyResetAt,
        int? AvailableResetCredits,
        string? Error,
        string Scope,
        string? ScopeId,
        string ScopeName,
        string? Account,
        string? Plan,
        string? ModelIds,
        int? ModelCount,
        int? ObservedRemainingPercent,
        string? ObservedResetAt,
        string? FiveHourWindowId,
        string? FiveHourWindowName,
        string? WeeklyWindowId,
        string? WeeklyWindowName);
}
