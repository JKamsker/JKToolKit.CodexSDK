using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class IdExtractionFixturesTests
{
    private static JsonElement LoadFixture(string name)
    {
        var relative = Path.Combine("Fixtures", name);
        var fullPath = Path.Combine(AppContext.BaseDirectory, relative);

        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "JKToolKit.CodexSDK.Tests", relative);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ExtractTurnId_FromReviewStartResponse()
    {
        var raw = LoadFixture("review-start-response.json");
        raw.TryGetProperty("turn", out var turn).Should().BeTrue();

        CodexAppServerClient.ExtractTurnId(turn).Should().Be("turn_900");
    }

    [Fact]
    public void ExtractTurnId_FromSteerResponse()
    {
        CodexAppServerClient.ExtractTurnId(LoadFixture("turn-steer-response.json")).Should().Be("turn_456");
    }
}

