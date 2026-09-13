using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ProgramWatchTests
{
    private const string Secret = "test-csrf-secret-do-not-log";

    [Fact]
    public async Task RefreshAntigravityWatchAsyncDoesNotReadWhenDisabled()
    {
        var called = false;

        var result = await Program.RefreshAntigravityWatchAsync(
            new CompanionSettings(),
            previousState: null,
            _ =>
            {
                called = true;
                return Task.FromResult(State());
            },
            CancellationToken.None);

        Assert.False(called);
        Assert.Null(result.State);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task RefreshAntigravityWatchAsyncReturnsEveryModelWhenEnabled()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [
                new AntigravityModelQuotaState("first", "Gemini", 54, null),
                new AntigravityModelQuotaState("second", "Claude", 100, null)
            ]);

        var result = await Program.RefreshAntigravityWatchAsync(
            new CompanionSettings { EnableAntigravityUsage = true },
            previousState: null,
            _ => Task.FromResult(state),
            CancellationToken.None);

        Assert.Same(state, result.State);
        Assert.Null(result.Error);
        Assert.Equal(2, result.State!.Models.Count);
        Assert.Equal("first", result.State.Models[0].Id);
        Assert.Equal("second", result.State.Models[1].Id);
        Assert.Null(result.State.Account);
        Assert.Null(result.State.Plan);
        Assert.Null(result.State.Models[0].ResetAt);
    }

    [Fact]
    public async Task RefreshAntigravityWatchAsyncPreservesStateAndRecoversAfterFailure()
    {
        var previous = State("previous");
        var settings = new CompanionSettings { EnableAntigravityUsage = true };

        var failed = await Program.RefreshAntigravityWatchAsync(
            settings,
            previous,
            _ => throw new InvalidOperationException($"failed with {Secret}"),
            CancellationToken.None);
        var recovered = await Program.RefreshAntigravityWatchAsync(
            settings,
            failed.State,
            _ => Task.FromResult(State("recovered")),
            CancellationToken.None);

        Assert.Same(previous, failed.State);
        Assert.Equal("Antigravity local usage service was unavailable.", failed.Error);
        Assert.DoesNotContain(Secret, failed.Error);
        Assert.Equal("recovered", recovered.State!.Models[0].Id);
        Assert.Null(recovered.Error);
    }

    [Fact]
    public async Task RefreshAntigravityWatchAsyncPropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Program.RefreshAntigravityWatchAsync(
                new CompanionSettings { EnableAntigravityUsage = true },
                previousState: null,
                _ => Task.FromResult(State()),
                cancellation.Token));
    }

    [Fact]
    public void WatchRendererHandlesNullAccountPlanAndReset()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            ConsoleUsageRenderer.WriteAntigravity(
                new AntigravityUsageState(
                    null,
                    null,
                    [new AntigravityModelQuotaState("model", "Gemini", 54, null)]),
                null,
                UiText.For(UiLanguage.English));
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("Gemini", output.ToString());
        Assert.Contains("Observed remaining: 54%", output.ToString());
    }

    [Fact]
    public void WatchRendererPrefersSharedPoolsWhenTheyAreAvailable()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            ConsoleUsageRenderer.WriteAntigravity(
                new AntigravityUsageState(
                    null,
                    null,
                    [new AntigravityModelQuotaState("model", "Model observation", 54, null)],
                    [new AntigravityQuotaPoolState(
                        "gemini-models",
                        "Gemini Models",
                        new AntigravityQuotaWindowState(
                            "gemini-5h", "Five Hour Limit Remaining", AntigravityQuotaCadence.FiveHour, 89,
                            null, TimeSpan.FromHours(5)),
                        null,
                        [])]),
                null,
                UiText.For(UiLanguage.English));
        }
        finally
        {
            Console.SetOut(original);
        }

        var rendered = output.ToString();
        Assert.Contains("Gemini Models", rendered);
        Assert.Contains("Five Hour Limit Remaining: 89%", rendered);
        Assert.DoesNotContain("Model observation", rendered);
    }

    private static AntigravityUsageState State(string id = "model") =>
        new(
            "sanitized@example.invalid",
            "Pro",
            [new AntigravityModelQuotaState(id, "Gemini", 54, null)]);
}
