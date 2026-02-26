using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Protocol;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SessionIdCaptureRegexTests
{
    [Theory]
    [InlineData("session id: 11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111")]
    [InlineData("Session ID=opaque-session-123", "opaque-session-123")]
    [InlineData("sid: opaque_session_123", "opaque_session_123")]
    [InlineData("{\"session_id\":\"opaque-session-123\",\"ok\":true}", "opaque-session-123")]
    [InlineData("{\"sid\": \"opaque-session-123\"}", "opaque-session-123")]
    public void SessionIdRegex_CapturesExpectedId(string text, string expectedId)
    {
        var match = CodexClientRegexes.SessionIdRegex().Match(text);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be(expectedId);
        SessionId.TryParse(match.Groups[1].Value, out _).Should().BeTrue();
    }

    [Fact]
    public void SessionIdRegex_DoesNotCaptureTrailingJsonPunctuation()
    {
        var text = "{\"session_id\":\"opaque-session-123\", \"ok\":true}";

        var match = CodexClientRegexes.SessionIdRegex().Match(text);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("opaque-session-123");
    }
}

