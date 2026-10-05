using CodexUsageCompanion.Lifecycle;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageStatusTrackerTests
{
    [Fact]
    public void SuccessfulProviderDoesNotClearAnotherProvidersFailure()
    {
        var status = new UsageStatusTracker();
        var now = DateTimeOffset.UtcNow;
        status.Update("Claude", null, "Session expired");

        status.Update("Codex", now, null);

        Assert.Equal("Claude: Session expired", status.Error);
        Assert.Equal(now, status.LastUpdatedAt);

        status.Update("Claude", now.AddSeconds(1), null);
        Assert.Null(status.Error);
    }

    [Fact]
    public void FailuresKeepLastSuccessfulTimestamp()
    {
        var status = new UsageStatusTracker();
        var now = DateTimeOffset.UtcNow;
        status.Update("Codex", now, null);

        status.Update("Codex", null, "Offline");

        Assert.Equal(now, status.LastUpdatedAt);
        Assert.Equal("Codex: Offline", status.Error);
    }

    [Fact]
    public void DisabledProviderClearsOnlyItsStatus()
    {
        var status = new UsageStatusTracker();
        var now = DateTimeOffset.UtcNow;
        status.Update("Claude", now, null);
        status.Update("Antigravity", now.AddSeconds(1), "Unavailable");
        status.Update("Codex", null, "Offline");

        status.Update("Antigravity", null, null);

        Assert.Equal(now, status.LastUpdatedAt);
        Assert.Equal("Codex: Offline", status.Error);
    }

    [Fact]
    public void MultipleErrorsHaveStableOrderAndProviderNames()
    {
        var status = new UsageStatusTracker();
        status.Update("Codex", null, "Offline");
        status.Update("Claude", null, "Session expired");
        status.Update("Antigravity", null, "Not running");

        Assert.Equal("Antigravity: Not running · Claude: Session expired · Codex: Offline", status.Error);
        Assert.Null(status.LastUpdatedAt);
    }
}
