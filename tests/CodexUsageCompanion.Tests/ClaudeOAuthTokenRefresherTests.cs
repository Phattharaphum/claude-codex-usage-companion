using System.Net;
using System.Net.Http;
using System.Text.Json;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ClaudeOAuthTokenRefresherTests
{
    [Fact]
    public async Task RefreshAsyncPostsRefreshGrantAndReturnsRotatedTokens()
    {
        string? capturedBody = null;
        Uri? capturedUri = null;
        var handler = new StubHandler(async request =>
        {
            capturedUri = request.RequestUri;
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {"access_token":"sk-ant-oat01-new","refresh_token":"sk-ant-ort01-new",
                     "expires_in":28800,"refresh_token_expires_in":2592000,
                     "scope":"user:inference user:profile"}
                    """)
            };
        });
        var refresher = new ClaudeOAuthTokenRefresher(new HttpClient(handler));

        var tokens = await refresher.RefreshAsync(
            new ClaudeCredentials("sk-ant-oat01-old", 0, "sk-ant-ort01-old", ["user:inference"]),
            CancellationToken.None);

        Assert.Equal(ClaudeOAuthTokenRefresher.TokenEndpoint, capturedUri?.ToString());
        using var request = JsonDocument.Parse(capturedBody!);
        Assert.Equal("refresh_token", request.RootElement.GetProperty("grant_type").GetString());
        Assert.Equal("sk-ant-ort01-old", request.RootElement.GetProperty("refresh_token").GetString());
        Assert.Equal(ClaudeOAuthTokenRefresher.ClientId, request.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("user:inference", request.RootElement.GetProperty("scope").GetString());
        Assert.Equal("sk-ant-oat01-new", tokens.AccessToken);
        Assert.Equal("sk-ant-ort01-new", tokens.RefreshToken);
        Assert.True(tokens.ExpiresAtUnixMs > DateTimeOffset.UtcNow.AddHours(7).ToUnixTimeMilliseconds());
        Assert.NotNull(tokens.RefreshTokenExpiresAtUnixMs);
        Assert.Equal(["user:inference", "user:profile"], tokens.Scopes);
    }

    [Fact]
    public async Task RefreshAsyncThrowsSessionExpiredWhenGrantIsRejected()
    {
        var handler = new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant"}""")
            }));
        var refresher = new ClaudeOAuthTokenRefresher(new HttpClient(handler));

        await Assert.ThrowsAsync<ClaudeSessionExpiredException>(
            () => refresher.RefreshAsync(
                new ClaudeCredentials("sk-ant-oat01-old", 0, "sk-ant-ort01-old", null),
                CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsyncThrowsSessionExpiredWithoutRefreshToken()
    {
        var called = false;
        var handler = new StubHandler(_ =>
        {
            called = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var refresher = new ClaudeOAuthTokenRefresher(new HttpClient(handler));

        await Assert.ThrowsAsync<ClaudeSessionExpiredException>(
            () => refresher.RefreshAsync(
                new ClaudeCredentials("sk-ant-oat01-old", 0, null, null),
                CancellationToken.None));
        Assert.False(called);
    }

    [Fact]
    public void ParseKeepsPreviousRefreshTokenWhenResponseOmitsIt()
    {
        var tokens = ClaudeOAuthTokenRefresher.Parse(
            """{"access_token":"sk-ant-oat01-new","expires_in":3600}""",
            "sk-ant-ort01-old");

        Assert.Equal("sk-ant-ort01-old", tokens.RefreshToken);
        Assert.Null(tokens.RefreshTokenExpiresAtUnixMs);
        Assert.Null(tokens.Scopes);
    }

    [Theory]
    [InlineData("""{"access_token":"sk-ant-oat01-new"}""")]
    [InlineData("""{"access_token":"sk-ant-oat01-new","expires_in":0}""")]
    [InlineData("""{"expires_in":3600}""")]
    [InlineData("not json")]
    public void ParseRejectsResponsesThatWouldExpireImmediately(string body)
    {
        Assert.Throws<ClaudeCredentialsFormatException>(
            () => ClaudeOAuthTokenRefresher.Parse(body, "sk-ant-ort01-old"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => respond(request);
    }
}
