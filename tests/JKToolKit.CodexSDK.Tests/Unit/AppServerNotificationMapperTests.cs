using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationMapperTests
{
    [Fact]
    public void Map_KnownNotifications_ToTypedRecords()
    {
        var json = JsonDocument.Parse("""{"threadId":"t","turnId":"u","itemId":"i","delta":"hi"}""").RootElement;
        var mapped = AppServerNotificationMapper.Map("item/agentMessage/delta", json);

        mapped.Should().BeOfType<AgentMessageDeltaNotification>()
            .Which.Delta.Should().Be("hi");
    }

    [Fact]
    public void Map_FixtureJsonl_MapsAllLines()
    {
        var path = Path.Combine("Fixtures", "appserver-notifications.jsonl");
        var fullPath = Path.Combine(AppContext.BaseDirectory, path);

        // test runner copies content into output; fall back to repo-relative path
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "JKToolKit.CodexSDK.Tests", path);
        }

        var lines = File.ReadAllLines(fullPath);
        var mapped = new List<AppServerNotification>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            var method = doc.RootElement.GetProperty("method").GetString()!;
            var @params = doc.RootElement.GetProperty("params").Clone();
            mapped.Add(AppServerNotificationMapper.Map(method, @params));
        }

        mapped.Should().ContainSingle(x => x is AgentMessageDeltaNotification);
        mapped.Should().ContainSingle(x => x is ThreadStartedNotification);
        mapped.Should().ContainSingle(x => x is ThreadNameUpdatedNotification);
        mapped.Should().ContainSingle(x => x is TurnStartedNotification);
        mapped.Should().ContainSingle(x => x is AppListUpdatedNotification);
        mapped.Should().ContainSingle(x => x is FuzzyFileSearchSessionUpdatedNotification);
        mapped.Should().ContainSingle(x => x is FuzzyFileSearchSessionCompletedNotification);
        mapped.Should().ContainSingle(x => x is TurnCompletedNotification);
        mapped.Should().ContainSingle(x => x is UnknownNotification);
    }
}

