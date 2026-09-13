using System.Globalization;
using System.Text.Json;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class AntigravityUsageParserTests
{
    [Fact]
    public void ParseResponseReadsValidSingleModel()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            {
              "label": "Gemini 3.1 Pro (Low)",
              "modelOrAlias": { "model": "MODEL_PLACEHOLDER_M36" },
              "quotaInfo": {
                "remainingFraction": 0.5373352,
                "resetTime": "2026-09-13T07:44:07Z"
              }
            }
            """));

        var model = Assert.Single(state.Models);
        Assert.Equal("sanitized@example.invalid", state.Account);
        Assert.Equal("Pro", state.Plan);
        Assert.Equal("MODEL_PLACEHOLDER_M36", model.Id);
        Assert.Equal("Gemini 3.1 Pro (Low)", model.Name);
        Assert.Equal(54, model.RemainingPercent);
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 7, 44, 7, TimeSpan.Zero), model.ResetAt);
    }

    [Fact]
    public void ParseResponsePreservesRealisticModelsWithIdenticalQuotaSeparately()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            {
              "label": "Gemini 3.1 Pro (Low)",
              "modelOrAlias": { "model": "MODEL_PLACEHOLDER_M36" },
              "quotaInfo": { "remainingFraction": 0.5373352, "resetTime": "2026-09-13T07:44:07Z" }
            },
            {
              "label": "Claude Sonnet 4.6 (Thinking)",
              "modelOrAlias": { "model": "MODEL_PLACEHOLDER_M35" },
              "quotaInfo": { "remainingFraction": 1, "resetTime": "2026-09-13T07:44:07Z" }
            },
            {
              "label": "Gemini 3.8 Flash (High)",
              "modelOrAlias": { "model": "MODEL_PLACEHOLDER_FLASH" },
              "quotaInfo": { "remainingFraction": 1, "resetTime": "2026-09-13T07:44:07Z" }
            }
            """));

        Assert.Equal(3, state.Models.Count);
        Assert.Collection(
            state.Models,
            model =>
            {
                Assert.Equal("MODEL_PLACEHOLDER_M36", model.Id);
                Assert.Equal(54, model.RemainingPercent);
            },
            model =>
            {
                Assert.Equal("MODEL_PLACEHOLDER_M35", model.Id);
                Assert.Equal("Claude Sonnet 4.6 (Thinking)", model.Name);
                Assert.Equal(100, model.RemainingPercent);
            },
            model =>
            {
                Assert.Equal("MODEL_PLACEHOLDER_FLASH", model.Id);
                Assert.Equal(100, model.RemainingPercent);
            });
    }

    [Fact]
    public void ParseResponseDoesNotSynthesizeSharedPoolsFromMatchingModelObservations()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            {
              "label": "Gemini A",
              "modelOrAlias": { "model": "gemini-a" },
              "quotaInfo": { "remainingFraction": 0.89, "resetTime": "2026-09-13T07:44:07Z" }
            },
            {
              "label": "Gemini B",
              "modelOrAlias": { "model": "gemini-b" },
              "quotaInfo": { "remainingFraction": 0.89, "resetTime": "2026-09-13T07:44:07Z" }
            }
            """));

        Assert.Equal(2, state.Models.Count);
        Assert.Empty(state.QuotaPools);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(0.124, 12)]
    [InlineData(0.125, 13)]
    [InlineData(-0.1, 0)]
    [InlineData(1.1, 100)]
    public void ParseResponseRoundsAndClampsRemainingFraction(double remainingFraction, int expectedPercent)
    {
        var fraction = remainingFraction.ToString(CultureInfo.InvariantCulture);
        var state = AntigravityUsageParser.ParseResponse(Response($$"""
            {
              "label": "Gemini",
              "modelOrAlias": { "model": "gemini" },
              "quotaInfo": { "remainingFraction": {{fraction}} }
            }
            """));

        Assert.Equal(expectedPercent, Assert.Single(state.Models).RemainingPercent);
    }

    [Fact]
    public void ParseResponseKeepsQuotaWhenResetTimeIsMalformed()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            {
              "label": "Gemini",
              "modelOrAlias": { "model": "gemini" },
              "quotaInfo": { "remainingFraction": 0.5, "resetTime": "not-a-date" }
            }
            """));

        var model = Assert.Single(state.Models);
        Assert.Equal(50, model.RemainingPercent);
        Assert.Null(model.ResetAt);
    }

    [Fact]
    public void ParseResponseAllowsMissingAccountAndPlan()
    {
        const string json = """
        {
          "userStatus": {
            "cascadeModelConfigData": {
              "clientModelConfigs": [
                {
                  "label": "Gemini",
                  "modelOrAlias": { "model": "gemini" },
                  "quotaInfo": { "remainingFraction": 0.5 }
                }
              ]
            }
          }
        }
        """;

        var state = AntigravityUsageParser.ParseResponse(json);

        Assert.Null(state.Account);
        Assert.Null(state.Plan);
        Assert.Single(state.Models);
    }

    [Fact]
    public void ParseResponseAllowsMissingPlanWithAnAccount()
    {
        const string json = """
        {
          "userStatus": {
            "email": "sanitized@example.invalid",
            "cascadeModelConfigData": {
              "clientModelConfigs": [
                {
                  "label": "Gemini",
                  "modelOrAlias": { "model": "gemini" },
                  "quotaInfo": { "remainingFraction": 0.5 }
                }
              ]
            }
          }
        }
        """;

        var state = AntigravityUsageParser.ParseResponse(json);

        Assert.Equal("sanitized@example.invalid", state.Account);
        Assert.Null(state.Plan);
        Assert.Single(state.Models);
    }

    [Fact]
    public void ParseResponseOmitsModelsWithoutUsableQuota()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            { "label": "No quota", "modelOrAlias": { "model": "none" } },
            { "label": "No fraction", "modelOrAlias": { "model": "missing" }, "quotaInfo": {} },
            "malformed model",
            { "label": "Valid", "modelOrAlias": { "model": "valid" }, "quotaInfo": { "remainingFraction": 0.5 } }
            """));

        var model = Assert.Single(state.Models);
        Assert.Equal("valid", model.Id);
    }

    [Fact]
    public void ParseResponseUsesLabelAsFinalIdentifierFallback()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            { "label": "Named Model", "quotaInfo": { "remainingFraction": 0.5 } }
            """));

        var model = Assert.Single(state.Models);
        Assert.Equal("Named Model", model.Id);
        Assert.Equal("Named Model", model.Name);
    }

    [Fact]
    public void ParseResponseUsesModelAsNameWhenLabelIsMissing()
    {
        var state = AntigravityUsageParser.ParseResponse(Response("""
            { "modelOrAlias": { "model": "MODEL_PLACEHOLDER_M36" }, "quotaInfo": { "remainingFraction": 0.5 } }
            """));

        var model = Assert.Single(state.Models);
        Assert.Equal("MODEL_PLACEHOLDER_M36", model.Id);
        Assert.Equal("MODEL_PLACEHOLDER_M36", model.Name);
    }

    [Fact]
    public void ParseResponseReturnsNoModelsForUnexpectedRootOrNoUsableModels()
    {
        var noStatus = AntigravityUsageParser.ParseResponse("""{ "unexpected": true }""");
        var noQuota = AntigravityUsageParser.ParseResponse(Response("""
            { "label": "No quota", "quotaInfo": {} }
            """));

        Assert.Empty(noStatus.Models);
        Assert.Empty(noQuota.Models);
    }

    [Fact]
    public void ParseResponseRejectsMalformedJsonAndNonObjectRoots()
    {
        Assert.ThrowsAny<JsonException>(() => AntigravityUsageParser.ParseResponse("{"));
        Assert.Throws<JsonException>(() => AntigravityUsageParser.ParseResponse("[]"));
    }

    private static string Response(string models) => $$"""
    {
      "userStatus": {
        "email": "sanitized@example.invalid",
        "planStatus": { "planInfo": { "planName": "Pro" } },
        "cascadeModelConfigData": {
          "clientModelConfigs": [{{models}}]
        }
      }
    }
    """;
}
