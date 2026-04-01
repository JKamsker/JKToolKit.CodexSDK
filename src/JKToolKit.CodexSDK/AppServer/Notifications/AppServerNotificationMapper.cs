using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static partial class AppServerNotificationMapper
{
    public static AppServerNotification Map(string method, JsonElement? @params)
    {
        if (@params is null || @params.Value.ValueKind != JsonValueKind.Object)
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            return new UnknownNotification(method, emptyDoc.RootElement.Clone());
        }

        var p = @params.Value;

        return method switch
        {
            "error" => new ErrorNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Error: GetAny(p, "error"),
                WillRetry: GetBool(p, "willRetry"),
                Params: p),

            "thread/started" => new ThreadStartedNotification(
                Thread: GetAny(p, "thread"),
                ThreadSummary: CodexAppServerClientThreadParsers.ParseThreadSummary(GetAny(p, "thread"), p),
                Params: p),

            "thread/name/updated" => new ThreadNameUpdatedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                ThreadName: GetStringOrNull(p, "threadName"),
                Params: p),

            "thread/tokenUsage/updated" => new ThreadTokenUsageUpdatedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                TokenUsage: GetAny(p, "tokenUsage"),
                Params: p),

            "thread/status/changed" => new ThreadStatusChangedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Status: GetAny(p, "status"),
                Params: p),

            "thread/archived" => new ThreadArchivedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Params: p),

            "thread/unarchived" => new ThreadUnarchivedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Params: p),

            "thread/closed" => new ThreadClosedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Params: p),

            "thread/realtime/started" => (AppServerNotification?)TryMapThreadRealtimeStarted(p) ?? new UnknownNotification(method, p),

            "thread/realtime/itemAdded" => (AppServerNotification?)TryMapThreadRealtimeItemAdded(p) ?? new UnknownNotification(method, p),

            "thread/realtime/transcriptUpdated" when
                TryGetRequiredString(p, "threadId", out var transcriptThreadId) &&
                TryGetRequiredString(p, "role", out var transcriptRole) &&
                TryGetRequiredString(p, "text", out var transcriptText)
                => new ThreadRealtimeTranscriptUpdatedNotification(
                    ThreadId: transcriptThreadId,
                    Role: transcriptRole,
                    Text: transcriptText,
                    Params: p),

            "thread/realtime/outputAudio/delta" => (AppServerNotification?)TryMapThreadRealtimeOutputAudioDelta(p) ?? new UnknownNotification(method, p),

            "thread/realtime/error" => (AppServerNotification?)TryMapThreadRealtimeError(p) ?? new UnknownNotification(method, p),

            "thread/realtime/closed" => (AppServerNotification?)TryMapThreadRealtimeClosed(p) ?? new UnknownNotification(method, p),

            "model/rerouted" => new ModelReroutedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                FromModel: GetString(p, "fromModel") ?? string.Empty,
                ToModel: GetString(p, "toModel") ?? string.Empty,
                Reason: GetString(p, "reason") ?? string.Empty,
                Params: p),

            "turn/started" => new TurnStartedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Turn: GetAny(p, "turn"),
                Params: p),

            "hook/started" => (AppServerNotification?)TryMapHookStarted(p) ?? new UnknownNotification(method, p),

            "hook/completed" => (AppServerNotification?)TryMapHookCompleted(p) ?? new UnknownNotification(method, p),

            "item/agentMessage/delta" => new AgentMessageDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                Params: p),

            "item/started" => new ItemStartedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Item: GetAny(p, "item"),
                Params: p),

            "item/completed" => new ItemCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Item: GetAny(p, "item"),
                Params: p),

            "turn/completed" => new TurnCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Turn: GetAny(p, "turn"),
                Params: p),

            "turn/diff/updated" => new TurnDiffUpdatedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Diff: GetString(p, "diff") ?? string.Empty,
                Params: p),

            "turn/plan/updated" => new TurnPlanUpdatedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Explanation: GetStringOrNull(p, "explanation"),
                Plan: ParsePlan(p),
                Params: p),

            "item/plan/delta" => new PlanDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                Params: p),

            "command/exec/outputDelta" when
                TryGetRequiredString(p, "processId", out var processId) &&
                TryGetRequiredString(p, "stream", out var stream) &&
                TryGetRequiredString(p, "deltaBase64", out var deltaBase64) &&
                TryGetRequiredBool(p, "capReached", out var capReached)
                => new CommandExecOutputDeltaNotification(
                    ProcessId: processId,
                    Stream: stream,
                    DeltaBase64: deltaBase64,
                    CapReached: capReached,
                    Params: p),

            "rawResponseItem/completed" => new RawResponseItemCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Item: GetAny(p, "item"),
                Params: p),

            "item/commandExecution/outputDelta" => (AppServerNotification?)TryMapCommandExecutionOutputDelta(p) ?? new UnknownNotification(method, p),

            "item/commandExecution/terminalInteraction" => new TerminalInteractionNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                ProcessId: GetString(p, "processId") ?? string.Empty,
                Stdin: GetString(p, "stdin") ?? string.Empty,
                Params: p),

            "item/fileChange/outputDelta" => new FileChangeOutputDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                Params: p),

            "item/mcpToolCall/progress" => new McpToolCallProgressNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Message: GetString(p, "message") ?? string.Empty,
                Params: p),

            "mcpServer/oauthLogin/completed" => new McpServerOauthLoginCompletedNotification(
                Name: GetString(p, "name") ?? string.Empty,
                Success: GetBool(p, "success"),
                Error: GetStringOrNull(p, "error"),
                Params: p),

            "mcpServer/startupStatus/updated" => (AppServerNotification?)TryMapMcpServerStartupStatusUpdated(p) ?? new UnknownNotification(method, p),

            "account/updated" => (AppServerNotification?)TryMapAccountUpdated(p) ?? new UnknownNotification(method, p),

            "account/rateLimits/updated" => new AccountRateLimitsUpdatedNotification(
                RateLimits: GetAny(p, "rateLimits"),
                Params: p),

            "app/list/updated" => new AppListUpdatedNotification(
                apps: AppServerNotificationParsing.ParseAppsList(p),
                data: GetOptionalAny(p, "data") ?? GetAny(p, "apps"),
                @params: p),

            "skills/changed" => new SkillsChangedNotification(
                @params: p),

            "serverRequest/resolved" => (AppServerNotification?)TryMapServerRequestResolved(p) ?? new UnknownNotification(method, p),

            "fs/changed" => (AppServerNotification?)TryMapFsChanged(p) ?? new UnknownNotification(method, p),

            "fuzzyFileSearch/sessionUpdated" => new FuzzyFileSearchSessionUpdatedNotification(
                sessionId: GetString(p, "sessionId") ?? string.Empty,
                query: GetString(p, "query") ?? string.Empty,
                files: AppServerNotificationParsing.ParseFuzzyFileSearchResults(p),
                @params: p),

            "fuzzyFileSearch/sessionCompleted" => new FuzzyFileSearchSessionCompletedNotification(
                sessionId: GetString(p, "sessionId") ?? string.Empty,
                @params: p),

            "item/autoApprovalReview/started" => (AppServerNotification?)TryMapAutoApprovalReviewStarted(p) ?? new UnknownNotification(method, p),

            "item/autoApprovalReview/completed" => (AppServerNotification?)TryMapAutoApprovalReviewCompleted(p) ?? new UnknownNotification(method, p),

            "item/reasoning/summaryTextDelta" => new ReasoningSummaryTextDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                SummaryIndex: GetInt64(p, "summaryIndex"),
                Params: p),

            "item/reasoning/summaryPartAdded" => new ReasoningSummaryPartAddedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                SummaryIndex: GetInt64(p, "summaryIndex"),
                Params: p),

            "item/reasoning/textDelta" => new ReasoningTextDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                ContentIndex: GetInt64(p, "contentIndex"),
                Params: p),

            "thread/compacted" => new ContextCompactedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Params: p),

            "deprecationNotice" => new DeprecationNoticeNotification(
                Summary: GetString(p, "summary") ?? string.Empty,
                Details: GetStringOrNull(p, "details"),
                Params: p),

            "configWarning" => new ConfigWarningNotification(
                Summary: GetString(p, "summary") ?? string.Empty,
                Details: GetStringOrNull(p, "details"),
                Path: GetStringOrNull(p, "path"),
                Range: GetOptionalAny(p, "range"),
                Params: p),

            "windows/worldWritableWarning" => new WindowsWorldWritableWarningNotification(
                SamplePaths: GetStringArray(p, "samplePaths"),
                ExtraCount: GetInt32(p, "extraCount"),
                FailedScan: GetBool(p, "failedScan"),
                Params: p),

            "windowsSandbox/setupCompleted" => new WindowsSandboxSetupCompletedNotification(
                Mode: GetString(p, "mode") ?? string.Empty,
                Success: GetBool(p, "success"),
                Error: GetStringOrNull(p, "error"),
                Params: p),

            "account/login/completed" => new AccountLoginCompletedNotification(
                LoginId: GetStringOrNull(p, "loginId"),
                Success: GetBool(p, "success"),
                Error: GetStringOrNull(p, "error"),
                Params: p),

            _ => new UnknownNotification(method, p)
        };
    }

    private static string? GetString(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static bool TryGetRequiredString(JsonElement obj, string propertyName, out string value)
    {
        value = string.Empty;

        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetRequiredBool(JsonElement obj, string propertyName, out bool value)
    {
        value = default;

        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (prop.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        return false;
    }

    private static string? GetStringOrNull(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind is JsonValueKind.String
            ? prop.GetString()
            : null;

    private static bool GetBool(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True
            ? true
            : obj.TryGetProperty(propertyName, out prop) && prop.ValueKind == JsonValueKind.False
                ? false
                : default;

    private static int GetInt32(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number &&
        prop.TryGetInt32(out var i)
            ? i
            : default;

    private static long GetInt64(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return default;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var i))
        {
            return i;
        }

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out i))
        {
            return i;
        }

        return default;
    }

    private static JsonElement GetAny(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop)
            ? prop.Clone()
            : EmptyObject();

    private static JsonElement? GetOptionalAny(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
            ? prop.Clone()
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }

    private static IReadOnlyList<TurnPlanStep> ParsePlan(JsonElement obj)
    {
        if (!obj.TryGetProperty("plan", out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TurnPlanStep>();
        }

        var list = new List<TurnPlanStep>();
        foreach (var el in prop.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new TurnPlanStep(
                Step: GetString(el, "step") ?? string.Empty,
                Status: GetString(el, "status") ?? string.Empty));
        }

        return list;
    }

    private static JsonElement EmptyObject()
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        return emptyDoc.RootElement.Clone();
    }
}
