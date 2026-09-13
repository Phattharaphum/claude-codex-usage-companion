using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexUsageCompanion.RateLimits;

// This is the model-level observation returned by GetUserStatus. It is useful
// metadata, but it is not an authoritative shared quota window.
public sealed record AntigravityModelQuotaState(
    string Id,
    string Name,
    int RemainingPercent,
    DateTimeOffset? ResetAt);

public sealed record AntigravityUsageState(
    string? Account,
    string? Plan,
    IReadOnlyList<AntigravityModelQuotaState> Models)
{
    public IReadOnlyList<AntigravityQuotaPoolState> QuotaPools { get; init; } = [];

    public AntigravityUsageState(
        string? account,
        string? plan,
        IReadOnlyList<AntigravityModelQuotaState> models,
        IReadOnlyList<AntigravityQuotaPoolState> quotaPools)
        : this(account, plan, models)
    {
        QuotaPools = quotaPools;
    }
}

[JsonConverter(typeof(AntigravityQuotaCadenceJsonConverter))]
public enum AntigravityQuotaCadence
{
    Unknown,
    FiveHour,
    Weekly
}

public sealed class AntigravityQuotaCadenceJsonConverter
    : JsonStringEnumConverter<AntigravityQuotaCadence>
{
    public AntigravityQuotaCadenceJsonConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}

public sealed record AntigravityQuotaWindowState(
    string Id,
    string Name,
    AntigravityQuotaCadence Cadence,
    int RemainingPercent,
    DateTimeOffset? ResetAt,
    TimeSpan? WindowDuration);

public sealed record AntigravityQuotaPoolState(
    string Id,
    string Name,
    AntigravityQuotaWindowState? FiveHour,
    AntigravityQuotaWindowState? Weekly,
    IReadOnlyList<string> ModelIds);
