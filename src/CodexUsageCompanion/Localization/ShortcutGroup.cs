namespace CodexUsageCompanion.Localization;

public sealed record ShortcutHint(string Keys, string Description);

public sealed record ShortcutGroup(
    string Title,
    IReadOnlyList<ShortcutHint> Shortcuts);
