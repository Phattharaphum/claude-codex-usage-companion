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

    [Theory]
    [InlineData("socket:[12345]", 12345L)]
    [InlineData("socket:[0]", 0L)]
    [InlineData("pipe:[12345]", null)]
    [InlineData("anon_inode:[eventpoll]", null)]
    [InlineData("socket:[abc]", null)]
    [InlineData("socket:[]", null)]
    [InlineData("/dev/null", null)]
    [InlineData(null, null)]
    public void ParseSocketInodeOnlyAcceptsSocketLinks(string? linkTarget, long? expected)
    {
        Assert.Equal(expected, AntigravityProcessDiscovery.ParseSocketInode(linkTarget));
    }

    [Fact]
    public void ParseProcNetListeningPortsReadsOwnedLoopbackListeners()
    {
        const string tcp = """
          sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
           0: 0100007F:A0B3 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 111 1 0000000000000000 100 0 0 10 0
           1: 0100007F:9049 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 111 1 0000000000000000 100 0 0 10 0
           2: 0100007F:A0B3 0100007F:D2F0 01 00000000:00000000 00:00000000 00000000  1000        0 111 1 0000000000000000 100 0 0 10 0
           3: 00000000:1F90 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 111 1 0000000000000000 100 0 0 10 0
           4: 0100007F:2328 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 999 1 0000000000000000 100 0 0 10 0
        """;

        var ports = AntigravityProcessDiscovery.ParseProcNetListeningPorts(tcp, new HashSet<long> { 111 });

        // 0xA0B3 and 0x9049; the established socket, the wildcard listener and
        // the listener owned by another process are all ignored.
        Assert.Equal([0x9049, 0xA0B3], ports);
    }

    [Fact]
    public void ParseProcNetListeningPortsReadsIpv6AndMappedLoopback()
    {
        const string tcp6 = """
          sl  local_address                         remote_address                        st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode
           0: 00000000000000000000000001000000:9089 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 222 1 0000000000000000 100 0 0 10 0
           1: 0000000000000000FFFF00000100007F:9A2B 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 222 1 0000000000000000 100 0 0 10 0
           2: 00000000000000000000000000000000:1F90 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 222 1 0000000000000000 100 0 0 10 0
        """;

        var ports = AntigravityProcessDiscovery.ParseProcNetListeningPorts(tcp6, new HashSet<long> { 222 });

        Assert.Equal([0x9089, 0x9A2B], ports);
    }

    [Fact]
    public void ParseProcNetListeningPortsIgnoresMalformedRows()
    {
        const string tcp = """
        header only
           0: garbage
           1: 0100007F:ZZZZ 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000 0 111 1
           2: 0100007F:0000 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000 0 111 1
           3: 0100007F:A0B3 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000 0 notnum 1
        """;

        Assert.Empty(AntigravityProcessDiscovery.ParseProcNetListeningPorts(tcp, new HashSet<long> { 111 }));
    }

    [Fact]
    public void ReadListeningPortsFromProcFindsARealLoopbackListenerOfThisProcess()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var ipv4 = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        using var wildcard = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 0);
        ipv4.Start();
        wildcard.Start();
        var loopbackPort = ((System.Net.IPEndPoint)ipv4.LocalEndpoint).Port;
        var wildcardPort = ((System.Net.IPEndPoint)wildcard.LocalEndpoint).Port;

        var ports = AntigravityProcessDiscovery.ReadListeningPortsFromProc(Environment.ProcessId);

        Assert.NotNull(ports);
        Assert.Contains(loopbackPort, ports);
        Assert.DoesNotContain(wildcardPort, ports);
    }

    [Fact]
    public void ReadListeningPortsFromProcReturnsNullWhenTheProcessIsGone()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Assert.Null(AntigravityProcessDiscovery.ReadListeningPortsFromProc(int.MaxValue));
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
