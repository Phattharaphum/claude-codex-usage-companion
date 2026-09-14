using CodexUsageCompanion.Ui;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsagePresentationTests
{
    [Theory]
    [InlineData(0, UsageSignal.Gray)]
    [InlineData(1, UsageSignal.Red)]
    [InlineData(39, UsageSignal.Red)]
    [InlineData(40, UsageSignal.Orange)]
    [InlineData(59, UsageSignal.Orange)]
    [InlineData(60, UsageSignal.Yellow)]
    [InlineData(79, UsageSignal.Yellow)]
    [InlineData(80, UsageSignal.Green)]
    [InlineData(100, UsageSignal.Green)]
    public void GetSignalUsesApprovedThresholds(int remainingPercent, UsageSignal expected)
    {
        Assert.Equal(expected, UsagePresentation.GetSignal(remainingPercent));
    }

    [Fact]
    public void GetCellFillRatiosUsesFiveTwentyPercentCells()
    {
        var ratios = UsagePresentation.GetCellFillRatios(51);

        Assert.Equal(new[] { 1d, 1d, 0.55d, 0d, 0d }, ratios);
    }

    [Fact]
    public void FormatFiveHourResetUsesDefaultMonthDayFormat()
    {
        var reset = new DateTimeOffset(2026, 7, 10, 23, 33, 0, TimeSpan.FromHours(8));

        Assert.Equal("於 7月10日 重置", UsagePresentation.FormatFiveHourReset(reset));
    }

    [Fact]
    public void FormatWeeklyResetUsesDefaultMonthDayFormat()
    {
        var reset = new DateTimeOffset(2026, 7, 17, 9, 0, 0, TimeSpan.FromHours(8));

        Assert.Equal("於 7月17日 重置", UsagePresentation.FormatWeeklyReset(reset));
    }

    [Fact]
    public void AntigravityPresentationKeepsPoolsAndWindowsSeparateInFiveHourFirstOrder()
    {
        var state = State(
            [
                Pool("Gemini Models", 89, 88),
                Pool("Claude and GPT models", 100, 80)
            ]);

        var presentation = UsagePresentation.BuildAntigravityPresentation(true, state, null);

        Assert.Equal(AntigravityPresentationKind.QuotaPools, presentation.Kind);
        Assert.Collection(
            presentation.Pools,
            gemini =>
            {
                Assert.Equal("Gemini Models", gemini.Name);
                Assert.Equal(89, gemini.Windows[0].RemainingPercent);
                Assert.Equal(AntigravityQuotaCadence.FiveHour, gemini.Windows[0].Cadence);
                Assert.Equal(88, gemini.Windows[1].RemainingPercent);
                Assert.Equal(AntigravityQuotaCadence.Weekly, gemini.Windows[1].Cadence);
            },
            thirdParty =>
            {
                Assert.Equal("Claude and GPT models", thirdParty.Name);
                Assert.Equal(100, thirdParty.Windows[0].RemainingPercent);
                Assert.Equal(80, thirdParty.Windows[1].RemainingPercent);
            });
    }

    [Fact]
    public void AntigravityPresentationPreservesLongTitlesAndOneHundredPercentValues()
    {
        var pool = new AntigravityQuotaPoolState(
            "third-party",
            "Claude and GPT models",
            new AntigravityQuotaWindowState(
                "3p-5h",
                "Five Hour Limit Remaining",
                AntigravityQuotaCadence.FiveHour,
                100,
                null,
                TimeSpan.FromHours(5)),
            null,
            []);

        var presentation = UsagePresentation.BuildAntigravityPresentation(true, State([pool]), null);

        var window = Assert.Single(Assert.Single(presentation.Pools).Windows);
        Assert.Equal("Five Hour Limit Remaining", window.Name);
        Assert.Equal(100, window.RemainingPercent);
    }

    [Fact]
    public void AntigravityPresentationPrefersAuthoritativePoolsOverModels()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [new AntigravityModelQuotaState("model", "Model observation", 12, null)],
            [Pool("Gemini Models", 89, 88)]);

        var presentation = UsagePresentation.BuildAntigravityPresentation(true, state, null);

        Assert.Equal(AntigravityPresentationKind.QuotaPools, presentation.Kind);
        Assert.Single(presentation.Pools);
        Assert.Equal(1, presentation.ObservedModelCount);
    }

    [Fact]
    public void AntigravityPresentationUsesClearlySeparateModelFallback()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [new AntigravityModelQuotaState("model", "Model observation", 54, null)]);

        var presentation = UsagePresentation.BuildAntigravityPresentation(true, state, null);

        Assert.Equal(AntigravityPresentationKind.ObservedModelFallback, presentation.Kind);
        Assert.Empty(presentation.Pools);
        Assert.Equal(1, presentation.ObservedModelCount);
    }

    [Fact]
    public void AntigravityPresentationAllowsMissingWindowsAndNullResets()
    {
        var pool = new AntigravityQuotaPoolState(
            "gemini-models",
            "Gemini Models",
            null,
            new AntigravityQuotaWindowState(
                "gemini-weekly", "Weekly", AntigravityQuotaCadence.Weekly, 88, null, TimeSpan.FromDays(7)),
            []);

        var presentation = UsagePresentation.BuildAntigravityPresentation(true, State([pool]), null);

        var renderedPool = Assert.Single(presentation.Pools);
        var window = Assert.Single(renderedPool.Windows);
        Assert.Equal(AntigravityQuotaCadence.Weekly, window.Cadence);
        Assert.Null(window.ResetAt);

        var fiveHourOnly = new AntigravityQuotaPoolState(
            "third-party",
            "Claude and GPT models",
            new AntigravityQuotaWindowState(
                "3p-5h", "Five Hour", AntigravityQuotaCadence.FiveHour, 100, null, TimeSpan.FromHours(5)),
            null,
            []);
        var fiveHourPresentation = UsagePresentation.BuildAntigravityPresentation(true, State([fiveHourOnly]), null);

        Assert.Equal(AntigravityQuotaCadence.FiveHour, Assert.Single(fiveHourPresentation.Pools[0].Windows).Cadence);
    }

    [Fact]
    public void AntigravityPresentationPreservesPriorPoolsWhenRuntimePublishesErrorWithState()
    {
        var presentation = UsagePresentation.BuildAntigravityPresentation(
            true,
            State([Pool("Gemini Models", 89, 88)]),
            "Antigravity local usage service was unavailable.");

        Assert.Equal(AntigravityPresentationKind.QuotaPools, presentation.Kind);
        Assert.Single(presentation.Pools);
        Assert.Equal("Antigravity local usage service was unavailable.", presentation.Error);
    }

    [Fact]
    public void AntigravityPresentationHandlesWaitingErrorAndDisabledStates()
    {
        var waiting = UsagePresentation.BuildAntigravityPresentation(true, null, null);
        var failed = UsagePresentation.BuildAntigravityPresentation(true, null, "sanitized failure");
        var disabled = UsagePresentation.BuildAntigravityPresentation(false, State([Pool("Gemini Models", 89, 88)]), null);

        Assert.Equal(AntigravityPresentationKind.Waiting, waiting.Kind);
        Assert.Equal(AntigravityPresentationKind.Error, failed.Kind);
        Assert.Equal("sanitized failure", failed.Error);
        Assert.Equal(AntigravityPresentationKind.Hidden, disabled.Kind);
        Assert.Empty(disabled.Pools);
    }

    private static AntigravityUsageState State(IReadOnlyList<AntigravityQuotaPoolState> pools) =>
        new(null, null, [], pools);

    private static AntigravityQuotaPoolState Pool(string name, int fiveHour, int weekly) =>
        new(
            name.ToLowerInvariant().Replace(" ", "-"),
            name,
            new AntigravityQuotaWindowState(
                "five-hour", "Five Hour", AntigravityQuotaCadence.FiveHour, fiveHour,
                null, TimeSpan.FromHours(5)),
            new AntigravityQuotaWindowState(
                "weekly", "Weekly", AntigravityQuotaCadence.Weekly, weekly,
                null, TimeSpan.FromDays(7)),
            []);
}
