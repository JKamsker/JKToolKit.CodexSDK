using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.AppServer.ResponseItems;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationRoutingContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    private static T Map<T>(string method, string json) where T : AppServerNotification
    {
        var payload = Json(json);
        var value = AppServerNotificationMapper.Map(method, payload).Should().BeOfType<T>().Subject;
        value.Method.Should().Be(method); JsonElement.DeepEquals(value.Params, payload).Should().BeTrue();
        return value;
    }

    [Fact]
    public void ThreadLifecycleNotifications_KeepRoutingIdentifiers()
    {
        var routes = new (string Method, Func<AppServerNotification,string> ThreadId)[]
        {
            ("thread/archived", n => ((ThreadArchivedNotification)n).ThreadId),
            ("thread/unarchived", n => ((ThreadUnarchivedNotification)n).ThreadId),
            ("thread/closed", n => ((ThreadClosedNotification)n).ThreadId),
            ("turn/started", n => ((TurnStartedNotification)n).ThreadId),
            ("turn/completed", n => ((TurnCompletedNotification)n).ThreadId),
            ("thread/realtime/started", n => ((ThreadRealtimeStartedNotification)n).ThreadId),
            ("thread/realtime/itemAdded", n => ((ThreadRealtimeItemAddedNotification)n).ThreadId),
            ("thread/realtime/sdp", n => ((ThreadRealtimeSdpNotification)n).ThreadId),
            ("thread/realtime/error", n => ((ThreadRealtimeErrorNotification)n).ThreadId),
            ("thread/realtime/closed", n => ((ThreadRealtimeClosedNotification)n).ThreadId),
            ("thread/environment/connected", n => ((ThreadEnvironmentConnectionNotification)n).ThreadId),
            ("thread/tokenUsage/updated", n => ((ThreadTokenUsageUpdatedNotification)n).ThreadId),
            ("turn/diff/updated", n => ((TurnDiffUpdatedNotification)n).ThreadId)
        };
        var payload = Json("""{"threadId":"thread","turnId":"turn","version":"v2","sessionId":"session","item":{},"sdp":"sdp","message":"message","reason":"reason","environmentId":"environment","turn":{"id":"turn","status":"completed"},"tokenUsage":{},"diff":"diff"}""");
        foreach (var route in routes)
        {
            var notification = AppServerNotificationMapper.Map(route.Method, payload);
            notification.Method.Should().Be(route.Method); route.ThreadId(notification).Should().Be("thread", route.Method);
        }
    }

    [Fact]
    public void MessageReasoningAndToolProgress_KeepDistinctCorrelationFields()
    {
        const string payload = """{"threadId":"thread","turnId":"turn","itemId":"item","delta":"delta","message":"progress","summaryIndex":7}""";
        var agent = Map<AgentMessageDeltaNotification>("item/agentMessage/delta", payload);
        (agent.ThreadId, agent.TurnId, agent.ItemId, agent.Delta).Should().Be(("thread", "turn", "item", "delta"));
        var plan = Map<PlanDeltaNotification>("item/plan/delta", payload);
        (plan.ThreadId, plan.TurnId, plan.ItemId, plan.Delta).Should().Be(("thread", "turn", "item", "delta"));
        var reasoning = Map<ReasoningSummaryTextDeltaNotification>("item/reasoning/summaryTextDelta", payload);
        (reasoning.ThreadId, reasoning.TurnId, reasoning.ItemId, reasoning.Delta).Should().Be(("thread", "turn", "item", "delta")); reasoning.SummaryIndex.Should().Be(7);
        var part = Map<ReasoningSummaryPartAddedNotification>("item/reasoning/summaryPartAdded", payload);
        (part.ThreadId, part.TurnId, part.ItemId).Should().Be(("thread", "turn", "item")); part.SummaryIndex.Should().Be(7);
        var progress = Map<McpToolCallProgressNotification>("item/mcpToolCall/progress", payload);
        (progress.ThreadId, progress.TurnId, progress.ItemId, progress.Message).Should().Be(("thread", "turn", "item", "progress"));
        var compacted = Map<ContextCompactedNotification>("thread/compacted", payload);
        (compacted.ThreadId, compacted.TurnId).Should().Be(("thread", "turn"));
    }

    [Fact]
    public void ModelAndAccountNotifications_MapReportedChanges()
    {
        var model = Map<ModelReroutedNotification>("model/rerouted", """{"threadId":"t","turnId":"u","fromModel":"old","toModel":"new","reason":"capacity"}""");
        (model.ThreadId, model.TurnId, model.FromModel, model.ToModel, model.Reason).Should().Be(("t", "u", "old", "new", "capacity"));
        var account = Map<AccountUpdatedNotification>("account/updated", """{"authMode":"chatgpt","planType":"pro"}""");
        account.AuthMode.Should().Be(CodexAuthMode.ChatGpt); account.PlanType.Should().Be(CodexPlanType.Pro); account.AuthModeValue.Should().Be("chatgpt"); account.PlanTypeValue.Should().Be("pro");
        var empty = Map<AccountUpdatedNotification>("account/updated", "{}"); empty.AuthModeValue.Should().BeNull(); empty.PlanTypeValue.Should().BeNull();
        var login = Map<AccountLoginCompletedNotification>("account/login/completed", """{"loginId":"login","success":false,"error":"cancelled"}""");
        login.LoginId.Should().Be("login"); login.Success.Should().BeFalse(); login.Error.Should().Be("cancelled");
        var limits = Map<AccountRateLimitsUpdatedNotification>("account/rateLimits/updated", """{"rateLimits":{"usedPercent":12}}""");
        limits.RateLimits.GetProperty("usedPercent").GetInt32().Should().Be(12);
    }

    [Fact]
    public void RawResponseNotifications_KeepCorrelationAndTypedOutput()
    {
        var raw = Map<RawResponseItemCompletedNotification>("rawResponseItem/completed", """{"threadId":"t","turnId":"u","item":{"type":"function_call_output","call_id":"call","output":"result"}}""");
        (raw.ThreadId, raw.TurnId).Should().Be(("t", "u")); raw.Item.GetProperty("call_id").GetString().Should().Be("call");
        var output = raw.ResponseItem.Should().BeOfType<CodexResponseItemFunctionCallOutput>().Subject;
        output.CallId.Should().Be("call"); output.Output.GetString().Should().Be("result");
        var completed = Map<RawResponseCompletedNotification>("rawResponse/completed", """{"threadId":"t","turnId":"u","responseId":"r"}""");
        (completed.ThreadId, completed.TurnId).Should().Be(("t", "u")); completed.ResponseId.Should().Be("r");
    }

    [Fact]
    public void ProjectNamingSearchAndWarnings_PreserveTheirOwnIdentifiers()
    {
        var name = Map<ThreadNameUpdatedNotification>("thread/name/updated", """{"threadId":"t","threadName":"Name"}""");
        (name.ThreadId, name.ThreadName).Should().Be(("t", "Name"));
        var project = Map<ThreadProjectUpdatedNotification>("thread/project/updated", """{"threadId":"t","projectId":"p"}""");
        (project.ThreadId, project.ProjectId).Should().Be(("t", "p"));
        var changed = Map<ProjectChangedNotification>("project/changed", """{"projectId":"p","changeType":"deleted"}""");
        changed.ProjectId.Should().Be("p"); changed.ChangeType.Should().Be("deleted");
        var search = Map<FuzzyFileSearchSessionUpdatedNotification>("fuzzyFileSearch/sessionUpdated", """{"sessionId":"s","query":"query","files":[]}""");
        search.SessionId.Should().Be("s"); search.Query.Should().Be("query"); search.Files.Should().BeEmpty();
        Map<FuzzyFileSearchSessionCompletedNotification>("fuzzyFileSearch/sessionCompleted", """{"sessionId":"s"}""").SessionId.Should().Be("s");
        var warning = Map<WindowsWorldWritableWarningNotification>("windows/worldWritableWarning", """{"samplePaths":["first",null,"second"],"extraCount":3,"failedScan":true}""");
        warning.SamplePaths.Should().Equal("first", "second"); warning.ExtraCount.Should().Be(3); warning.FailedScan.Should().BeTrue();
        var deprecation = Map<DeprecationNoticeNotification>("deprecationNotice", """{"summary":"Summary","details":"Details"}""");
        deprecation.Summary.Should().Be("Summary"); deprecation.Details.Should().Be("Details");
        var transcript = Map<ThreadRealtimeTranscriptUpdatedNotification>("thread/realtime/transcriptUpdated", """{"threadId":"t","role":"assistant","text":"text"}""");
        (transcript.ThreadId, transcript.Role, transcript.Text).Should().Be(("t", "assistant", "text"));
    }

    [Theory]
    [InlineData("\"123\"", 123L)]
    [InlineData("123", 123L)]
    public void StrictReview_MapsNumericAndLegacyStringTimestamp(string timestamp, long expected)
    {
        var review = Map<StrictReviewRequiredNotification>("autoApprovalReview/strictReviewRequired", "{\"threadId\":\"t\",\"turnId\":\"u\",\"startedAtMs\":" + timestamp + "}");
        (review.ThreadId, review.TurnId).Should().Be(("t", "u")); review.StartedAtMs.Should().Be(expected);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1.5")]
    [InlineData("\"bad\"")]
    public void StrictReview_RejectsUnparseableTimestamp(string timestamp) =>
        AppServerNotificationMapper.Map("autoApprovalReview/strictReviewRequired", Json("{\"threadId\":\"t\",\"turnId\":\"u\",\"startedAtMs\":" + timestamp + "}"))
            .Should().BeOfType<UnknownNotification>();
}
