using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadSummaryParsingTests
{
    [Fact]
    public void ParseThreadSummary_InfersArchived_WhenPathUsesArchivedSessions()
    {
        using var doc = JsonDocument.Parse("""
        {
          "id": "t1",
          "preview": "hello",
          "path": "C:/Users/Jonas/.codex/archived_sessions/rollout.jsonl"
        }
        """);

        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(doc.RootElement);
        summary.Should().NotBeNull();
        summary!.Archived.Should().BeTrue();
    }

    [Fact]
    public void ParseThreadSummary_UsesEnvelopeFallbacks_ForListAndLifecycleShapes()
    {
        using var doc = JsonDocument.Parse("""
        {
          "model": "gpt-5",
          "serviceTier": "fast",
          "thread": {
            "id": "t2",
            "name": "hello"
          }
        }
        """);

        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(doc.RootElement.GetProperty("thread"), doc.RootElement);

        summary.Should().NotBeNull();
        summary!.Model.Should().Be("gpt-5");
        summary.ServiceTier.Should().Be(JKToolKit.CodexSDK.Models.CodexServiceTier.Fast);
    }
}
