using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class AntigravityQuotaSummaryParserTests
{
    [Theory]
    [InlineData("response")]
    [InlineData("summary")]
    [InlineData("root")]
    [InlineData("quotaGroups")]
    public void ParseResponseSupportsKnownGroupEnvelopes(string envelope)
    {
        var pools = AntigravityQuotaSummaryParser.ParseResponse(Wrap(envelope, Groups()));

        Assert.Equal(2, pools.Count);
        var pool = pools[0];
        Assert.Equal("Gemini Models", pool.Name);
        Assert.Equal(89, pool.FiveHour!.RemainingPercent);
        Assert.Equal(88, pool.Weekly!.RemainingPercent);
    }

    [Fact]
    public void ParseResponsePreservesSeparateGeminiAndClaudeGptPools()
    {
        var pools = AntigravityQuotaSummaryParser.ParseResponse(Wrap("response", Groups()));

        Assert.Collection(
            pools,
            gemini =>
            {
                Assert.Equal("gemini-models", gemini.Id);
                Assert.Equal("Gemini Models", gemini.Name);
                Assert.Equal("gemini-5h", gemini.FiveHour!.Id);
                Assert.Equal("gemini-weekly", gemini.Weekly!.Id);
                Assert.Empty(gemini.ModelIds);
            },
            thirdParty =>
            {
                Assert.Equal("claude-and-gpt-models", thirdParty.Id);
                Assert.Equal("Claude and GPT models", thirdParty.Name);
                Assert.Equal(100, thirdParty.FiveHour!.RemainingPercent);
                Assert.Equal(80, thirdParty.Weekly!.RemainingPercent);
            });
    }

    [Fact]
    public void ParseResponseUsesExplicitCadenceAndKeepsResetWhenValid()
    {
        var pools = AntigravityQuotaSummaryParser.ParseResponse(Wrap("response", Groups()));
        var gemini = pools[0];

        Assert.Equal(AntigravityQuotaCadence.FiveHour, gemini.FiveHour!.Cadence);
        Assert.Equal(TimeSpan.FromHours(5), gemini.FiveHour.WindowDuration);
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 7, 44, 7, TimeSpan.Zero), gemini.FiveHour.ResetAt);
        Assert.Equal(AntigravityQuotaCadence.Weekly, gemini.Weekly!.Cadence);
        Assert.Equal(TimeSpan.FromDays(7), gemini.Weekly.WindowDuration);
    }

    [Fact]
    public void ParseResponseUsesIdentifierFallbackAndProtobufJsonRemainingVariants()
    {
        const string json = """
        {
          "groups": [
            {
              "displayName": "Gemini Models",
              "buckets": [
                { "bucketId": "gemini-5h", "remaining": { "case": "remainingFraction", "value": 0.125 } },
                { "bucketId": "gemini-weekly", "remaining": { "remainingFraction": 0.875 } }
              ]
            }
          ]
        }
        """;

        var pool = Assert.Single(AntigravityQuotaSummaryParser.ParseResponse(json));

        Assert.Equal(13, pool.FiveHour!.RemainingPercent);
        Assert.Equal(88, pool.Weekly!.RemainingPercent);
    }

    [Fact]
    public void ParseResponseSupportsClearLegacyUsedLimitBuckets()
    {
        const string json = """
        {
          "groups": [
            {
              "displayName": "Gemini Models",
              "buckets": [
                { "name": "hourly", "used": 25, "limit": 100 },
                { "name": "weekly", "used": 20, "limit": 100 }
              ]
            }
          ]
        }
        """;

        var pool = Assert.Single(AntigravityQuotaSummaryParser.ParseResponse(json));

        Assert.Equal(75, pool.FiveHour!.RemainingPercent);
        Assert.Equal(80, pool.Weekly!.RemainingPercent);
    }

    [Fact]
    public void ParseResponseKeepsBucketsWhenResetIsMissingOrMalformed()
    {
        const string json = """
        {
          "groups": [
            {
              "displayName": "Gemini Models",
              "buckets": [
                { "window": "five_hour", "remainingFraction": 0.5, "resetTime": "not-a-date" },
                { "window": "weekly", "remainingFraction": 0.75 }
              ]
            }
          ]
        }
        """;

        var pool = Assert.Single(AntigravityQuotaSummaryParser.ParseResponse(json));

        Assert.Equal(50, pool.FiveHour!.RemainingPercent);
        Assert.Null(pool.FiveHour.ResetAt);
        Assert.Equal(75, pool.Weekly!.RemainingPercent);
        Assert.Null(pool.Weekly.ResetAt);
    }

    [Fact]
    public void ParseResponseDoesNotInventAWindowForUnknownCadence()
    {
        const string json = """
        {
          "groups": [
            {
              "displayName": "Gemini Models",
              "buckets": [ { "window": "monthly", "remainingFraction": 0.5 } ]
            }
          ]
        }
        """;

        Assert.Empty(AntigravityQuotaSummaryParser.ParseResponse(json));
    }

    private static string Wrap(string envelope, string groups) => envelope switch
    {
        "response" => $$"""{ "response": { "groups": {{groups}} } }""",
        "summary" => $$"""{ "summary": { "groups": {{groups}} } }""",
        "root" => $$"""{ "groups": {{groups}} }""",
        "quotaGroups" => $$"""{ "quotaGroups": {{groups}} }""",
        _ => throw new ArgumentOutOfRangeException(nameof(envelope))
    };

    private static string Groups() => """
    [
      {
        "displayName": "Gemini Models",
        "buckets": [
          {
            "bucketId": "gemini-5h",
            "displayName": "Five Hour Limit Remaining",
            "window": "five_hour",
            "remainingFraction": 0.89,
            "resetTime": "2026-09-13T07:44:07Z"
          },
          {
            "bucketId": "gemini-weekly",
            "displayName": "Weekly Limit Remaining",
            "window": "weekly",
            "remainingFraction": 0.88
          }
        ]
      },
      {
        "displayName": "Claude and GPT models",
        "buckets": [
          { "bucketId": "3p-5h", "window": "five_hour", "remainingFraction": 1.0 },
          { "bucketId": "3p-weekly", "window": "weekly", "remainingFraction": 0.80 }
        ]
      }
    ]
    """;
}
