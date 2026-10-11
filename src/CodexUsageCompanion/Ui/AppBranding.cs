using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CodexUsageCompanion.Ui;

public static class AppBranding
{
    public const string ApplicationId = "claude-codex-usage-companion";
    public const string ProductName = "Claude Codex Usage Companion";

    // Where "About" sends people. These point at this repository and at the
    // project the Linux port was derived from.
    public const string ProjectUrl = "https://github.com/Phattharaphum/claude-codex-usage-companion";
    public const string IssuesUrl = ProjectUrl + "/issues";
    public const string ChangelogUrl = ProjectUrl + "/blob/main/CHANGELOG.md";
    public const string LicenseUrl = ProjectUrl + "/blob/main/LICENSE";
    public const string OriginalProjectUrl = "https://github.com/gkfriend/codex-usage-companion";
    private static readonly Uri LogoUri = new(
        "avares://CodexUsageCompanion/Assets/claude-codex-usage-companion.png");
    private static Bitmap? _logo;

    public static WindowIcon CreateWindowIcon()
    {
        using var stream = AssetLoader.Open(LogoUri);
        return new WindowIcon(stream);
    }

    public static Image CreateLogoImage() => new()
    {
        Source = _logo ??= LoadLogo(),
        Width = 22,
        Height = 22,
        Stretch = Stretch.Uniform
    };

    private static Bitmap LoadLogo()
    {
        using var stream = AssetLoader.Open(LogoUri);
        return new Bitmap(stream);
    }
}
