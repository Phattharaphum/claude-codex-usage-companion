using System.Reflection;
using Avalonia.Media;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

/// <summary>
/// Guards the colour tokens against unreadable text. Small text needs a 4.5:1
/// contrast ratio (WCAG AA); large numerals and graphics need 3:1. Each window
/// kept its own palette, so each is checked against what it draws on.
/// </summary>
public sealed class ContrastTests
{
    private const double Text = 4.5;
    private const double Strong = 7.0;
    private const double Graphic = 3.0;

    public static TheoryData<string, object> InsightPalettes => new()
    {
        { "Insights light", InsightsWindow.HistoryPalette.Light },
        { "Insights dark", InsightsWindow.HistoryPalette.Dark },
        { "Analysis light", AnalysisView.AnalysisPalette.Light },
        { "Analysis dark", AnalysisView.AnalysisPalette.Dark },
        { "Efficiency light", EfficiencyView.EfficiencyPalette.Light },
        { "Efficiency dark", EfficiencyView.EfficiencyPalette.Dark },
        { "Records light", RecordsView.TimelinePalette.Light },
        { "Records dark", RecordsView.TimelinePalette.Dark },
        { "Shortcuts light", ShortcutsWindow.ShortcutsPalette.Light },
        { "Shortcuts dark", ShortcutsWindow.ShortcutsPalette.Dark }
    };

