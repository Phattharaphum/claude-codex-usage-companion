using System.Text.Json;
using CodexUsageCompanion.RateLimits;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class ClaudeCredentialsWriterTests
{
    [Fact]
    public void WriteReplacesTokensAndKeepsFieldsItDoesNotOwn()
    {
        var path = WriteTempFile(
            """
            {"claudeAiOauth":{"accessToken":"old","refreshToken":"old-refresh","expiresAt":1,
              "subscriptionType":"pro","rateLimitTier":"default_claude_ai"},
             "otherProvider":{"token":"keep-me"}}
            """);
        try
        {
            ClaudeCredentialsWriter.Write(
                path,
                new ClaudeRefreshedTokens("new", "new-refresh", 1234, 5678, ["user:inference"]));

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var oauth = document.RootElement.GetProperty("claudeAiOauth");
            Assert.Equal("new", oauth.GetProperty("accessToken").GetString());
            Assert.Equal("new-refresh", oauth.GetProperty("refreshToken").GetString());
            Assert.Equal(1234, oauth.GetProperty("expiresAt").GetInt64());
            Assert.Equal(5678, oauth.GetProperty("refreshTokenExpiresAt").GetInt64());
            Assert.Equal("pro", oauth.GetProperty("subscriptionType").GetString());
            Assert.Equal("default_claude_ai", oauth.GetProperty("rateLimitTier").GetString());
            Assert.Equal(
                "keep-me",
                document.RootElement.GetProperty("otherProvider").GetProperty("token").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteLeavesNoTemporaryFileAndStaysOwnerReadable()
    {
        var path = WriteTempFile("""{"claudeAiOauth":{"accessToken":"old","expiresAt":1}}""");
        try
        {
            ClaudeCredentialsWriter.Write(
                path,
                new ClaudeRefreshedTokens("new", "new-refresh", 1234, null, null));

            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    File.GetUnixFileMode(path));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteKeepsOmittedOptionalFieldsUnchanged()
    {
        var path = WriteTempFile(
            """{"claudeAiOauth":{"accessToken":"old","expiresAt":1,"refreshTokenExpiresAt":99,"scopes":["user:profile"]}}""");
        try
        {
            ClaudeCredentialsWriter.Write(
                path,
                new ClaudeRefreshedTokens("new", "new-refresh", 1234, null, null));

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var oauth = document.RootElement.GetProperty("claudeAiOauth");
            Assert.Equal(99, oauth.GetProperty("refreshTokenExpiresAt").GetInt64());
            Assert.Equal(
                "user:profile",
                oauth.GetProperty("scopes")[0].GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"claude-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ".credentials.json");
        File.WriteAllText(path, content);
        return path;
    }
}
