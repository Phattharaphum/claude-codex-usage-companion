using CodexUsageCompanion.Lifecycle;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class UsageProviderActivationTests
{
    [Fact]
    public void EnablingStartsProviderAndDisablingCancelsItsActiveRequest()
    {
        using var activation = new UsageProviderActivation(false);
        Assert.False(activation.TryGetToken(out _));

        Assert.True(activation.SetEnabled(true));
        Assert.True(activation.TryGetToken(out var enabledToken));
        Assert.True(activation.IsCurrent(enabledToken));
        Assert.False(enabledToken.IsCancellationRequested);

        Assert.True(activation.SetEnabled(false));
        Assert.True(enabledToken.IsCancellationRequested);
        Assert.False(activation.TryGetToken(out _));
        Assert.False(activation.IsCurrent(enabledToken));
    }

    [Fact]
    public void ReenablingRejectsResponsesFromPreviousActivation()
    {
        using var activation = new UsageProviderActivation(true);
        Assert.True(activation.TryGetToken(out var previous));

        activation.SetEnabled(false);
        activation.SetEnabled(true);

        Assert.True(activation.TryGetToken(out var current));
        Assert.True(activation.IsCurrent(current));
        Assert.False(activation.IsCurrent(previous));
        Assert.NotEqual(previous, current);
    }

    [Fact]
    public void ApplyingUnchangedSettingsKeepsActiveRequestAlive()
    {
        using var activation = new UsageProviderActivation(true);
        Assert.True(activation.TryGetToken(out var token));

        Assert.False(activation.SetEnabled(true));

        Assert.True(activation.IsCurrent(token));
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void DisposalCancelsRequestsAndPreventsReactivation()
    {
        var activation = new UsageProviderActivation(true);
        Assert.True(activation.TryGetToken(out var token));

        activation.Dispose();
        activation.Dispose();

        Assert.True(token.IsCancellationRequested);
        Assert.False(activation.IsCurrent(token));
        Assert.False(activation.SetEnabled(true));
        Assert.False(activation.TryGetToken(out _));
    }
}
