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
}

