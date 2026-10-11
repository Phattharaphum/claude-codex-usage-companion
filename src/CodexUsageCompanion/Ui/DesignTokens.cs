using Avalonia.Media;

namespace CodexUsageCompanion.Ui;

/// <summary>
/// The names, colours and icons that identify a provider. Every window used to
/// carry its own copy, and the copies had drifted ("Claude + ChatGPT" in one
/// window, "C+GPT" in another, three different Gemini blues).
/// </summary>
internal static class ProviderStyle
{
    public const string Claude = "claude";
    public const string Codex = "codex";
    public const string Gemini = "antigravity-gemini";
    public const string ClaudeAndGpt = "antigravity-claudeandchatgpt";

    public static IReadOnlyList<string> Known { get; } = [Claude, Codex, Gemini, ClaudeAndGpt];

    public static bool IsKnown(string provider) =>
        Known.Contains(provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>Position in the fixed provider order (unknown providers sort last).</summary>
    public static int Order(string provider)
    {
        for (var index = 0; index < Known.Count; index++)
        {
            if (string.Equals(Known[index], provider, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return Known.Count;
    }

    public static string DisplayName(string provider) => provider.ToLowerInvariant() switch
    {
        Claude => "Claude",
        Codex => "Codex",
        Gemini => "Antigravity · Gemini",
        ClaudeAndGpt => "Antigravity · Claude & GPT",
        _ => provider
    };

    /// <summary>
    /// The shared quota pool as the overlay labels it. The service names the
    /// pools "Gemini Models" and "Claude and GPT models".
    /// </summary>
    public static string PoolName(string poolName)
    {
        if (poolName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        return poolName.Contains("claude", StringComparison.OrdinalIgnoreCase) &&
               poolName.Contains("gpt", StringComparison.OrdinalIgnoreCase)
            ? "Claude & GPT"
            : poolName;
    }

    public static string IconGeometry(string provider) => provider.ToLowerInvariant() switch
    {
        Claude => "M12 2L13.8 8.7L20.5 7L16.2 12L21.8 15.2L15.3 16.5L16.8 23L12 18.5L7.2 23L8.7 16.5L2.2 15.2L7.8 12L3.5 7L10.2 8.7L12 2Z",
        Codex => "M12 2C6.48 2 2 6.48 2 12S6.48 22 12 22 22 17.52 22 12 17.52 2 12 2ZM12 4C16.42 4 20 7.58 20 12C20 13.91 19.33 15.66 18.21 17.03L6.97 5.79C8.34 4.67 10.09 4 12 4ZM4 12C4 10.09 4.67 8.34 5.79 6.97L17.03 18.21C15.66 19.33 13.91 20 12 20C7.58 20 4 16.42 4 12Z",
        Gemini => "M12 2L13.7 7.3L19 9L13.7 10.7L12 16L10.3 10.7L5 9L10.3 7.3ZM19 15L20 18L23 19L20 20L19 23L18 20L15 19L18 18Z",
        ClaudeAndGpt => "M12 2L14.5 8.5L21 9.5L16 14L17.5 20.5L12 17L6.5 20.5L8 14L3 9.5L9.5 8.5L12 2Z",
        _ => "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8z"
    };

    /// <summary>The provider's colour, readable as text on the theme's surfaces.</summary>
    public static string Accent(string provider, bool isLight, string fallback) =>
        provider.ToLowerInvariant() switch
        {
            Claude => isLight ? "#B9502F" : "#F08A66",
            Codex => isLight ? "#0B7A5A" : "#4AD894",
            Gemini => isLight ? "#4357D1" : "#8794FF",
            ClaudeAndGpt => isLight ? "#7A45B8" : "#C38AF0",
            _ => fallback
        };

    public static IBrush AccentBrush(string provider, bool isLight, string fallback) =>
        new SolidColorBrush(Color.Parse(Accent(provider, isLight, fallback)));
}

/// <summary>
/// The two quota windows get one colour each, everywhere: an amber for the
/// 5-hour window and a blue for the weekly one. They deliberately avoid the
/// green-to-red scale, which always means "how much is left".
/// </summary>
internal static class CadenceStyle
{
    public static string FiveHour(bool isLight) => isLight ? "#B45309" : "#FFB45E";

    public static string Weekly(bool isLight) => isLight ? "#0369A1" : "#7DD3FC";

    public static string FiveHourArgb(bool isLight) => "#FF" + FiveHour(isLight)[1..];

    public static string WeeklyArgb(bool isLight) => "#FF" + Weekly(isLight)[1..];
}

/// <summary>
/// Colours for status wording ("Light", "High", "Safe", "Urgent") drawn as small
/// text on a tinted pill. Each was chosen to keep 4.5:1 contrast on the surface
/// and on its own tint, which the brighter amber and red used before did not.
/// </summary>
internal static class StatusStyle
{
    public static string Good(bool isLight) => isLight ? "#0A6E51" : "#4AD894";

    public static string Caution(bool isLight) => isLight ? "#994708" : "#FBBF24";

    public static string Warn(bool isLight) => isLight ? "#AF3A0B" : "#FB923C";

    public static string Critical(bool isLight) => isLight ? "#B22424" : "#FC8F8F";

    public static string Neutral(bool isLight) => isLight ? "#56616E" : "#A3B1C4";

    public static string Info(bool isLight) => isLight ? "#3F5FA8" : "#83A9F2";
}

/// <summary>
/// A tooltip is invisible to a screen reader, and a button that shows only an
/// icon has no name at all. Setting both together keeps them from drifting.
/// </summary>
internal static class A11y
{
    /// <summary>Sets the tooltip and the matching accessible name or description.</summary>
    public static void Tip(Avalonia.Controls.Control control, object? tip)
    {
        Avalonia.Controls.ToolTip.SetTip(control, tip);
        if (tip is not string text || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // A control you operate is named by its tooltip; a card or a chart is
        // already named by its content, so the tooltip is its description.
        if (control is Avalonia.Controls.Button or Avalonia.Controls.Primitives.ToggleButton)
        {
            Avalonia.Automation.AutomationProperties.SetName(control, text);
        }
        else
        {
            Avalonia.Automation.AutomationProperties.SetHelpText(control, text);
        }
    }

    public static void Name(Avalonia.Controls.Control control, string name) =>
        Avalonia.Automation.AutomationProperties.SetName(control, name);
}
