namespace CodexUsageCompanion.Localization;

public sealed record ShortcutHint(string Keys, string Description)
{
    /// <summary>The individual keys of a combination ("Ctrl+R" is Ctrl and R), for drawing keycaps.</summary>
    public IReadOnlyList<string> KeyParts => Keys.Split('+', StringSplitOptions.RemoveEmptyEntries);
}

public sealed record ShortcutGroup(
    string Title,
    IReadOnlyList<ShortcutHint> Shortcuts);
