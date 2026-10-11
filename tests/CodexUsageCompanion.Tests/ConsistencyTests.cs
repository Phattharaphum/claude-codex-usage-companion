using CodexUsageCompanion.Localization;
using CodexUsageCompanion.RateLimits;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ConsistencyTests
{
    private static readonly DateTimeOffset Sunday = new(2026, 10, 11, 16, 2, 0, TimeSpan.FromHours(7));

    [Theory]
    [InlineData(UiLanguage.English, "Oct 11, 2026")]
    [InlineData(UiLanguage.TraditionalChinese, "2026年10月11日")]
    [InlineData(UiLanguage.SimplifiedChinese, "2026年10月11日")]
    public void DatesAreWrittenInTheInterfaceLanguage(UiLanguage language, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatDate(Sunday));
    }

    [Theory]
    [InlineData(UiLanguage.English, "Sunday, Oct 11, 2026")]
    [InlineData(UiLanguage.TraditionalChinese, "2026年10月11日 星期日")]
    [InlineData(UiLanguage.SimplifiedChinese, "2026年10月11日 星期日")]
    public void WeekdayDatesPutTheWeekdayWhereTheLanguageExpectsIt(UiLanguage language, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatWeekdayDate(Sunday));
    }

    [Theory]
    [InlineData(UiLanguage.English, "Sun, Oct 11 · 16:02")]
    [InlineData(UiLanguage.TraditionalChinese, "10月11日 週日 16:02")]
    [InlineData(UiLanguage.SimplifiedChinese, "10月11日 周日 16:02")]
    public void WeekdayDateTimesAreCompact(UiLanguage language, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatWeekdayDateTime(Sunday));
    }

    [Theory]
    [InlineData(UiLanguage.English, "Oct 11 · 16:02")]
    [InlineData(UiLanguage.TraditionalChinese, "10月11日 16:02")]
    public void ShortDateTimesDropTheYear(UiLanguage language, string expected)
    {
        Assert.Equal(expected, UiText.For(language).FormatDateTimeShort(Sunday));
    }

    [Fact]
    public void EveryWeekdayHasAFullAndAnAbbreviatedNameInEveryLanguage()
    {
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            var text = UiText.For(language);
            var full = Enum.GetValues<DayOfWeek>().Select(day => text.FormatWeekday(day, abbreviated: false)).ToArray();
            var shortNames = Enum.GetValues<DayOfWeek>().Select(day => text.FormatWeekday(day, abbreviated: true)).ToArray();

            Assert.Equal(7, full.Distinct().Count());
            Assert.Equal(7, shortNames.Distinct().Count());
        }
    }

    [Fact]
    public void ASinglePeriodDayIsAWeekdayDateAndASpanIsStartToEnd()
    {
        var text = UiText.For(UiLanguage.English);

        Assert.Equal("Sunday, Oct 11, 2026", text.FormatPeriod(Sunday.Date, Sunday.Date.AddHours(23)));
        Assert.Equal(
            "Oct 5, 2026 — Oct 11, 2026",
            UiText.For(UiLanguage.English).FormatPeriod(Sunday.AddDays(-6), Sunday));
    }

    [Fact]
    public void EveryAntigravityFailureHasATranslation()
    {
        foreach (var kind in Enum.GetValues<AntigravityFailureKind>())
        {
            var english = new AntigravityUsageException(kind).Message;

            foreach (var language in new[] { UiLanguage.TraditionalChinese, UiLanguage.SimplifiedChinese })
            {
                var translated = UiText.For(language).LocalizeError(english);

                Assert.NotEqual(english, translated);
                Assert.False(string.IsNullOrWhiteSpace(translated));
            }
        }
    }

    [Fact]
    public void EnglishErrorsAndUnknownMessagesPassThroughUntouched()
    {
        Assert.Equal(
            "Antigravity is not running. Open it to refresh usage.",
            UiText.For(UiLanguage.English).LocalizeError("Antigravity is not running. Open it to refresh usage."));
        Assert.Equal(
            "Some server said no (HTTP 418)",
            UiText.For(UiLanguage.TraditionalChinese).LocalizeError("Some server said no (HTTP 418)"));
        Assert.Null(UiText.For(UiLanguage.TraditionalChinese).LocalizeError(null));
        Assert.Equal(string.Empty, UiText.For(UiLanguage.TraditionalChinese).LocalizeError(string.Empty));
    }

    [Fact]
    public void ErrorsEmbeddedInACombinedStatusAreTranslatedInPlace()
    {
        var combined = "Antigravity: Antigravity is not running. Open it to refresh usage. · Claude: Claude usage is temporarily unavailable.";

        var translated = UiText.For(UiLanguage.TraditionalChinese).LocalizeError(combined);

        Assert.StartsWith("Antigravity: Antigravity 未執行", translated);
        Assert.Contains("Claude: Claude 用量暫時無法取得。", translated);
    }

    [Theory]
    [InlineData("Codex app-server request 2 timed out after 8 seconds.", "請求 2 在 8 秒後逾時")]
    [InlineData("The request was canceled due to the configured HttpClient.Timeout of 10 seconds elapsing.", "（10 秒）")]
    public void TimeoutMessagesKeepTheirNumbers(string english, string expectedFragment)
    {
        Assert.Contains(expectedFragment, UiText.For(UiLanguage.TraditionalChinese).LocalizeError(english));
    }

    [Fact]
    public void ProviderNamesAreTheSameEverywhere()
    {
        Assert.Equal("Claude", ProviderStyle.DisplayName("claude"));
        Assert.Equal("Codex", ProviderStyle.DisplayName("CODEX"));
        Assert.Equal("Antigravity · Gemini", ProviderStyle.DisplayName("antigravity-gemini"));
        Assert.Equal("Antigravity · Claude & GPT", ProviderStyle.DisplayName("antigravity-claudeandchatgpt"));
        Assert.Equal("something-else", ProviderStyle.DisplayName("something-else"));
    }

    [Theory]
    [InlineData("Gemini Models", "Gemini")]
    [InlineData("Claude and GPT models", "Claude & GPT")]
    [InlineData("Some New Pool", "Some New Pool")]
    public void QuotaPoolNamesMatchTheProviderNames(string pool, string expected)
    {
        Assert.Equal(expected, ProviderStyle.PoolName(pool));
    }

    [Fact]
    public void ProvidersKeepAFixedOrderAndUnknownOnesSortLast()
    {
        var ordered = new[] { "codex", "mystery", "antigravity-claudeandchatgpt", "claude", "antigravity-gemini" }
            .OrderBy(ProviderStyle.Order)
            .ToArray();

        Assert.Equal(
            ["claude", "codex", "antigravity-gemini", "antigravity-claudeandchatgpt", "mystery"],
            ordered);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryProviderAndBothCadencesHaveTheirOwnColour(bool light)
    {
        var colors = ProviderStyle.Known
            .Select(provider => ProviderStyle.Accent(provider, light, "#000000"))
            .Concat([CadenceStyle.FiveHour(light), CadenceStyle.Weekly(light)])
            .ToArray();

        Assert.Equal(colors.Length, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(ProviderStyle.Known, provider => Assert.NotEmpty(ProviderStyle.IconGeometry(provider)));
    }

    [Fact]
    public void TheQuotaWindowsAreCalledTheSameThingInEveryTable()
    {
        foreach (var language in Enum.GetValues<UiLanguage>())
        {
            var text = UiText.For(language);

            Assert.Equal(text.QuotaFiveHourLabel, text.UsageHistoryFiveHour);
            Assert.Equal(text.QuotaFiveHourLabel, text.UsageHistoryFiveHourWindow);
            Assert.Equal(text.QuotaWeeklyLabel, text.UsageHistoryWeek);
            Assert.Equal(text.QuotaWeeklyLabel, text.UsageHistoryWeeklyWindow);
        }
    }

    [Theory]
    [InlineData(UiLanguage.English, 6, 17, 0, "6d 17h")]
    [InlineData(UiLanguage.TraditionalChinese, 6, 17, 0, "6天 17小時")]
    [InlineData(UiLanguage.SimplifiedChinese, 6, 17, 0, "6天 17小时")]
    [InlineData(UiLanguage.English, 0, 5, 34, "5h 34m")]
    [InlineData(UiLanguage.TraditionalChinese, 0, 5, 34, "5小時 34分")]
    [InlineData(UiLanguage.English, 0, 0, 7, "7m")]
    [InlineData(UiLanguage.SimplifiedChinese, 0, 0, 7, "7分")]
    public void DurationsAreWrittenInTheInterfaceLanguage(
        UiLanguage language, int days, int hours, int minutes, string expected)
    {
        var duration = new TimeSpan(days, hours, minutes, 0);

        Assert.Equal(expected, UiText.For(language).FormatDuration(duration));
    }
}
