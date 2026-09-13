using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Platform;

public enum LinuxStatusNotifierPresentationKind
{
    Hidden,
    Waiting,
    QuotaPools
}

public sealed record LinuxStatusNotifierQuotaWindowPresentation(
    string Name,
    int RemainingPercent);

public sealed record LinuxStatusNotifierQuotaPoolPresentation(
    string Name,
    IReadOnlyList<LinuxStatusNotifierQuotaWindowPresentation> Windows);

public sealed record LinuxStatusNotifierPresentation(
    LinuxStatusNotifierPresentationKind Kind,
    IReadOnlyList<LinuxStatusNotifierQuotaPoolPresentation> Pools);

/// <summary>
/// Represents only the authoritative Antigravity quota-pool information that a
/// Linux StatusNotifierItem menu can display. It deliberately has no model list:
/// model observations are not quota pools and would make the indicator noisy.
/// </summary>
public static class LinuxStatusNotifierPresentationBuilder
{
    public static LinuxStatusNotifierPresentation Build(
        bool antigravityEnabled,
        AntigravityUsageState? state)
    {
        if (!antigravityEnabled)
        {
            return new LinuxStatusNotifierPresentation(
                LinuxStatusNotifierPresentationKind.Hidden,
                []);
        }

        if (state?.QuotaPools.Count > 0)
        {
            var pools = state.QuotaPools.Select(pool =>
            {
                var windows = new[] { pool.FiveHour, pool.Weekly }
                    .OfType<AntigravityQuotaWindowState>()
                    .Select(window => new LinuxStatusNotifierQuotaWindowPresentation(
                        window.Name,
                        window.RemainingPercent))
                    .ToArray();
                return new LinuxStatusNotifierQuotaPoolPresentation(pool.Name, windows);
            }).ToArray();
            return new LinuxStatusNotifierPresentation(
                LinuxStatusNotifierPresentationKind.QuotaPools,
                pools);
        }

        return new LinuxStatusNotifierPresentation(
            LinuxStatusNotifierPresentationKind.Waiting,
            []);
    }
}
