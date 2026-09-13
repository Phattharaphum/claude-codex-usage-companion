using CodexUsageCompanion.Platform;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class LinuxStatusNotifierPresentationTests
{
    [Fact]
    public void DisabledAntigravityHidesIndicatorDataWithoutReadingModels()
    {
        var presentation = LinuxStatusNotifierPresentationBuilder.Build(
            false,
            State([Pool("Gemini Models", 89, 88)]));

        Assert.Equal(LinuxStatusNotifierPresentationKind.Hidden, presentation.Kind);
        Assert.Empty(presentation.Pools);
    }

    [Fact]
    public void AuthoritativePoolsKeepGeminiAndClaudeGptSeparateWithFiveHourFirst()
    {
        var models = Enumerable.Range(1, 14)
            .Select(index => new AntigravityModelQuotaState(
                $"model-{index}", $"Model {index}", index, null))
            .ToArray();
        var state = new AntigravityUsageState(
            null,
            null,
            models,
            [
                Pool("Gemini Models", 89, 88),
                Pool("Claude and GPT models", 100, 80)
            ]);

        var presentation = LinuxStatusNotifierPresentationBuilder.Build(true, state);

        Assert.Equal(LinuxStatusNotifierPresentationKind.QuotaPools, presentation.Kind);
        Assert.Equal(2, presentation.Pools.Count);
        Assert.Equal("Gemini Models", presentation.Pools[0].Name);
        Assert.Equal("Claude and GPT models", presentation.Pools[1].Name);
        Assert.Collection(
            presentation.Pools[0].Windows,
            fiveHour =>
            {
                Assert.Equal("Five Hour", fiveHour.Name);
                Assert.Equal(89, fiveHour.RemainingPercent);
            },
            weekly =>
            {
                Assert.Equal("Weekly", weekly.Name);
                Assert.Equal(88, weekly.RemainingPercent);
            });
        Assert.Collection(
            presentation.Pools[1].Windows,
            fiveHour => Assert.Equal(100, fiveHour.RemainingPercent),
            weekly => Assert.Equal(80, weekly.RemainingPercent));
        Assert.Equal(4, presentation.Pools.Sum(pool => pool.Windows.Count));
        Assert.DoesNotContain(
            presentation.Pools,
            pool => pool.Name.StartsWith("Model ", StringComparison.Ordinal));
    }

    [Fact]
    public void LastKnownPoolsRemainPresentAfterATransientError()
    {
        var state = State([Pool("Gemini Models", 89, 88)]);

        var presentation = LinuxStatusNotifierPresentationBuilder.Build(true, state);

        Assert.Equal(LinuxStatusNotifierPresentationKind.QuotaPools, presentation.Kind);
        Assert.Equal(89, presentation.Pools[0].Windows[0].RemainingPercent);
    }

    [Fact]
    public void EnabledAntigravityWithoutPoolsShowsWaitingInsteadOfModelExpansion()
    {
        var presentation = LinuxStatusNotifierPresentationBuilder.Build(
            true,
            new AntigravityUsageState(
                null,
                null,
                [new AntigravityModelQuotaState("model", "Model", 45, null)]));

        Assert.Equal(LinuxStatusNotifierPresentationKind.Waiting, presentation.Kind);
        Assert.Empty(presentation.Pools);
    }

    private static AntigravityUsageState State(
        IReadOnlyList<AntigravityQuotaPoolState> pools) =>
        new(null, null, [], pools);

    private static AntigravityQuotaPoolState Pool(
        string name,
        int fiveHour,
        int weekly) =>
        new(
            name,
            name,
            new AntigravityQuotaWindowState(
                "five-hour",
                "Five Hour",
                AntigravityQuotaCadence.FiveHour,
                fiveHour,
                null,
                TimeSpan.FromHours(5)),
            new AntigravityQuotaWindowState(
                "weekly",
                "Weekly",
                AntigravityQuotaCadence.Weekly,
                weekly,
                null,
                TimeSpan.FromDays(7)),
            []);
}
