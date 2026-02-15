using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class IdExtractionFixturesTests
{
    [Fact]
    public void ExtractTurnId_FromReviewStartResponse()
    {
        var raw = JsonFixtures.Load("review-start-response.json");
        raw.TryGetProperty("turn", out var turn).Should().BeTrue();

        CodexAppServerClient.ExtractTurnId(turn).Should().Be("turn_900");
    }

    [Fact]
    public void ExtractTurnId_FromSteerResponse()
    {
        CodexAppServerClient.ExtractTurnId(JsonFixtures.Load("turn-steer-response.json")).Should().Be("turn_456");
    }
}

