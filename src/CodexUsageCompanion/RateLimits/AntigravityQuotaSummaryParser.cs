using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CodexUsageCompanion.RateLimits;

public static class AntigravityQuotaSummaryParser
{
    public static IReadOnlyList<AntigravityQuotaPoolState> ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Antigravity quota summary response root must be an object.");
        }

        var groups = FindGroups(root);
        if (groups is null)
        {
            return [];
        }

        var pools = new List<AntigravityQuotaPoolState>();
        var index = 0;
        foreach (var group in groups.Value.EnumerateArray())
        {
            index++;
            if (group.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(group, "displayName") ?? ReadString(group, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var poolId = ReadString(group, "id") ?? ToStableId(name, index);
            var (fiveHour, weekly) = ParseWindows(group, poolId);
            if (fiveHour is null && weekly is null)
            {
                continue;
            }

            // The local grouped response does not supply stable model membership.
            pools.Add(new AntigravityQuotaPoolState(poolId, name, fiveHour, weekly, []));
        }

        return pools;
    }

    private static JsonElement? FindGroups(JsonElement root)
    {
        foreach (var container in Containers(root))
        {
            foreach (var property in new[] { "groups", "quotaGroups" })
            {
                if (container.TryGetProperty(property, out var groups) &&
                    groups.ValueKind == JsonValueKind.Array)
                {
                    return groups;
                }
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> Containers(JsonElement root)
    {
        foreach (var property in new[] { "response", "summary" })
        {
            if (root.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                yield return nested;
            }
        }

        yield return root;
    }

    private static (AntigravityQuotaWindowState? FiveHour, AntigravityQuotaWindowState? Weekly) ParseWindows(
        JsonElement group,
        string poolId)
    {
        if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        AntigravityQuotaWindowState? fiveHour = null;
        AntigravityQuotaWindowState? weekly = null;
        foreach (var bucket in buckets.EnumerateArray())
        {
            if (bucket.ValueKind != JsonValueKind.Object || ReadBool(bucket, "disabled") == true)
            {
                continue;
            }

            var cadence = ReadCadence(bucket);
            if (cadence is AntigravityQuotaCadence.Unknown ||
                (cadence is AntigravityQuotaCadence.FiveHour && fiveHour is not null) ||
                (cadence is AntigravityQuotaCadence.Weekly && weekly is not null) ||
                !TryReadRemainingFraction(bucket, out var remainingFraction))
            {
                continue;
            }

            var name = ReadString(bucket, "displayName") ??
                       ReadString(bucket, "name") ??
                       (cadence == AntigravityQuotaCadence.FiveHour ? "Five Hour Limit" : "Weekly Limit");
            var id = ReadString(bucket, "bucketId") ??
                     $"{poolId}-{ToStableId(name, cadence == AntigravityQuotaCadence.FiveHour ? 1 : 2)}";
            var window = new AntigravityQuotaWindowState(
                id,
                name,
                cadence,
                RemainingPercent(remainingFraction),
                ReadResetAt(bucket),
                cadence == AntigravityQuotaCadence.FiveHour ? TimeSpan.FromHours(5) : TimeSpan.FromDays(7));

            if (cadence == AntigravityQuotaCadence.FiveHour)
            {
                fiveHour = window;
            }
            else
            {
                weekly = window;
            }
        }

        return (fiveHour, weekly);
    }

    private static AntigravityQuotaCadence ReadCadence(JsonElement bucket)
    {
        var explicitWindow = ReadString(bucket, "window");
        var explicitCadence = CadenceFromText(explicitWindow);
        if (explicitCadence != AntigravityQuotaCadence.Unknown)
        {
            return explicitCadence;
        }

        return CadenceFromText(string.Join(" ", new[]
        {
            ReadString(bucket, "bucketId"),
            ReadString(bucket, "displayName"),
            ReadString(bucket, "name")
        }.Where(value => value is not null)));
    }

    private static AntigravityQuotaCadence CadenceFromText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AntigravityQuotaCadence.Unknown;
        }

        var normalized = value.Trim().ToLowerInvariant().Replace('_', '-');
        if (normalized.Contains("weekly", StringComparison.Ordinal))
        {
            return AntigravityQuotaCadence.Weekly;
        }

        return normalized.Contains("five-hour", StringComparison.Ordinal) ||
               normalized.Contains("five hour", StringComparison.Ordinal) ||
               normalized.Contains("5h", StringComparison.Ordinal) ||
               normalized.Contains("hourly", StringComparison.Ordinal)
            ? AntigravityQuotaCadence.FiveHour
            : AntigravityQuotaCadence.Unknown;
    }

    private static bool TryReadRemainingFraction(JsonElement bucket, out double fraction)
    {
        if (TryReadNumber(bucket, "remainingFraction", out fraction))
        {
            return IsFraction(fraction);
        }

        if (bucket.TryGetProperty("remaining", out var remaining) && remaining.ValueKind == JsonValueKind.Object)
        {
            if (TryReadNumber(remaining, "remainingFraction", out fraction) ||
                (ReadString(remaining, "case") == "remainingFraction" &&
                 TryReadNumber(remaining, "value", out fraction)))
            {
                return IsFraction(fraction);
            }
        }

        if (TryReadNumber(bucket, "used", out var used) &&
            TryReadNumber(bucket, "limit", out var limit) &&
            limit > 0 && used >= 0 && used <= limit)
        {
            fraction = 1d - (used / limit);
            return true;
        }

        fraction = 0;
        return false;
    }

    private static bool TryReadNumber(JsonElement parent, string propertyName, out double value)
    {
        value = 0;
        return parent.TryGetProperty(propertyName, out var element) &&
               element.ValueKind == JsonValueKind.Number &&
               element.TryGetDouble(out value) &&
               double.IsFinite(value);
    }

    private static bool IsFraction(double value) => value is >= 0d and <= 1d;

    private static int RemainingPercent(double remainingFraction) => (int)Math.Round(
        Math.Clamp(remainingFraction, 0d, 1d) * 100d,
        MidpointRounding.AwayFromZero);

    private static DateTimeOffset? ReadResetAt(JsonElement bucket)
    {
        var resetTime = ReadString(bucket, "resetTime");
        return resetTime is not null && DateTimeOffset.TryParse(
            resetTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool? ReadBool(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static string ToStableId(string value, int fallbackIndex)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return builder.Length > 0 ? builder.ToString() : $"pool-{fallbackIndex}";
    }
}
