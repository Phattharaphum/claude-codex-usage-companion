using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Ui;

public enum UsageSignal
{
    Gray,
    Red,
    Orange,
    Yellow,
    Green
}

public enum AntigravityPresentationKind
{
    Hidden,
    Waiting,
    Error,
    ObservedModelFallback,
    QuotaPools
}

public sealed record AntigravityQuotaPoolPresentation(
    string Name,
    IReadOnlyList<AntigravityQuotaWindowState> Windows);

public sealed record AntigravityPresentationState(
    AntigravityPresentationKind Kind,
    IReadOnlyList<AntigravityQuotaPoolPresentation> Pools,
    int ObservedModelCount,
    string? Error);

public static class UsagePresentation
{
    public static AntigravityPresentationState BuildAntigravityPresentation(
        bool enabled,
        AntigravityUsageState? state,
        string? error)
    {
        if (!enabled)
        {
            return new AntigravityPresentationState(AntigravityPresentationKind.Hidden, [], 0, null);
        }

        if (state?.QuotaPools.Count > 0)
        {
            var pools = state.QuotaPools.Select(pool =>
            {
                // Keep the short rolling window first in every provider group:
                // 5hr is the immediate limit, followed by the weekly limit.
                var windows = new[] { pool.FiveHour, pool.Weekly }
                    .OfType<AntigravityQuotaWindowState>()
                    .ToArray();
                return new AntigravityQuotaPoolPresentation(pool.Name, windows);
            }).ToArray();
            return new AntigravityPresentationState(
                AntigravityPresentationKind.QuotaPools,
                pools,
                state.Models.Count,
                error);
        }

        if (state?.Models.Count > 0)
        {
            return new AntigravityPresentationState(
                AntigravityPresentationKind.ObservedModelFallback,
                [],
                state.Models.Count,
                error);
        }

        return new AntigravityPresentationState(
            string.IsNullOrWhiteSpace(error)
                ? AntigravityPresentationKind.Waiting
                : AntigravityPresentationKind.Error,
            [],
            0,
            error);
    }

    public static UsageSignal GetSignal(int remainingPercent)
    {
        var remaining = Math.Clamp(remainingPercent, 0, 100);
        return remaining switch
        {
            0 => UsageSignal.Gray,
            < 40 => UsageSignal.Red,
            < 60 => UsageSignal.Orange,
            < 80 => UsageSignal.Yellow,
            _ => UsageSignal.Green
        };
    }

    public static double[] GetCellFillRatios(int remainingPercent)
    {
        var remaining = Math.Clamp(remainingPercent, 0, 100);
        return Enumerable.Range(0, 5)
            .Select(index => Math.Clamp((remaining - index * 20) / 20d, 0d, 1d))
            .ToArray();
    }

    public static string FormatFiveHourReset(DateTimeOffset localReset)
    {
        return UiText.For(UiLanguage.TraditionalChinese).FormatFiveHourReset(localReset);
    }

    public static string FormatWeeklyReset(DateTimeOffset localReset)
    {
        return UiText.For(UiLanguage.TraditionalChinese).FormatWeeklyReset(localReset);
    }
}