    [Theory]
    [MemberData(nameof(InsightPalettes))]
    public void WindowPalettesKeepTextReadable(string name, object palette)
    {
        var light = name.Contains("light", StringComparison.Ordinal);
        var failures = new List<string>();
        foreach (var surface in new[] { "Root", "Surface", "Soft" }.Where(surface => Has(palette, surface)))
        {
            Check(failures, name, palette, "Primary", surface, Strong);
            Check(failures, name, palette, "Secondary", surface, Text);
        }

        foreach (var surface in new[] { "Surface", "Soft", "AccentSoft" }.Where(surface => Has(palette, surface)))
        {
            Check(failures, name, palette, "Accent", surface, Text);
        }

        foreach (var surface in new[] { "Surface", "ErrorSoft" }.Where(surface => Has(palette, surface)))
        {
            Check(failures, name, palette, "Error", surface, Text);
        }

        foreach (var signal in new[] { "Green", "Yellow", "Orange", "Red" }.Where(signal => Has(palette, signal)))
        {
            Check(failures, name, palette, signal, "Surface", Graphic);
        }

        if (Has(palette, "InfoSoft"))
        {
            Check(failures, name, palette, "InfoText", "InfoSoft", Text);
            Check(failures, name, palette, "InfoTitle", "InfoSoft", Text);
        }

        if (Has(palette, "InsightSoft"))
        {
            Check(failures, name, palette, "InsightText", "InsightSoft", Text);
            Check(failures, name, palette, "InsightTitle", "InsightSoft", Text);
        }

        if (Has(palette, "WarningSoft"))
        {
            Check(failures, name, palette, "WarningText", "WarningSoft", Text);
        }

        // Provider and cadence colours are drawn as text on this palette's surface.
        var surfaceColor = Get(palette, "Surface");
        foreach (var provider in ProviderStyle.Known)
        {
            AddRatio(failures, $"{name}: provider {provider}", ProviderStyle.Accent(provider, light, "#000000"), surfaceColor, Text);
        }

        AddRatio(failures, $"{name}: 5-hour cadence", CadenceStyle.FiveHour(light), surfaceColor, Text);
        AddRatio(failures, $"{name}: weekly cadence", CadenceStyle.Weekly(light), surfaceColor, Text);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OverlayPaletteKeepsTextReadable(bool light)
    {
        // The overlay window is translucent, so its text sits on the card, which is opaque.
        var palette = light ? OverlayThemePalette.Light : OverlayThemePalette.Dark;
        var failures = new List<string>();
        foreach (var (name, foreground, minimum) in new (string, string, double)[]
                 {
                     ("CardTitle", palette.CardTitle, Strong),
                     ("SecondaryText", palette.SecondaryText, Text),
                     ("StatusText", palette.StatusText, Text),
                     ("ErrorText", palette.ErrorText, Text),
                     ("Green", palette.Green, Graphic),
                     ("Yellow", palette.Yellow, Graphic),
                     ("Orange", palette.Orange, Graphic),
                     ("Red", palette.Red, Graphic)
                 })
        {
            AddRatio(failures, $"overlay {(light ? "light" : "dark")}: {name}", foreground, palette.CardBackground, minimum);
        }

        foreach (var provider in new[] { "FiveHour", "Weekly" })
        {
            var color = provider == "FiveHour" ? CadenceStyle.FiveHour(light) : CadenceStyle.Weekly(light);
            AddRatio(failures, $"overlay {(light ? "light" : "dark")}: {provider} dot", color, palette.CardBackground, Graphic);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SettingsPaletteKeepsTextReadable(bool light)
    {
        var palette = light ? SettingsWindow.SettingsPalette.Light : SettingsWindow.SettingsPalette.Dark;
        var failures = new List<string>();
        foreach (var (surfaceName, surface) in new[]
                 {
                     ("Window", Hex(palette.Window)),
                     ("Card", Hex(palette.Card)),
                     ("Sidebar", Hex(palette.Sidebar)),
                     ("Control", Hex(palette.Control))
                 })
        {
            AddRatio(failures, $"settings {light}: primary on {surfaceName}", Hex(palette.TextPrimary), surface, Strong);
            AddRatio(failures, $"settings {light}: secondary on {surfaceName}", Hex(palette.TextSecondary), surface, Text);
            AddRatio(failures, $"settings {light}: accent on {surfaceName}", Hex(palette.Accent), surface, Text);
        }

        AddRatio(failures, $"settings {light}: accent on selected nav", Hex(palette.Accent), Hex(palette.AccentSoft), Text);
        AddRatio(
            failures,
            $"settings {light}: warning text on notice",
            Hex(palette.NoticeText),
            Composite(palette.NoticeBackground, Hex(palette.Card)),
            Text);
        // White label on the primary button.
        AddRatio(failures, $"settings {light}: OK button", "#FFFFFFFF", Hex(palette.PrimaryButton), Text);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StatusWordsStayReadableOnTheirOwnTintedPills(bool light)
    {
        var surfaces = light
            ? new[] { "#FFFFFFFF", "#FFF7F9F7", "#FFF2F5F3" }
            : new[] { "#FF202421", "#FF292E2A", "#FF151816" };
        var colors = new (string Name, string Value)[]
        {
            ("Good", StatusStyle.Good(light)),
            ("Caution", StatusStyle.Caution(light)),
            ("Warn", StatusStyle.Warn(light)),
            ("Critical", StatusStyle.Critical(light)),
            ("Neutral", StatusStyle.Neutral(light)),
            ("Info", StatusStyle.Info(light))
        };
        var failures = new List<string>();
        foreach (var (name, value) in colors)
        {
            var hex = "#FF" + value[1..];
            foreach (var surface in surfaces)
            {
                AddRatio(failures, $"{name} on {surface}", hex, surface, Text);
                // The pill fills itself with the same colour at low opacity.
                var tinted = Composite(Color.FromArgb(light ? (byte)22 : (byte)35, Color.Parse(hex).R, Color.Parse(hex).G, Color.Parse(hex).B), surface);
                AddRatio(failures, $"{name} on its tint over {surface}", hex, tinted, Text);
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheRatioMatchesKnownReferenceValues()
    {
        Assert.Equal(21, Ratio("#FF000000", "#FFFFFFFF"), 0.01);
        Assert.Equal(1, Ratio("#FF777777", "#FF777777"), 0.001);
        // #767676 on white is the classic "just passes AA" grey.
        Assert.Equal(4.54, Ratio("#FF767676", "#FFFFFFFF"), 0.02);
    }

    private static bool Has(object palette, string property) => palette.GetType().GetProperty(property) is not null;

    private static string Get(object palette, string property) =>
        (string)palette.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!.GetValue(palette)!;

    private static void Check(List<string> failures, string name, object palette, string foreground, string background, double minimum) =>
        AddRatio(failures, $"{name}: {foreground} on {background}", Get(palette, foreground), Get(palette, background), minimum);

    private static void AddRatio(List<string> failures, string label, string foreground, string background, double minimum)
    {
        var ratio = Ratio(foreground, background);
        if (ratio < minimum)
        {
            failures.Add($"{label} is {ratio:0.00}:1, needs {minimum:0.0}:1 ({foreground} on {background})");
        }
    }

    private static string Hex(IBrush brush)
    {
        var color = ((ISolidColorBrush)brush).Color;
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string Composite(Color foreground, string background)
    {
        var back = Color.Parse(background);
        var alpha = foreground.A / 255d;
        byte Mix(byte top, byte bottom) => (byte)Math.Round(top * alpha + bottom * (1 - alpha));
        return $"#FF{Mix(foreground.R, back.R):X2}{Mix(foreground.G, back.G):X2}{Mix(foreground.B, back.B):X2}";
    }

    private static double Ratio(string foreground, string background)
    {
        var back = Color.Parse(background);
        var front = Color.Parse(Composite(Color.Parse(foreground), background));
        var lighter = Math.Max(Luminance(front), Luminance(back));
        var darker = Math.Min(Luminance(front), Luminance(back));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var scaled = value / 255d;
            return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }
}
