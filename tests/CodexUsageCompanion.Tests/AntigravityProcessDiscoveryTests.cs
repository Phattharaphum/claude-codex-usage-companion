using System.Text;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class AntigravityProcessDiscoveryTests
{
    private const string Secret = "test-csrf-secret-do-not-log";

    [Fact]
    public void ParseCommandLineReadsNulSeparatedArguments()
    {
        var arguments = AntigravityProcessDiscovery.ParseCommandLine(
            Encoding.UTF8.GetBytes($"/opt/language_server\0--csrf_token\0{Secret}\0"));

        Assert.Equal(["/opt/language_server", "--csrf_token", Secret], arguments);
    }

    [Theory]
    [InlineData("--csrf_token", true)]
    [InlineData("--csrf_token=", false)]
    [InlineData("--csrf_token_extra", false)]
    public void TryExtractCsrfTokenRecognizesOnlyExactArgumentForms(string argument, bool expected)
    {
        var arguments = argument == "--csrf_token"
            ? new[] { "/opt/language_server", argument, Secret }
            : new[] { "/opt/language_server", argument == "--csrf_token=" ? argument : argument + "=" + Secret };

        var found = AntigravityProcessDiscovery.TryExtractCsrfToken(arguments, out var token);

        Assert.Equal(expected, found);
        Assert.Equal(expected ? Secret : string.Empty, token);
    }

    [Fact]
    public void TryExtractCsrfTokenSupportsEqualsFormAndRejectsMissingValues()
    {
        Assert.True(AntigravityProcessDiscovery.TryExtractCsrfToken(
            ["/opt/language_server", $"--csrf_token={Secret}"], out var inline));
        Assert.Equal(Secret, inline);
        Assert.False(AntigravityProcessDiscovery.TryExtractCsrfToken(
            ["/opt/language_server", "--csrf_token"], out _));
        Assert.False(AntigravityProcessDiscovery.TryExtractCsrfToken(
            ["/opt/language_server", "--csrf_token", ""], out _));
        Assert.False(AntigravityProcessDiscovery.TryExtractCsrfToken(
            ["/opt/language_server", "--csrf_token", "--another-option"], out _));
    }

    [Theory]
    [InlineData("/snap/antigravity/28/opt/antigravity/resources/bin/language_server")]
    [InlineData("/opt/antigravity/bin/language_server")]
    public void IsLanguageServerExecutableAcceptsSnapAndNonSnapPaths(string executablePath)
    {
        Assert.True(AntigravityProcessDiscovery.IsLanguageServerExecutable(executablePath));
    }

    [Fact]
    public void DiscoverCandidatesRejectsArgumentsThatOnlyMentionLanguageServer()
    {
        var candidates = AntigravityProcessDiscovery.DiscoverCandidates(
            [100],
            _ => new AntigravityProcessSnapshot(
                100,
                "/usr/bin/bash",
                ["bash", "-c", $"echo language_server --csrf_token {Secret}"]));

        Assert.Empty(candidates);
    }

    [Fact]
    public void DiscoverCandidatesReturnsEveryVerifiedLanguageServer()
    {
        var candidates = AntigravityProcessDiscovery.DiscoverCandidates(
            [200, 100],
            processId => new AntigravityProcessSnapshot(
                processId,
                "/opt/antigravity/bin/language_server",
                ["language_server", $"--csrf_token={Secret}{processId}"]));

        Assert.Equal(2, candidates.Count);
        Assert.Equal(200, candidates[0].ProcessId);
        Assert.Equal(100, candidates[1].ProcessId);
    }

    [Fact]
    public void DiscoverCandidatesSkipsProcessesThatDisappearDuringInspection()
    {
        var candidates = AntigravityProcessDiscovery.DiscoverCandidates(
            [100, 200],
            processId => processId == 100
                ? throw new DirectoryNotFoundException("process disappeared")
                : new AntigravityProcessSnapshot(
                    processId,
                    "/opt/antigravity/bin/language_server",
                    ["language_server", "--csrf_token", Secret]));

        var candidate = Assert.Single(candidates);
        Assert.Equal(200, candidate.ProcessId);
    }

    [Fact]
    public void ParseListeningPortsReadsSortedDistinctLoopbackListeners()
    {
        const string output = """
        COMMAND       PID USER   FD   TYPE DEVICE SIZE/OFF NODE NAME
        language_     71740 user  40u  IPv4  12345      0t0  TCP 127.0.0.1:41011 (LISTEN)
        language_     71740 user  41u  IPv6  12346      0t0  TCP [::1]:37017 (LISTEN)
        language_     71740 user  42u  IPv4  12347      0t0  TCP 127.0.0.1:41011 (LISTEN)
        """;

        var ports = AntigravityProcessDiscovery.ParseListeningPorts(output);

        Assert.Equal([37017, 41011], ports);
    }

    [Fact]
    public void ParseListeningPortsIgnoresMalformedAndNonLoopbackLines()
    {
        const string output = """
        Docker overlay warning written to stdout is not socket data
        language_ 7 user 40u IPv4 1 0t0 TCP 0.0.0.0:37017 (LISTEN)
        language_ 7 user 41u IPv4 1 0t0 TCP 192.168.1.10:38507 (LISTEN)
        language_ 7 user 42u IPv4 1 0t0 TCP 127.0.0.1:not-a-port (LISTEN)
        language_ 7 user 43u IPv4 1 0t0 TCP 127.0.0.1:38507
        language_ 7 user 44u IPv4 1 0t0 TCP localhost:37017 (LISTEN)
        """;

        var ports = AntigravityProcessDiscovery.ParseListeningPorts(output);

        Assert.Equal([37017], ports);
    }

    [Fact]
    public void RedactedTypesNeverExposeCsrfTokensThroughToString()
    {
        var endpoint = new AntigravityLocalEndpoint(71740, Secret, [37017, 38507]);
        var candidate = new AntigravityProcessCandidate(71740, Secret);
        var snapshot = new AntigravityProcessSnapshot(
            71740,
            "/opt/antigravity/bin/language_server",
            ["language_server", "--csrf_token", Secret]);

        Assert.DoesNotContain(Secret, endpoint.ToString());
        Assert.DoesNotContain(Secret, candidate.ToString());
        Assert.DoesNotContain(Secret, snapshot.ToString());
    }
}
