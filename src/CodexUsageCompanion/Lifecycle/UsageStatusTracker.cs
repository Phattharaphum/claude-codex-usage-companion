namespace CodexUsageCompanion.Lifecycle;

/// <summary>Retains each provider's error until that provider succeeds or is disabled.</summary>
internal sealed class UsageStatusTracker
{
    private readonly Dictionary<string, DateTimeOffset> _updatedAt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    internal DateTimeOffset? LastUpdatedAt => _updatedAt.Count == 0 ? null : _updatedAt.Values.Max();

    internal string? Error => _errors.Count == 0
        ? null
        : string.Join(" · ", _errors.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}: {entry.Value}"));

    internal void Update(string provider, DateTimeOffset? updatedAt, string? error)
    {
        if (updatedAt is null && string.IsNullOrWhiteSpace(error))
        {
            _updatedAt.Remove(provider);
            _errors.Remove(provider);
            return;
        }

        if (updatedAt is { } timestamp)
        {
            _updatedAt[provider] = timestamp;
        }

        if (string.IsNullOrWhiteSpace(error))
        {
            _errors.Remove(provider);
        }
        else
        {
            _errors[provider] = error;
        }
    }
}
