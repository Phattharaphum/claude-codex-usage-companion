using System.Text.RegularExpressions;
using Xunit;

namespace CodexUsageCompanion.Tests;

/// <summary>
/// A source-level guard: text below 10 px is too small to read comfortably on
/// a desktop, and several windows had drifted down to 8.5-9.5 px labels.
/// </summary>
public sealed class TypographyTests
{
    private const double MinimumFontSize = 10;

    [Fact]
    public void NoInterfaceTextIsSmallerThanTenPixels()
    {
        var directory = FindUiDirectory();
        if (directory is null)
        {
            return; // Tests run from a packaged layout without the sources.
        }

        var small = new List<string>();
        var pattern = new Regex(@"FontSize\s*=\s*(\d+(?:\.\d+)?)");
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (Match match in pattern.Matches(lines[index]))
                {
                    if (double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) < MinimumFontSize)
                    {
                        small.Add($"{Path.GetFileName(file)}:{index + 1}: {lines[index].Trim()}");
                    }
                }
            }
        }

        Assert.True(small.Count == 0, string.Join(Environment.NewLine, small));
    }

    private static string? FindUiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "CodexUsageCompanion", "Ui");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
