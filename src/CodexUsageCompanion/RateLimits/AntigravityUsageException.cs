namespace CodexUsageCompanion.RateLimits;

/// <summary>
/// Why the local Antigravity usage service could not be read. The kind drives
/// the user-facing message; transport details (ports, tokens, payloads) are
/// deliberately never part of it.
/// </summary>
internal enum AntigravityFailureKind
{
    NotRunning,
    NoListeningPort,
    AuthRejected,
    Timeout,
    InvalidResponse,
    Unreachable,
    MultipleAccounts,
    DiscoveryUnavailable
}

internal sealed class AntigravityUsageException : InvalidOperationException
{
    internal AntigravityUsageException(AntigravityFailureKind kind)
        : this(kind, DefaultMessage(kind))
    {
    }

    internal AntigravityUsageException(
        AntigravityFailureKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    internal AntigravityFailureKind Kind { get; }

    internal static string DefaultMessage(AntigravityFailureKind kind) => kind switch
    {
        AntigravityFailureKind.NotRunning =>
            "Antigravity is not running. Open it to refresh usage.",
        AntigravityFailureKind.NoListeningPort =>
            "Antigravity is still starting; its local service is not listening yet.",
        AntigravityFailureKind.AuthRejected =>
            "Antigravity rejected the local request. Restart Antigravity and try again.",
        AntigravityFailureKind.Timeout =>
            "Antigravity's local service did not respond in time.",
        AntigravityFailureKind.InvalidResponse =>
            "Antigravity's local service returned unexpected usage data.",
        AntigravityFailureKind.Unreachable =>
            "Could not connect to Antigravity's local service.",
        AntigravityFailureKind.MultipleAccounts =>
            "Multiple Antigravity accounts are active.",
        _ => "Antigravity local usage service was unavailable."
    };
}
