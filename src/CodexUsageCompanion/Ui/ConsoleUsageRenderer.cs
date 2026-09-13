using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;

namespace CodexUsageCompanion.Ui;

public static class ConsoleUsageRenderer
{
    private const string Reset = "[0m";

    public static void WriteCodex(RateLimitState? state, string? error, UiText text, bool color) =>
        Write("Codex usage", text.FiveHourTitle, text.WeeklyTitle, state, error, text, color);

    public static void WriteClaude(RateLimitState? state, string? error, UiText text, bool color) =>
        Write("Claude usage", text.ClaudeFiveHourTitle, text.ClaudeWeeklyTitle, state, error, text, color);

    public static void WriteAntigravity(AntigravityUsageState? state, string? error, UiText text)
    {
        Console.WriteLine("Antigravity usage");
        if (state is null)
        {
            Console.WriteLine(error ?? text.WaitingForData);
            return;
        }

        if (!string.IsNullOrWhiteSpace(state.Account))
        {
            Console.WriteLine($"Account: {state.Account}");
        }

        if (!string.IsNullOrWhiteSpace(state.Plan))
        {
            Console.WriteLine($"Plan: {state.Plan}");
        }

        if (state.QuotaPools.Count > 0)
        {
            foreach (var pool in state.QuotaPools)
            {
                Console.WriteLine();
                Console.WriteLine(pool.Name);
                WriteAntigravityWindow(pool.FiveHour, text);
                WriteAntigravityWindow(pool.Weekly, text);
            }

            return;
        }

        foreach (var model in state.Models)
        {
            Console.WriteLine();
            Console.WriteLine(model.Name);
            Console.WriteLine($"  Observed remaining: {model.RemainingPercent}%");
            var reset = model.ResetAt is { } resetAt
                ? text.FormatFiveHourReset(resetAt.ToLocalTime())
                : text.ResetUnavailable;
            Console.WriteLine($"  Reset: {reset}");
        }
    }

    private static void WriteAntigravityWindow(AntigravityQuotaWindowState? window, UiText text)
    {
        if (window is null)
        {
            return;
        }

        var reset = window.ResetAt is { } resetAt
            ? window.Cadence == AntigravityQuotaCadence.Weekly
                ? text.FormatWeeklyReset(resetAt.ToLocalTime())
                : text.FormatFiveHourReset(resetAt.ToLocalTime())
            : text.ResetUnavailable;
        Console.WriteLine($"  {window.Name}: {window.RemainingPercent}%  {reset}");
    }

    private static void Write(
        string label,
        string fiveHourTitle,
        string weeklyTitle,
        RateLimitState? state,
        string? error,
        UiText text,
        bool color)
    {
        Console.WriteLine(label);
        if (state is null)
        {
            Console.WriteLine(error ?? text.WaitingForData);
            return;
        }

        WriteWindow(fiveHourTitle, state.FiveHour, weekly: false, text, color);
        WriteWindow(weeklyTitle, state.Weekly, weekly: true, text, color);
        if (state.AvailableResetCredits is int credits)
        {
            Console.WriteLine($"Reset credits: {credits}");
        }
    }

    private static void WriteWindow(
        string title,
        RateLimitWindowState? state,
        bool weekly,
        UiText text,
        bool color)
    {
        if (state is null)
        {
            Console.WriteLine($"{title,-20} [-----] {text.LimitUnavailable}");
            return;
        }

        var signal = UsagePresentation.GetSignal(state.RemainingPercent);
        var bar = RenderBar(state.RemainingPercent);
        var prefix = color ? Ansi(signal) : string.Empty;
        var suffix = color ? Reset : string.Empty;
        var reset = state.ResetsAt is long unixSeconds
            ? weekly
                ? text.FormatWeeklyReset(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime())
                : text.FormatFiveHourReset(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime())
            : text.ResetUnavailable;
        Console.WriteLine($"{title,-20} [{prefix}{bar}{suffix}] {state.RemainingPercent,3}%  {reset}");
    }

    private static string RenderBar(int remainingPercent)
    {
        var filled = (int)Math.Ceiling(Math.Clamp(remainingPercent, 0, 100) / 20d);
        return new string('■', filled) + new string('·', 5 - filled);
    }

    private static string Ansi(UsageSignal signal) => signal switch
    {
        UsageSignal.Green => "[32m",
        UsageSignal.Yellow => "[33m",
        UsageSignal.Orange => "[38;5;208m",
        UsageSignal.Red => "[31m",
        _ => "[90m"
    };
}
