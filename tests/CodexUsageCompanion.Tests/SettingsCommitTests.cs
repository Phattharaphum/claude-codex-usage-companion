using CodexUsageCompanion.Configuration;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class SettingsCommitTests
{
    [Fact]
    public void FailedAutostartDoesNotOverwriteSavedSettings()
    {
        var saved = false;
        Assert.Throws<IOException>(() => App.CommitSettings(
            new CompanionSettings { StartOnBoot = true }, new CompanionSettings(),
            settings => { saved = true; return settings; },
            _ => throw new IOException("Cannot write autostart entry.")));

        Assert.False(saved);
    }

    [Fact]
    public void FailedSaveRestoresPreviousAutostartChoice()
    {
        var autostartEnabled = false;
        Assert.Throws<IOException>(() => App.CommitSettings(
            new CompanionSettings { StartOnBoot = true }, new CompanionSettings(),
            _ => throw new IOException("Settings disk is full."),
            enabled => autostartEnabled = enabled));

        Assert.False(autostartEnabled);
    }

    [Fact]
    public void SuccessfulCommitReturnsNormalizedSavedSettings()
    {
        var autostartEnabled = false;
        var applied = App.CommitSettings(
            new CompanionSettings { StartOnBoot = true, Margin = 1000 },
            new CompanionSettings(), CompanionSettingsStore.Normalize,
            enabled => autostartEnabled = enabled);

        Assert.True(autostartEnabled);
        Assert.True(applied.StartOnBoot);
        Assert.Equal(64, applied.Margin);
    }
}
