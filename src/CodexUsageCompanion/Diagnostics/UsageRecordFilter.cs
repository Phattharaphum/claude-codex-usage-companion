using System.Globalization;
using System.Text;

namespace CodexUsageCompanion.Diagnostics;

/// <summary>What the Records tab narrows its table to.</summary>
public sealed record UsageRecordFilterOptions
{
    /// <summary>
    /// Keep only samples where something changed since the provider's previous
    /// sample. A log written every minute is mostly the same row repeated.
    /// </summary>
    public bool ChangedOnly { get; init; }

    public bool ErrorsOnly { get; init; }

    /// <summary>Space-separated terms; every term must match somewhere in the row.</summary>
    public string? Search { get; init; }
}

public static class UsageRecordFilter
{
    /// <summary>
    /// A reset time that moves by less than this is the provider re-estimating
    /// ("now + 5 hours" for an idle window), not a change worth showing.
    /// </summary>
    public static readonly TimeSpan ResetTimeTolerance = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<UsageHistoryEntry> Apply(
        IReadOnlyList<UsageHistoryEntry> newestFirst,
        UsageRecordFilterOptions options,
        Func<string, string>? providerName = null)
    {
        IEnumerable<UsageHistoryEntry> rows = newestFirst;
        if (options.ChangedOnly)
        {
            rows = KeepChanges(newestFirst);
        }

        if (options.ErrorsOnly)
        {
            rows = rows.Where(entry => !IsSuccess(entry));
        }

        var terms = (options.Search ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length > 0)
        {
            rows = rows.Where(entry =>
            {
                var haystack = SearchText(entry, providerName);
                return terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
            });
        }

        return rows.ToArray();
    }

    private static List<UsageHistoryEntry> KeepChanges(IReadOnlyList<UsageHistoryEntry> newestFirst)
    {
        var keep = new bool[newestFirst.Count];
        var previousByProvider = new Dictionary<string, UsageHistoryEntry>(StringComparer.OrdinalIgnoreCase);
        // Walk oldest to newest so each row is compared with what came before it.
        for (var index = newestFirst.Count - 1; index >= 0; index--)
        {
            var entry = newestFirst[index];
            keep[index] = !previousByProvider.TryGetValue(entry.Provider, out var previous) ||
                          Changed(previous, entry);
            previousByProvider[entry.Provider] = entry;
        }

        var kept = new List<UsageHistoryEntry>();
        for (var index = 0; index < newestFirst.Count; index++)
        {
            if (keep[index])
            {
                kept.Add(newestFirst[index]);
            }
        }

        return kept;
    }

    private static bool Changed(UsageHistoryEntry previous, UsageHistoryEntry current)
    {
        if (IsSuccess(previous) != IsSuccess(current))
        {
            return true;
        }

        if (!IsSuccess(current))
        {
            return !string.Equals(previous.Error, current.Error, StringComparison.Ordinal);
        }

        return previous.FiveHourRemainingPercent != current.FiveHourRemainingPercent ||
               previous.WeeklyRemainingPercent != current.WeeklyRemainingPercent ||
               ResetMoved(previous.FiveHourResetAt, current.FiveHourResetAt) ||
               ResetMoved(previous.WeeklyResetAt, current.WeeklyResetAt);
    }

    private static bool ResetMoved(DateTimeOffset? previous, DateTimeOffset? current)
    {
        if (previous is null || current is null)
        {
            return previous is null != current is null;
        }

        return (current.Value - previous.Value).Duration() >= ResetTimeTolerance;
    }

    private static bool IsSuccess(UsageHistoryEntry entry) =>
        string.Equals(entry.Status, "success", StringComparison.OrdinalIgnoreCase);

    private static string SearchText(UsageHistoryEntry entry, Func<string, string>? providerName)
    {
        var local = entry.UpdatedAt.ToLocalTime();
        var builder = new StringBuilder();
        builder.Append(entry.Provider).Append(' ');
        if (providerName is not null)
        {
            builder.Append(providerName(entry.Provider)).Append(' ');
        }

        builder.Append(entry.Status).Append(' ').Append(entry.Error).Append(' ')
            .Append(local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(' ')
            .Append(local.ToString("MMM d", CultureInfo.InvariantCulture)).Append(' ')
            .Append(local.ToString("HH:mm", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
