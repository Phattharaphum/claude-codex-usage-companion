using System.Text.Json;
using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ProgramStatusTests
{
    private const string Secret = "test-csrf-secret-do-not-log";

    [Fact]
    public void BuildStatusPayloadOmitsDisabledAntigravityAndPreservesClaudeCodexShapes()
    {
        var claude = new RateLimitState(new RateLimitWindowState(80, 300, 1_700_000_000), null, null);
        var codex = new RateLimitState(null, new RateLimitWindowState(60, 10080, 1_700_000_100), 2);

        var payload = Program.BuildStatusPayload(
            claudeEnabled: true,
            claude,
            claudeError: null,
            codexEnabled: true,
            codex,
            codexError: null,
            antigravityEnabled: false,
            antigravityState: null,
            antigravityError: null);

        using var document = JsonDocument.Parse(payload.ToJsonString(JsonDefaults.Output));
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("claude", out var claudeProvider));
        Assert.True(root.TryGetProperty("codex", out var codexProvider));
        Assert.False(root.TryGetProperty("antigravity", out _));
        Assert.Equal(80, claudeProvider.GetProperty("state").GetProperty("fiveHour")
            .GetProperty("remainingPercent").GetInt32());
        Assert.Equal(60, codexProvider.GetProperty("state").GetProperty("weekly")
            .GetProperty("remainingPercent").GetInt32());
    }

    [Fact]
    public void BuildStatusPayloadSerializesEveryEnabledAntigravityModel()
    {
        var resetAt = new DateTimeOffset(2026, 9, 13, 7, 44, 7, TimeSpan.Zero);
        var state = new AntigravityUsageState(
            "sanitized@example.invalid",
            "Pro",
            [
                new AntigravityModelQuotaState(
                    "MODEL_PLACEHOLDER_M36",
                    "Gemini 3.1 Pro (Low)",
                    54,
                    resetAt),
                new AntigravityModelQuotaState(
                    "MODEL_PLACEHOLDER_M35",
                    "Claude Sonnet 4.6 (Thinking)",
                    100,
                    resetAt)
            ]);

        var payload = Program.BuildStatusPayload(
            false, null, null,
            false, null, null,
            true, state, null);

        using var document = JsonDocument.Parse(payload.ToJsonString(JsonDefaults.Output));
        var provider = document.RootElement.GetProperty("antigravity");
        Assert.Null(provider.GetProperty("error").GetString());
        var serializedState = provider.GetProperty("state");
        Assert.Equal("sanitized@example.invalid", serializedState.GetProperty("account").GetString());
        Assert.Equal("Pro", serializedState.GetProperty("plan").GetString());
        var models = serializedState.GetProperty("models");
        Assert.Equal(2, models.GetArrayLength());
        Assert.Equal("MODEL_PLACEHOLDER_M36", models[0].GetProperty("id").GetString());
        Assert.Equal("Gemini 3.1 Pro (Low)", models[0].GetProperty("name").GetString());
        Assert.Equal(54, models[0].GetProperty("remainingPercent").GetInt32());
        Assert.Equal(resetAt, DateTimeOffset.Parse(models[0].GetProperty("resetAt").GetString()!));
        Assert.Equal("MODEL_PLACEHOLDER_M35", models[1].GetProperty("id").GetString());
    }

    [Fact]
    public void BuildStatusPayloadSerializesNullAntigravityFieldsSafely()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [new AntigravityModelQuotaState("model", "Model", 50, null)]);

        var payload = Program.BuildStatusPayload(
            false, null, null,
            false, null, null,
            true, state, null);

        using var document = JsonDocument.Parse(payload.ToJsonString(JsonDefaults.Output));
        var serializedState = document.RootElement.GetProperty("antigravity").GetProperty("state");
        Assert.Equal(JsonValueKind.Null, serializedState.GetProperty("account").ValueKind);
        Assert.Equal(JsonValueKind.Null, serializedState.GetProperty("plan").ValueKind);
        Assert.Equal(JsonValueKind.Null, serializedState.GetProperty("models")[0]
            .GetProperty("resetAt").ValueKind);
    }

    [Fact]
    public void BuildStatusPayloadSerializesAntigravityQuotaPoolsAlongsideModels()
    {
        var resetAt = new DateTimeOffset(2026, 9, 13, 7, 44, 7, TimeSpan.Zero);
        var state = new AntigravityUsageState(
            "sanitized@example.invalid",
            "Pro",
            [new AntigravityModelQuotaState("gemini", "Gemini", 89, resetAt)],
            [new AntigravityQuotaPoolState(
                "gemini-models",
                "Gemini Models",
                new AntigravityQuotaWindowState(
                    "gemini-5h", "Five Hour Limit Remaining", AntigravityQuotaCadence.FiveHour, 89,
                    resetAt, TimeSpan.FromHours(5)),
                new AntigravityQuotaWindowState(
                    "gemini-weekly", "Weekly Limit Remaining", AntigravityQuotaCadence.Weekly, 88,
                    resetAt, TimeSpan.FromDays(7)),
                [])]);

        var payload = Program.BuildStatusPayload(
            false, null, null,
            false, null, null,
            true, state, null);

        using var document = JsonDocument.Parse(payload.ToJsonString(JsonDefaults.Output));
        var serializedState = document.RootElement.GetProperty("antigravity").GetProperty("state");
        Assert.Single(serializedState.GetProperty("models").EnumerateArray());
        var pool = Assert.Single(serializedState.GetProperty("quotaPools").EnumerateArray());
        Assert.Equal("Gemini Models", pool.GetProperty("name").GetString());
        Assert.Equal(89, pool.GetProperty("fiveHour").GetProperty("remainingPercent").GetInt32());
        Assert.Equal("fiveHour", pool.GetProperty("fiveHour").GetProperty("cadence").GetString());
        Assert.Equal(88, pool.GetProperty("weekly").GetProperty("remainingPercent").GetInt32());
        Assert.Empty(pool.GetProperty("modelIds").EnumerateArray());
    }

    [Fact]
    public async Task ReadAntigravityUsageAsyncReportsSanitizedFailure()
    {
        var settings = new CompanionSettings { EnableAntigravityUsage = true };

        var result = await Program.ReadAntigravityUsageAsync(
            settings,
            CancellationToken.None,
            _ => throw new InvalidOperationException($"unexpected failure {Secret}"));

        Assert.Null(result.State);
        Assert.Equal("Antigravity local usage service was unavailable.", result.Error);
        Assert.DoesNotContain(Secret, result.Error);
    }

    [Fact]
    public async Task ReadAntigravityUsageAsyncDoesNotReadWhenDisabled()
    {
        var called = false;

        var result = await Program.ReadAntigravityUsageAsync(
            new CompanionSettings(),
            CancellationToken.None,
            _ =>
            {
                called = true;
                return Task.FromResult(new AntigravityUsageState(null, null, []));
            });

        Assert.Null(result.State);
        Assert.Null(result.Error);
        Assert.False(called);
    }

    [Fact]
    public void WriteAntigravityRendersStateAndError()
    {
        var text = UiText.For(UiLanguage.English);
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            ConsoleUsageRenderer.WriteAntigravity(
                new AntigravityUsageState(
                    "sanitized@example.invalid",
                    "Pro",
                    [new AntigravityModelQuotaState("model", "Gemini", 54, null)]),
                null,
                text);
            ConsoleUsageRenderer.WriteAntigravity(null, "Antigravity local usage service was unavailable.", text);
        }
        finally
        {
            Console.SetOut(original);
        }

        var rendered = output.ToString();
        Assert.Contains("Antigravity usage", rendered);
        Assert.Contains("Account: sanitized@example.invalid", rendered);
        Assert.Contains("Plan: Pro", rendered);
        Assert.Contains("Gemini", rendered);
        Assert.Contains("Observed remaining: 54%", rendered);
        Assert.Contains("Antigravity local usage service was unavailable.", rendered);
    }

    [Fact]
    public void WriteAntigravityPrefersSharedQuotaPoolsOverModelObservations()
    {
        var state = new AntigravityUsageState(
            null,
            null,
            [new AntigravityModelQuotaState("gemini", "Model-only observation", 12, null)],
            [new AntigravityQuotaPoolState(
                "gemini-models",
                "Gemini Models",
                new AntigravityQuotaWindowState(
                    "gemini-5h", "Five Hour Limit Remaining", AntigravityQuotaCadence.FiveHour, 89,
                    null, TimeSpan.FromHours(5)),
                new AntigravityQuotaWindowState(
                    "gemini-weekly", "Weekly Limit Remaining", AntigravityQuotaCadence.Weekly, 88,
                    null, TimeSpan.FromDays(7)),
                [])]);
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            ConsoleUsageRenderer.WriteAntigravity(state, null, UiText.For(UiLanguage.English));
        }
        finally
        {
            Console.SetOut(original);
        }

        var rendered = output.ToString();
        Assert.Contains("Gemini Models", rendered);
        Assert.Contains("Five Hour Limit Remaining: 89%", rendered);
        Assert.Contains("Weekly Limit Remaining: 88%", rendered);
        Assert.DoesNotContain("Model-only observation", rendered);
    }
}
