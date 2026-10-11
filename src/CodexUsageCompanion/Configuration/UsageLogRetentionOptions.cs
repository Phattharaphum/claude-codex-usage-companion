namespace CodexUsageCompanion.Configuration;

public static class UsageLogRetentionOptions
{
    /// <summary>Keep every record. The log then grows without bound.</summary>
    public const int Forever = 0;

    public const int DefaultDays = 90;
    public const int MaximumDays = 3650;

    public static IReadOnlyList<int> Choices { get; } = Array.AsReadOnly(
    [
        30,
        90,
        180,
        365,
        Forever
    ]);

    public static int Normalize(int days) =>
        days < 0 ? DefaultDays : Math.Min(days, MaximumDays);
}
