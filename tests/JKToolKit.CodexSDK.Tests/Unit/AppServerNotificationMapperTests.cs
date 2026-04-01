using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
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
        var started = JsonDocument.Parse("""{"threadId":"t","sessionId":"s","version":"v2"}""").RootElement;
        var startedMapped = AppServerNotificationMapper.Map("thread/realtime/started", started)
            .Should().BeOfType<ThreadRealtimeStartedNotification>()
            .Which;

        startedMapped.SessionId.Should().Be("s");
        startedMapped.Version.Should().Be("v2");

        var itemAdded = JsonDocument.Parse("""{"threadId":"t","item":{"type":"x"}}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/itemAdded", itemAdded)
            .Should().BeOfType<ThreadRealtimeItemAddedNotification>()
            .Which.ItemType.Should().Be("x");

        var transcriptUpdated = JsonDocument.Parse("""{"threadId":"t","role":"assistant","text":"hello"}""").RootElement;
        AppServerNotificationMapper.Map("thread/realtime/transcriptUpdated", transcriptUpdated)
            .Should().BeOfType<ThreadRealtimeTranscriptUpdatedNotification>()
            .Which.Text.Should().Be("hello");

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
    public void Map_ThreadStarted_SurfacesTypedThreadSummary()
    {
        var started = JsonDocument.Parse("""
        {
          "thread": {
            "id": "thread-1",
            "name": "Review",
            "cwd": "C:\\repo",
            "source": "cli"
          }
        }
        """).RootElement;

        var mapped = AppServerNotificationMapper.Map("thread/started", started)
            .Should().BeOfType<ThreadStartedNotification>()
            .Which;

        mapped.ThreadId.Should().Be("thread-1");
        mapped.ThreadSummary.Should().NotBeNull();
        mapped.ThreadSummary!.Name.Should().Be("Review");
        mapped.ThreadSummary.Cwd.Should().Be("C:\\repo");
        mapped.ThreadSummary.SourceKind.Should().Be("cli");
    }

    [Fact]
    public void Map_ThreadRealtimeTranscriptUpdated_RequiresStructuredFields()
    {
        var invalid = JsonDocument.Parse("""{"threadId":"t","role":123}""").RootElement;

        var mapped = AppServerNotificationMapper.Map("thread/realtime/transcriptUpdated", invalid);

        mapped.Should().BeOfType<UnknownNotification>();
    }

    [Fact]
    public void Map_NewlyTypedNotificationMethods_ToTypedRecords()
    {
        var commandExecDelta = JsonDocument.Parse("""{"processId":"p1","stream":"stdout","deltaBase64":"aGVsbG8=","capReached":false}""").RootElement;
        var commandExecNotification = AppServerNotificationMapper.Map("command/exec/outputDelta", commandExecDelta)
            .Should().BeOfType<CommandExecOutputDeltaNotification>()
            .Which;

        commandExecNotification.ProcessId.Should().Be("p1");
        commandExecNotification.Stream.Should().Be("stdout");
        commandExecNotification.StreamKind.Should().Be(CommandExecOutputStreamKind.Stdout);

        var fsChanged = JsonDocument.Parse("""{"watchId":"w1","changedPaths":["C:\\repo\\a.txt","C:\\repo\\b.txt"]}""").RootElement;
        AppServerNotificationMapper.Map("fs/changed", fsChanged)
            .Should().BeOfType<FsChangedNotification>()
            .Which.ChangedPaths.Should().HaveCount(2);

        var hookStarted = JsonDocument.Parse("""{"threadId":"t","turnId":"u","run":{"id":"r1","status":"running"}}""").RootElement;
        AppServerNotificationMapper.Map("hook/started", hookStarted)
            .Should().BeOfType<HookStartedNotification>()
            .Which.ThreadId.Should().Be("t");

        var hookCompleted = JsonDocument.Parse("""{"threadId":"t","run":{"id":"r1","status":"completed"}}""").RootElement;
        AppServerNotificationMapper.Map("hook/completed", hookCompleted)
            .Should().BeOfType<HookCompletedNotification>()
            .Which.TurnId.Should().BeNull();

        var startupStatus = JsonDocument.Parse("""{"name":"server-1","status":"ready","error":null}""").RootElement;
        AppServerNotificationMapper.Map("mcpServer/startupStatus/updated", startupStatus)
            .Should().BeOfType<McpServerStartupStatusUpdatedNotification>()
            .Which.Status.Should().Be(McpServerStartupState.Ready);

        AppServerNotificationMapper.Map("skills/changed", JsonDocument.Parse("""{}""").RootElement)
            .Should().BeOfType<SkillsChangedNotification>();

        var appListUpdated = JsonDocument.Parse("""{"data":[{"id":"app-1","name":"Calendar","isEnabled":true}]}""").RootElement;
        AppServerNotificationMapper.Map("app/list/updated", appListUpdated)
            .Should().BeOfType<AppListUpdatedNotification>()
            .Which.Apps.Should().ContainSingle();

        var requestResolved = JsonDocument.Parse("""{"threadId":"t1","requestId":123}""").RootElement;
        var resolvedNotification = AppServerNotificationMapper.Map("serverRequest/resolved", requestResolved)
            .Should().BeOfType<ServerRequestResolvedNotification>()
            .Which;

        resolvedNotification.RequestId.IntegerValue.Should().Be(123);
        resolvedNotification.RequestIdValue.Should().Be("123");

        var stringRequestResolved = JsonDocument.Parse("""{"threadId":"t1","requestId":"req-1"}""").RootElement;
        AppServerNotificationMapper.Map("serverRequest/resolved", stringRequestResolved)
            .Should().BeOfType<ServerRequestResolvedNotification>()
            .Which.RequestId.StringValue.Should().Be("req-1");
    }

    [Fact]
    public void Map_CommandExecOutputDelta_WithMalformedPayload_ReturnsUnknown()
    {
        var invalid = JsonDocument.Parse("""{"processId":"p1","stream":"stdout"}""").RootElement;

        AppServerNotificationMapper.Map("command/exec/outputDelta", invalid)
            .Should().BeOfType<UnknownNotification>();
    }

    [Theory]
    [InlineData("authStatusChange")]
    [InlineData("loginChatGptComplete")]
    [InlineData("sessionConfigured")]
    public void Map_LegacyNotificationMethods_ReturnUnknown(string method)
    {
        var payload = JsonDocument.Parse("""{"info":"legacy"}""").RootElement;

        var mapped = AppServerNotificationMapper.Map(method, payload);

        var unknown = mapped.Should().BeOfType<UnknownNotification>().Subject;
        unknown.Method.Should().Be(method);
    }

    [Fact]
    public void Map_FuzzyFileSearchSessionUpdated_PreservesMatchType()
    {
        var updated = JsonDocument.Parse("""{"sessionId":"s1","query":"","files":[{"root":"C:\\repo","path":"src\\App.cs","fileName":"App.cs","score":42,"matchType":"file"}]}""").RootElement;
        var notification = AppServerNotificationMapper.Map("fuzzyFileSearch/sessionUpdated", updated)
            .Should().BeOfType<FuzzyFileSearchSessionUpdatedNotification>()
            .Which;

        notification.Files.Should().ContainSingle();
        notification.Files[0].MatchType.Should().Be("file");
        notification.Files[0].MatchKind.Should().Be(FuzzyFileSearchMatchType.File);
    }

    [Fact]
    public void Map_ReasoningIndexNotifications_UseLongValues()
    {
        const long summaryIndex = 3_000_000_000;
        var summaryTextDelta = JsonDocument.Parse("""{"threadId":"t","turnId":"u","itemId":"i","delta":"x","summaryIndex":3000000000}""").RootElement;
        AppServerNotificationMapper.Map("item/reasoning/summaryTextDelta", summaryTextDelta)
            .Should().BeOfType<ReasoningSummaryTextDeltaNotification>()
            .Which.SummaryIndex.Should().Be(summaryIndex);

        var summaryPartAdded = JsonDocument.Parse("""{"threadId":"t","turnId":"u","itemId":"i","summaryIndex":"4000000000"}""").RootElement;
        AppServerNotificationMapper.Map("item/reasoning/summaryPartAdded", summaryPartAdded)
            .Should().BeOfType<ReasoningSummaryPartAddedNotification>()
            .Which.SummaryIndex.Should().Be(4_000_000_000L);

        var textDelta = JsonDocument.Parse("""{"threadId":"t","turnId":"u","itemId":"i","delta":"x","contentIndex":5000000000}""").RootElement;
        AppServerNotificationMapper.Map("item/reasoning/textDelta", textDelta)
            .Should().BeOfType<ReasoningTextDeltaNotification>()
            .Which.ContentIndex.Should().Be(5_000_000_000L);
    }

    [Fact]
    public void Map_ModelRerouted_ToTypedRecord()
    {
        var json = JsonDocument.Parse("""{"threadId":"t","turnId":"u","fromModel":"a","toModel":"b","reason":"highRiskCyberActivity"}""").RootElement;
        AppServerNotificationMapper.Map("model/rerouted", json)
            .Should().BeOfType<ModelReroutedNotification>()
            .Which.ToModel.Should().Be("b");
    }

    [Fact]
    public void Map_AutoApprovalReviewNotifications_ToTypedRecords()
    {
        var started = JsonDocument.Parse("""{"threadId":"t","turnId":"u","targetItemId":"item-1","action":{"type":"exec"},"review":{"status":"inProgress","rationale":"checking","riskScore":4}}""").RootElement;
        var startedNotification = AppServerNotificationMapper.Map("item/autoApprovalReview/started", started)
            .Should().BeOfType<ItemAutoApprovalReviewStartedNotification>()
            .Which;

        startedNotification.TargetItemId.Should().Be("item-1");
        startedNotification.Review.Status.Should().Be(GuardianApprovalReviewStatus.InProgress);
        startedNotification.Review.Rationale.Should().Be("checking");

        var completed = JsonDocument.Parse("""{"threadId":"t","turnId":"u","targetItemId":"item-1","action":{"type":"exec"},"review":{"status":"approved","riskScore":"5"}}""").RootElement;
        var completedNotification = AppServerNotificationMapper.Map("item/autoApprovalReview/completed", completed)
            .Should().BeOfType<ItemAutoApprovalReviewCompletedNotification>()
            .Which;

        completedNotification.Review.Status.Should().Be(GuardianApprovalReviewStatus.Approved);
        completedNotification.Review.RiskScore.Should().Be(5);
    }

    [Fact]
    public void Map_ThreadRealtimeOutputAudioDelta_SurfacesAudioItemId()
    {
        var audioDelta = JsonDocument.Parse("""{"threadId":"t","audio":{"itemId":"item-42","data":"abc","numChannels":2,"sampleRate":24000,"samplesPerChannel":480}}""").RootElement;
        var mapped = AppServerNotificationMapper.Map("thread/realtime/outputAudio/delta", audioDelta)
            .Should().BeOfType<ThreadRealtimeOutputAudioDeltaNotification>()
            .Which;

        mapped.ItemId.Should().Be("item-42");
        mapped.Data.Should().Be("abc");
    }

    [Fact]
    public void Map_AccountUpdated_ParsesPlanType()
    {
        var json = JsonDocument.Parse("""{"authMode":"chatgptAuthTokens","planType":"unknown"}""").RootElement;

        var mapped = AppServerNotificationMapper.Map("account/updated", json);

        var typed = mapped.Should().BeOfType<AccountUpdatedNotification>().Subject;
        typed.AuthMode.Should().Be(CodexAuthMode.ChatGptAuthTokens);
        typed.PlanType.Should().Be(CodexPlanType.Unknown);
    }

    [Fact]
    public void Map_AccountUpdated_WithInvalidFieldType_ReturnsUnknown()
    {
        var json = JsonDocument.Parse("""{"authMode":123}""").RootElement;

        var mapped = AppServerNotificationMapper.Map("account/updated", json);

        mapped.Should().BeOfType<UnknownNotification>();
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

