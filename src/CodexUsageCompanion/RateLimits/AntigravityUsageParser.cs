using System.Globalization;
using System.Text.Json;

namespace CodexUsageCompanion.RateLimits;

public static class AntigravityUsageParser
{
    public static AntigravityUsageState ParseResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Antigravity usage response root must be an object.");
        }

        if (!root.TryGetProperty("userStatus", out var userStatus) ||
            userStatus.ValueKind != JsonValueKind.Object)
        {
            return new AntigravityUsageState(null, null, []);
        }

        var account = ReadString(userStatus, "email");
        var plan = ReadPlan(userStatus);
        var models = ParseModels(userStatus);
        return new AntigravityUsageState(account, plan, models);
    }

    private static IReadOnlyList<AntigravityModelQuotaState> ParseModels(JsonElement userStatus)
    {
        if (!userStatus.TryGetProperty("cascadeModelConfigData", out var cascade) ||
            cascade.ValueKind != JsonValueKind.Object ||
            !cascade.TryGetProperty("clientModelConfigs", out var configs) ||
            configs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var models = new List<AntigravityModelQuotaState>();
        foreach (var config in configs.EnumerateArray())
        {
            if (config.ValueKind != JsonValueKind.Object ||
                !config.TryGetProperty("quotaInfo", out var quota) ||
                quota.ValueKind != JsonValueKind.Object ||
                !quota.TryGetProperty("remainingFraction", out var fractionElement) ||
                fractionElement.ValueKind != JsonValueKind.Number ||
                !fractionElement.TryGetDouble(out var remainingFraction))
            {
                continue;
            }

            var model = ReadModel(config);
            var label = ReadString(config, "label");
            var id = model ?? ReadStableId(config) ?? label;
            var name = label ?? model ?? ReadStableId(config);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            models.Add(new AntigravityModelQuotaState(
                id,
                name,
                RemainingPercent(remainingFraction),
                ReadResetAt(quota)));
        }

        return models;
    }

    private static string? ReadPlan(JsonElement userStatus)
    {
        return userStatus.TryGetProperty("planStatus", out var planStatus) &&
               planStatus.ValueKind == JsonValueKind.Object &&
               planStatus.TryGetProperty("planInfo", out var planInfo) &&
               planInfo.ValueKind == JsonValueKind.Object
            ? ReadString(planInfo, "planName")
            : null;
    }

    private static string? ReadModel(JsonElement config)
    {
        return config.TryGetProperty("modelOrAlias", out var modelOrAlias) &&
               modelOrAlias.ValueKind == JsonValueKind.Object
            ? ReadString(modelOrAlias, "model")
            : null;
    }

    private static string? ReadStableId(JsonElement config) =>
        ReadString(config, "modelId") ?? ReadString(config, "id");

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // Antigravity returns a remaining fraction. Match Claude parsing by rounding the
    // resulting percentage to the nearest integer, with midpoint values away from zero.
    private static int RemainingPercent(double remainingFraction)
    {
        return (int)Math.Round(
            Math.Clamp(remainingFraction, 0d, 1d) * 100d,
            MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset? ReadResetAt(JsonElement quota)
    {
        var resetTime = ReadString(quota, "resetTime");
        return resetTime is not null && DateTimeOffset.TryParse(
            resetTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
