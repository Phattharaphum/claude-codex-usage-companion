namespace CodexUsageCompanion.RateLimits;

public sealed record AntigravityModelQuotaState(
    string Id,
    string Name,
    int RemainingPercent,
    DateTimeOffset? ResetAt);

public sealed record AntigravityUsageState(
    string? Account,
    string? Plan,
    IReadOnlyList<AntigravityModelQuotaState> Models);
