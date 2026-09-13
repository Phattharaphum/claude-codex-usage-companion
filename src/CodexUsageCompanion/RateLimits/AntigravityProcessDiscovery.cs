using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CodexUsageCompanion.RateLimits;

internal sealed class AntigravityLocalEndpoint
{
    internal AntigravityLocalEndpoint(int processId, string csrfToken, IReadOnlyList<int> ports)
    {
        ProcessId = processId;
        CsrfToken = csrfToken;
        Ports = ports;
    }

    internal int ProcessId { get; }
    internal string CsrfToken { get; }
    internal IReadOnlyList<int> Ports { get; }

    public override string ToString() =>
        $"Antigravity local endpoint (PID {ProcessId}, CSRF token present, {Ports.Count} ports)";
}

internal sealed class AntigravityProcessSnapshot(
    int processId,
    string executablePath,
    IReadOnlyList<string> commandLine)
{
    internal int ProcessId { get; } = processId;
    internal string ExecutablePath { get; } = executablePath;
    internal IReadOnlyList<string> CommandLine { get; } = commandLine;

    public override string ToString() =>
        $"Antigravity process snapshot (PID {ProcessId}, executable {Path.GetFileName(ExecutablePath)})";
}

internal sealed class AntigravityProcessCandidate(int processId, string csrfToken)
{
    internal int ProcessId { get; } = processId;
    internal string CsrfToken { get; } = csrfToken;

    public override string ToString() =>
        $"Antigravity language server candidate (PID {ProcessId}, CSRF token present)";
}

internal static class AntigravityProcessDiscovery
{
    private const string ProcDirectory = "/proc";
    private const string LanguageServerName = "language_server";

    internal static IReadOnlyList<AntigravityLocalEndpoint> Discover()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "Antigravity local process discovery is only supported on Linux.");
        }

        var currentUid = ReadUid(Path.Combine(ProcDirectory, "self", "status"));
        var candidates = DiscoverCandidates(
            EnumerateProcessIds(),
            processId => ReadProcessSnapshot(processId, currentUid));
        var endpoints = new List<AntigravityLocalEndpoint>();
        foreach (var candidate in candidates)
        {
            var ports = ReadListeningPorts(candidate.ProcessId);
            if (ports.Count > 0)
            {
                endpoints.Add(new AntigravityLocalEndpoint(
                    candidate.ProcessId,
                    candidate.CsrfToken,
                    ports));
            }
        }

        return endpoints;
    }

    internal static IReadOnlyList<AntigravityProcessCandidate> DiscoverCandidates(
        IEnumerable<int> processIds,
        Func<int, AntigravityProcessSnapshot?> readSnapshot)
    {
        var candidates = new List<AntigravityProcessCandidate>();
        foreach (var processId in processIds)
        {
            try
            {
                var snapshot = readSnapshot(processId);
                if (snapshot is null ||
                    !IsLanguageServerExecutable(snapshot.ExecutablePath) ||
                    !TryExtractCsrfToken(snapshot.CommandLine, out var csrfToken))
                {
                    continue;
                }

                candidates.Add(new AntigravityProcessCandidate(snapshot.ProcessId, csrfToken));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Processes can exit or revoke access between /proc enumeration and reading.
            }
        }

        return candidates;
    }

    internal static IReadOnlyList<string> ParseCommandLine(byte[] contents)
    {
        return Encoding.UTF8.GetString(contents)
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    internal static bool TryExtractCsrfToken(IReadOnlyList<string> arguments, out string csrfToken)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument == "--csrf_token")
            {
                if (index + 1 >= arguments.Count ||
                    string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                    arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    break;
                }

                csrfToken = arguments[index + 1];
                return true;
            }

            const string prefix = "--csrf_token=";
            if (argument.StartsWith(prefix, StringComparison.Ordinal))
            {
                var value = argument[prefix.Length..];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    csrfToken = value;
                    return true;
                }

                break;
            }
        }

        csrfToken = string.Empty;
        return false;
    }

    internal static bool IsLanguageServerExecutable(string executablePath) =>
        string.Equals(
            Path.GetFileName(executablePath),
            LanguageServerName,
            StringComparison.Ordinal);

    internal static IReadOnlyList<int> ParseListeningPorts(string standardOutput)
    {
        var ports = new SortedSet<int>();
        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("(LISTEN)", StringComparison.Ordinal) ||
                !TryParseLoopbackListeningPort(line, out var port))
            {
                continue;
            }

            ports.Add(port);
        }

        return ports.ToArray();
    }

    private static IReadOnlyList<int> ReadListeningPorts(int processId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "lsof",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-nP");
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-iTCP");
            startInfo.ArgumentList.Add("-sTCP:LISTEN");

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start lsof for Antigravity discovery.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            _ = error.GetAwaiter().GetResult();
            return ParseListeningPorts(output.GetAwaiter().GetResult());
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "The lsof utility is required for Antigravity local process discovery.",
                exception);
        }
    }

    private static bool TryParseLoopbackListeningPort(string line, out int port)
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tcpIndex = Array.FindIndex(fields, field => field == "TCP");
        if (tcpIndex < 0 || tcpIndex + 1 >= fields.Length)
        {
            port = 0;
            return false;
        }

        var address = fields[tcpIndex + 1];
        var separator = address.LastIndexOf(':');
        if (separator <= 0 ||
            !int.TryParse(address[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) ||
            port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }

        var host = address[..separator].Trim('[', ']');
        return host is "127.0.0.1" or "::1" or "localhost";
    }

    private static IReadOnlyList<int> EnumerateProcessIds()
    {
        try
        {
            return Directory.EnumerateDirectories(ProcDirectory)
                .Select(Path.GetFileName)
                .Where(name => int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                .Select(name => int.Parse(name!, CultureInfo.InvariantCulture))
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static AntigravityProcessSnapshot? ReadProcessSnapshot(int processId, int? currentUid)
    {
        var processDirectory = Path.Combine(ProcDirectory, processId.ToString(CultureInfo.InvariantCulture));
        try
        {
            var ownerUid = ReadUid(Path.Combine(processDirectory, "status"));
            if (currentUid is not null && ownerUid != currentUid)
            {
                return null;
            }

            var executable = File.ResolveLinkTarget(Path.Combine(processDirectory, "exe"), true)?.FullName;
            if (string.IsNullOrWhiteSpace(executable))
            {
                return null;
            }

            var commandLine = ParseCommandLine(File.ReadAllBytes(Path.Combine(processDirectory, "cmdline")));
            return new AntigravityProcessSnapshot(processId, executable, commandLine);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int? ReadUid(string statusPath)
    {
        try
        {
            foreach (var line in File.ReadLines(statusPath))
            {
                if (!line.StartsWith("Uid:", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = line["Uid:".Length..].TrimStart()
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var uid)
                    ? uid
                    : null;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }
}
