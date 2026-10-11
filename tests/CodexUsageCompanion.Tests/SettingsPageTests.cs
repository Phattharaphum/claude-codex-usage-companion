using CodexUsageCompanion.Configuration;
using CodexUsageCompanion.Localization;
using CodexUsageCompanion.Platform;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class SettingsPageTests
{
    [Fact]
    public void DiagnosticsSummarizeVersionPlatformAndSettingsWithoutSecrets()
    {
        var settings = new CompanionSettings
        {
            EnableAntigravityUsage = true,
            RefreshIntervalSeconds = 120,
            UsageLogRetentionDays = 30,
            Opacity = 0.85
        };

        var report = SettingsDiagnostics.Build(
            settings,
            "1.2.3",
            3 * 1_048_576L,
            name => name switch
            {
                "XDG_SESSION_TYPE" => "wayland",
                "XDG_CURRENT_DESKTOP" => "GNOME",
                _ => null
            });

        Assert.Contains("Claude Codex Usage Companion 1.2.3", report);
        Assert.Contains("Session: wayland / GNOME", report);
        Assert.Contains("Antigravity on", report);
        Assert.Contains("Refresh interval: 120 s", report);
        Assert.Contains("opacity: 85%", report);
        Assert.Contains("retention 30 days", report);
        Assert.Contains("3.0 MB", report);
    }

    [Fact]
    public void DiagnosticsNeverIncludeCredentialsOrUsageData()
    {
        var report = SettingsDiagnostics.Build(new CompanionSettings(), "1.0.0", null, _ => null);

        Assert.DoesNotContain("token", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", report);
        Assert.Contains("Session: unknown / unknown", report);
    }

    [Fact]
    public void DiagnosticsDescribeUnlimitedRetention()
    {
        var report = SettingsDiagnostics.Build(
            new CompanionSettings { UsageLogRetentionDays = 0 }, "1.0.0", null, _ => null);

        Assert.Contains("retention forever", report);
        Assert.DoesNotContain(" MB", report);
    }

    [Fact]
    public void HistoryFileInfoCountsCsvRecordsWithoutTheHeader()
    {
        WithFile("usage.csv", "updated_at,provider\n2026-10-01,codex\n2026-10-02,claude\n2026-10-03,codex\n", path =>
        {
            var info = SettingsWindow.ReadHistoryFileInfo(path);

            Assert.NotNull(info);
            Assert.Equal(3, info!.Value.Records);
            Assert.True(info.Value.Bytes > 0);
        });
    }

    [Fact]
    public void HistoryFileInfoCountsEveryLineOfNonCsvLogs()
    {
        WithFile("usage.jsonl", "{\"a\":1}\n{\"a\":2}\n", path =>
        {
            Assert.Equal(2, SettingsWindow.ReadHistoryFileInfo(path)!.Value.Records);
        });
    }

    [Fact]
    public void HistoryFileInfoIsNullForAMissingFile()
    {
        Assert.Null(SettingsWindow.ReadHistoryFileInfo(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.csv")));
    }

    [Theory]
    [InlineData("/usr/bin:/opt/bin", "/opt/bin/notify-send", true)]
    [InlineData("/usr/bin:/opt/bin", "/somewhere/else/notify-send", false)]
    [InlineData("", "/usr/bin/notify-send", false)]
    [InlineData(null, "/usr/bin/notify-send", false)]
    public void NotifySendIsFoundOnPath(string? path, string existing, bool expected)
    {
        Assert.Equal(
            expected,
            LinuxDesktopNotificationService.IsAvailable(path, candidate => candidate == existing));
    }

    [Theory]
    [InlineData(UiLanguage.English, WindowPosition.RightBottom, "Bottom right")]
    [InlineData(UiLanguage.English, WindowPosition.MiddleCenter, "Center")]
    [InlineData(UiLanguage.TraditionalChinese, WindowPosition.LeftTop, "左上")]
    [InlineData(UiLanguage.TraditionalChinese, WindowPosition.MiddleBottom, "下方置中")]
    [InlineData(UiLanguage.SimplifiedChinese, WindowPosition.RightCenter, "右侧居中")]
    public void WindowPositionsHaveReadableNames(UiLanguage language, string position, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatWindowPosition(position));
    }

    [Fact]
    public void EveryWindowPositionHasADistinctNameInEveryLanguage()
    {
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            var names = WindowPosition.Values
                .Select(position => UiText.For(language).FormatWindowPosition(position))
                .ToArray();

            Assert.Equal(WindowPosition.Values.Count, names.Distinct().Count());
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        }
    }

    [Theory]
    [InlineData(UiLanguage.English, 512L, 3, "512 B · 3 records")]
    [InlineData(UiLanguage.English, 2048L, 1200, "2 KB · 1,200 records")]
    [InlineData(UiLanguage.English, 12_582_912L, 58818, "12.0 MB · 58,818 records")]
    [InlineData(UiLanguage.TraditionalChinese, 12_582_912L, 58818, "12.0 MB · 58,818 筆記錄")]
    [InlineData(UiLanguage.SimplifiedChinese, 12_582_912L, 58818, "12.0 MB · 58,818 条记录")]
    public void HistoryFileInfoIsFormatted(UiLanguage language, long bytes, int records, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatHistoryFileInfo(bytes, records));
    }

    [Fact]
    public void ClearHistoryConfirmationNamesTheFileAndItsSize()
    {
        var message = UiText.For(UiLanguage.English)
            .FormatClearHistoryConfirm("/tmp/usage.csv", "12.0 MB · 3 records");

        Assert.Contains("/tmp/usage.csv", message);
        Assert.Contains("12.0 MB", message);
        Assert.Contains("permanently", message);
    }

    [Fact]
    public void DatePreviewsUseTheCandidateFormat()
    {
        var moment = new DateTimeOffset(2026, 10, 11, 16, 2, 0, TimeSpan.Zero);
        var text = UiText.For(UiLanguage.English);

        Assert.Equal("Oct 11", text.PreviewDateTime(DateTimeFormatOptions.MonthDay, moment));
        Assert.Equal("Oct 11 16:02", text.PreviewDateTime(DateTimeFormatOptions.MonthDayTime, moment));
        Assert.Equal("2026-10-11", text.PreviewDateTime(DateTimeFormatOptions.YearMonthDay, moment));
        Assert.Equal("16:02", text.PreviewDateTime(DateTimeFormatOptions.HourMinute, moment));
    }

    [Fact]
    public void EverySettingsStringIsTranslated()
    {
        var names = new[]
        {
            "TrayTaskbarGroup", "TrayTaskbarGroupDescription", "StartupBehaviorGroup", "StartupBehaviorGroupDescription",
            "ProviderTagClaude", "ProviderTagCodex", "ProviderTagAntigravity", "RefreshGroup",
            "LanguageThemeGroup", "LanguageThemeGroupDescription", "OverlayPlacementGroup", "OverlayPlacementGroupDescription",
            "PositionHint", "OpacityOption", "OpacityDescription", "MarginOption", "MarginDescription",
            "NotificationAlertsGroup", "NotificationAlertsGroupDescription", "TestNotificationOption",
            "TestNotificationDescription", "TestNotificationAction", "TestNotificationTitle", "TestNotificationBody",
            "TestNotificationSentNotice", "NotifySendMissingNotice", "ResetFormatGroup", "ResetFormatGroupDescription",
            "LastUpdatedFormatGroup", "LastUpdatedFormatGroupDescription", "FormatPreviewLabel",
            "HistoryFileGroup", "HistoryFileGroupDescription", "OpenFolderAction", "ClearHistoryAction",
            "ClearHistoryConfirmTitle", "ClearHistoryConfirmAction", "NoHistoryFileYet", "AboutDescription",
            "ProjectPageAction", "ReportIssueAction", "LicenseAction", "ChangelogAction", "OriginalProjectAction",
            "CopyDiagnosticsAction", "CopiedNotice", "DiagnosticsDescription", "DiagnosticsGroup",
            "NoSettingsMatch", "SearchResultsHeading"
        };
        var english = UiText.For(UiLanguage.English);
        var traditional = UiText.For(UiLanguage.TraditionalChinese);
        var simplified = UiText.For(UiLanguage.SimplifiedChinese);

        foreach (var name in names)
        {
            var property = typeof(UiText).GetProperty(name)
                ?? throw new InvalidOperationException($"UiText.{name} does not exist.");
            var en = (string)property.GetValue(english)!;
            var tw = (string)property.GetValue(traditional)!;
            var cn = (string)property.GetValue(simplified)!;

            Assert.False(string.IsNullOrWhiteSpace(en), name);
            Assert.False(string.IsNullOrWhiteSpace(tw), name);
            Assert.False(string.IsNullOrWhiteSpace(cn), name);
            // A product name or a bare term can legitimately match; everything else must differ.
            if (name is not ("ProviderTagCodex" or "ProviderTagClaude"))
            {
                Assert.NotEqual(en, tw);
                Assert.NotEqual(en, cn);
            }
        }
    }

    private static void WithFile(string name, string contents, Action<string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CodexUsageCompanion.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, name);
            File.WriteAllText(path, contents);
            test(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
