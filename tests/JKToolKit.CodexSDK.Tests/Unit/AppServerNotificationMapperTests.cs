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
    public void Map_ThreadRealtimeNotifications_ToTypedRecords()
    {
        var started = JsonDocument.Parse("""{"threadId":"t","sessionId":"s"}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/started", started)
            .Should().BeOfType<ThreadRealtimeStartedNotification>()
            .Which.SessionId.Should().Be("s");

        var itemAdded = JsonDocument.Parse("""{"threadId":"t","item":{"type":"x"}}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/itemAdded", itemAdded)
            .Should().BeOfType<ThreadRealtimeItemAddedNotification>()
            .Which.ItemType.Should().Be("x");

        var audioDelta = JsonDocument.Parse("""{"threadId":"t","audio":{"data":"abc","numChannels":2,"sampleRate":24000,"samplesPerChannel":480}}""").RootElement;
        var mapped = AppServerNotificationMapper.Map("thread/realtime/outputAudio/delta", audioDelta);

        mapped.Should().BeOfType<ThreadRealtimeOutputAudioDeltaNotification>()
            .Which.Data.Should().Be("abc");

        var closed = JsonDocument.Parse("""{"threadId":"t","reason":"bye"}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/closed", closed)
            .Should().BeOfType<ThreadRealtimeClosedNotification>()
            .Which.Reason.Should().Be("bye");

        var error = JsonDocument.Parse("""{"threadId":"t","message":"oops"}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/error", error)
            .Should().BeOfType<ThreadRealtimeErrorNotification>()
            .Which.Message.Should().Be("oops");
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
        mapped.OfType<ItemStartedNotification>().Should().ContainSingle().Which.ItemType.Should().Be("agentMessage");
        mapped.OfType<ItemCompletedNotification>().Should().ContainSingle().Which.ItemType.Should().Be("agentMessage");
        mapped.Should().ContainSingle(x => x is AppListUpdatedNotification);
        mapped.Should().ContainSingle(x => x is FuzzyFileSearchSessionUpdatedNotification);
        mapped.Should().ContainSingle(x => x is FuzzyFileSearchSessionCompletedNotification);
        mapped.Should().ContainSingle(x => x is TurnCompletedNotification);
        mapped.Should().ContainSingle(x => x is UnknownNotification);
    }
}

