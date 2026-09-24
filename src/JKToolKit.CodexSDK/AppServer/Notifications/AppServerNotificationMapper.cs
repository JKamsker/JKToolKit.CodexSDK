using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Json;
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
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Error: GetAny(p, JsonFieldNames.Error),
                WillRetry: GetBool(p, "willRetry"),
                Params: p),

            AppServerMethods.ThreadStarted => new ThreadStartedNotification(
                Thread: GetAny(p, JsonFieldNames.Thread),
                ThreadSummary: CodexAppServerClientThreadParsers.ParseThreadSummary(GetAny(p, JsonFieldNames.Thread), p),
                Params: p),

            AppServerMethods.ThreadNameUpdated => new ThreadNameUpdatedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                ThreadName: GetStringOrNull(p, "threadName"),
                Params: p),

            AppServerMethods.ThreadSettingsUpdated => new ThreadSettingsUpdatedNotification(
                threadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                threadSettings: GetAny(p, JsonFieldNames.ThreadSettings),
                cwd: GetStringOrNull(GetAny(p, JsonFieldNames.ThreadSettings), JsonFieldNames.Cwd),
                model: GetStringOrNull(GetAny(p, JsonFieldNames.ThreadSettings), JsonFieldNames.Model),
                serviceTier: GetStringOrNull(GetAny(p, JsonFieldNames.ThreadSettings), JsonFieldNames.ServiceTier),
                disabledPluginIds: GetStringArray(GetAny(p, JsonFieldNames.ThreadSettings), JsonFieldNames.DisabledPluginIds),
                @params: p),

            AppServerMethods.ThreadGoalUpdated => new ThreadGoalUpdatedNotification(
                threadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                turnId: GetStringOrNull(p, JsonFieldNames.TurnId),
                goal: CodexAppServerThreadManagementParsers.ParseThreadGoal(GetAny(p, JsonFieldNames.Goal)),
                @params: p),

            AppServerMethods.ThreadGoalCleared => new ThreadGoalClearedNotification(
                threadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                @params: p),

            AppServerMethods.ThreadTokenUsageUpdated => new ThreadTokenUsageUpdatedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                TokenUsage: GetAny(p, "tokenUsage"),
                Params: p),

            AppServerMethods.ThreadStatusChanged => new ThreadStatusChangedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Status: GetAny(p, JsonFieldNames.Status),
                Params: p),

            AppServerMethods.ThreadArchived => new ThreadArchivedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Params: p),

            AppServerMethods.ThreadDeleted when TryGetRequiredString(p, JsonFieldNames.ThreadId, out var deletedThreadId)
                => new ThreadDeletedNotification(
                    ThreadId: deletedThreadId,
                    Params: p),

            AppServerMethods.ThreadDeleted => new UnknownNotification(method, p),

            AppServerMethods.ThreadUnarchived => new ThreadUnarchivedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Params: p),

            AppServerMethods.ThreadReverted => new ThreadRevertedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Params: p),

            AppServerMethods.ThreadQueueChanged => new ThreadQueueChangedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Params: p),

            AppServerMethods.ThreadClosed => new ThreadClosedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Params: p),

            AppServerMethods.ThreadRealtimeStarted => (AppServerNotification?)TryMapThreadRealtimeStarted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ThreadRealtimeItemAdded => (AppServerNotification?)TryMapThreadRealtimeItemAdded(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ThreadRealtimeTranscriptUpdated when
                TryGetRequiredString(p, JsonFieldNames.ThreadId, out var transcriptThreadId) &&
                TryGetRequiredString(p, JsonFieldNames.Role, out var transcriptRole) &&
                TryGetRequiredString(p, JsonFieldNames.Text, out var transcriptText)
                => new ThreadRealtimeTranscriptUpdatedNotification(
                    ThreadId: transcriptThreadId,
                    Role: transcriptRole,
                    Text: transcriptText,
                    Params: p),

            AppServerMethods.ThreadRealtimeOutputAudioDelta => (AppServerNotification?)TryMapThreadRealtimeOutputAudioDelta(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ThreadRealtimeSdp => (AppServerNotification?)TryMapThreadRealtimeSdp(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ThreadRealtimeError => (AppServerNotification?)TryMapThreadRealtimeError(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ThreadRealtimeClosed => (AppServerNotification?)TryMapThreadRealtimeClosed(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ModelRerouted => new ModelReroutedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                FromModel: GetString(p, "fromModel") ?? string.Empty,
                ToModel: GetString(p, "toModel") ?? string.Empty,
                Reason: GetString(p, JsonFieldNames.Reason) ?? string.Empty,
                Params: p),

            AppServerMethods.ModelSafetyBufferingUpdated when
                TryGetRequiredString(p, JsonFieldNames.ThreadId, out var safetyBufferingThreadId) &&
                TryGetRequiredString(p, JsonFieldNames.TurnId, out var safetyBufferingTurnId) &&
                TryGetRequiredString(p, JsonFieldNames.Model, out var safetyBufferingModel) &&
                TryGetRequiredBool(p, "showBufferingUi", out var showBufferingUi)
                => new ModelSafetyBufferingUpdatedNotification(
                    ThreadId: safetyBufferingThreadId,
                    TurnId: safetyBufferingTurnId,
                    Model: safetyBufferingModel,
                    UseCases: GetStringArray(p, "useCases"),
                    Reasons: GetStringArray(p, "reasons"),
                    ShowBufferingUi: showBufferingUi,
                    FasterModel: GetStringOrNull(p, "fasterModel"),
                    Params: p),

            AppServerMethods.ModelSafetyBufferingUpdated => new UnknownNotification(method, p),

            AppServerMethods.TurnStarted => new TurnStartedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Turn: GetAny(p, JsonFieldNames.Turn),
                Params: p),

            AppServerMethods.HookStarted => (AppServerNotification?)TryMapHookStarted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.HookCompleted => (AppServerNotification?)TryMapHookCompleted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ItemAgentMessageDelta => new AgentMessageDeltaNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Delta: GetString(p, JsonFieldNames.Delta) ?? string.Empty,
                Params: p),

            AppServerMethods.ItemStarted => new ItemStartedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Item: GetAny(p, JsonFieldNames.Item),
                Params: p),

            AppServerMethods.ItemCompleted => new ItemCompletedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Item: GetAny(p, JsonFieldNames.Item),
                Params: p),

            AppServerMethods.TurnCompleted => new TurnCompletedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                Turn: GetAny(p, JsonFieldNames.Turn),
                Params: p),

            AppServerMethods.TurnModerationMetadata => new TurnModerationMetadataNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Metadata: GetAny(p, "metadata"),
                Params: p),

            AppServerMethods.TurnDiffUpdated => new TurnDiffUpdatedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Diff: GetString(p, JsonFieldNames.Diff) ?? string.Empty,
                Params: p),

            AppServerMethods.TurnPlanUpdated => new TurnPlanUpdatedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Explanation: GetStringOrNull(p, JsonFieldNames.Explanation),
                Plan: ParsePlan(p),
                Params: p),

            AppServerMethods.ItemPlanDelta => new PlanDeltaNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Delta: GetString(p, JsonFieldNames.Delta) ?? string.Empty,
                Params: p),

            AppServerMethods.CommandExecOutputDelta when
                TryGetRequiredString(p, JsonFieldNames.ProcessId, out var processId) &&
                TryGetRequiredString(p, "stream", out var stream) &&
                TryGetRequiredString(p, "deltaBase64", out var deltaBase64) &&
                TryGetRequiredBool(p, "capReached", out var capReached)
                => new CommandExecOutputDeltaNotification(
                    ProcessId: processId,
                    Stream: stream,
                    DeltaBase64: deltaBase64,
                    CapReached: capReached,
                    Params: p),

            AppServerMethods.RawResponseCompleted => (AppServerNotification?)TryMapRawResponseCompleted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.RawResponseItemCompleted => new RawResponseItemCompletedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Item: GetAny(p, JsonFieldNames.Item),
                Params: p),

            AppServerMethods.ItemCommandExecutionOutputDelta => (AppServerNotification?)TryMapCommandExecutionOutputDelta(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ItemCommandExecutionTerminalInteraction => new TerminalInteractionNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                ProcessId: GetString(p, JsonFieldNames.ProcessId) ?? string.Empty,
                Stdin: GetString(p, "stdin") ?? string.Empty,
                Params: p),

            AppServerMethods.ItemFileChangeOutputDelta => new FileChangeOutputDeltaNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Delta: GetString(p, JsonFieldNames.Delta) ?? string.Empty,
                Params: p),

            AppServerMethods.ItemMcpToolCallProgress => new McpToolCallProgressNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Message: GetString(p, JsonFieldNames.Message) ?? string.Empty,
                Params: p),

            AppServerMethods.McpServerOauthLoginCompleted => new McpServerOauthLoginCompletedNotification(
                Name: GetString(p, JsonFieldNames.Name) ?? string.Empty,
                ThreadId: GetStringOrNull(p, JsonFieldNames.ThreadId),
                Success: GetBool(p, JsonFieldNames.Success),
                Error: GetStringOrNull(p, JsonFieldNames.Error),
                Params: p),

            AppServerMethods.McpServerStartupStatusUpdated => (AppServerNotification?)TryMapMcpServerStartupStatusUpdated(p) ?? new UnknownNotification(method, p),

            AppServerMethods.AccountUpdated => (AppServerNotification?)TryMapAccountUpdated(p) ?? new UnknownNotification(method, p),

            AppServerMethods.AccountRateLimitsUpdated => new AccountRateLimitsUpdatedNotification(
                RateLimits: GetAny(p, JsonFieldNames.RateLimits),
                Params: p),

            AppServerMethods.ThreadEnvironmentConnected => (AppServerNotification?)TryMapThreadEnvironmentConnection(method, p) ?? new UnknownNotification(method, p),

            "thread/environment/disconnected" => (AppServerNotification?)TryMapThreadEnvironmentConnection(method, p) ?? new UnknownNotification(method, p),

            AppServerMethods.AppListUpdated => new AppListUpdatedNotification(
                apps: AppServerNotificationParsing.ParseAppsList(p),
                data: GetOptionalAny(p, JsonFieldNames.Data) ?? GetAny(p, JsonFieldNames.Apps),
                @params: p),

            AppServerMethods.RemoteControlStatusChanged when TryGetRequiredString(p, JsonFieldNames.Status, out var remoteControlStatus)
                => new RemoteControlStatusChangedNotification(
                    status: remoteControlStatus,
                    serverName: GetStringOrNull(p, JsonFieldNames.ServerName),
                    installationId: GetStringOrNull(p, JsonFieldNames.InstallationId),
                    environmentId: GetStringOrNull(p, JsonFieldNames.EnvironmentId),
                    @params: p),

            AppServerMethods.SkillsChanged => new SkillsChangedNotification(
                @params: p),

            AppServerMethods.ServerRequestResolved => (AppServerNotification?)TryMapServerRequestResolved(p) ?? new UnknownNotification(method, p),

            AppServerMethods.FsChanged => (AppServerNotification?)TryMapFsChanged(p) ?? new UnknownNotification(method, p),

            AppServerMethods.FuzzyFileSearchSessionUpdated => new FuzzyFileSearchSessionUpdatedNotification(
                sessionId: GetString(p, JsonFieldNames.SessionId) ?? string.Empty,
                query: GetString(p, JsonFieldNames.Query) ?? string.Empty,
                files: AppServerNotificationParsing.ParseFuzzyFileSearchResults(p),
                @params: p),

            AppServerMethods.FuzzyFileSearchSessionCompleted => new FuzzyFileSearchSessionCompletedNotification(
                sessionId: GetString(p, JsonFieldNames.SessionId) ?? string.Empty,
                @params: p),

            AppServerMethods.ItemAutoApprovalReviewStarted => (AppServerNotification?)TryMapAutoApprovalReviewStarted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ItemAutoApprovalReviewCompleted => (AppServerNotification?)TryMapAutoApprovalReviewCompleted(p) ?? new UnknownNotification(method, p),

            AppServerMethods.ItemReasoningSummaryTextDelta => new ReasoningSummaryTextDeltaNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Delta: GetString(p, JsonFieldNames.Delta) ?? string.Empty,
                SummaryIndex: GetInt64(p, JsonFieldNames.SummaryIndex),
                Params: p),

            AppServerMethods.ItemReasoningSummaryPartAdded => new ReasoningSummaryPartAddedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                SummaryIndex: GetInt64(p, JsonFieldNames.SummaryIndex),
                Params: p),

            AppServerMethods.ItemReasoningTextDelta => new ReasoningTextDeltaNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                ItemId: GetString(p, JsonFieldNames.ItemId) ?? string.Empty,
                Delta: GetString(p, JsonFieldNames.Delta) ?? string.Empty,
                ContentIndex: GetInt64(p, "contentIndex"),
                Params: p),

            AppServerMethods.ThreadCompacted => new ContextCompactedNotification(
                ThreadId: GetString(p, JsonFieldNames.ThreadId) ?? string.Empty,
                TurnId: GetString(p, JsonFieldNames.TurnId) ?? string.Empty,
                Params: p),

            "deprecationNotice" => new DeprecationNoticeNotification(
                Summary: GetString(p, JsonFieldNames.Summary) ?? string.Empty,
                Details: GetStringOrNull(p, JsonFieldNames.Details),
                Params: p),

            "configWarning" => new ConfigWarningNotification(
                Summary: GetString(p, JsonFieldNames.Summary) ?? string.Empty,
                Details: GetStringOrNull(p, JsonFieldNames.Details),
                Path: GetStringOrNull(p, JsonFieldNames.Path),
                Range: GetOptionalAny(p, "range"),
                Params: p),

            AppServerMethods.WindowsWorldWritableWarning => new WindowsWorldWritableWarningNotification(
                SamplePaths: GetStringArray(p, "samplePaths"),
                ExtraCount: GetInt32(p, "extraCount"),
                FailedScan: GetBool(p, "failedScan"),
                Params: p),

            AppServerMethods.WindowsSandboxSetupCompleted => new WindowsSandboxSetupCompletedNotification(
                Mode: GetString(p, JsonFieldNames.Mode) ?? string.Empty,
                Success: GetBool(p, JsonFieldNames.Success),
                Error: GetStringOrNull(p, JsonFieldNames.Error),
                Params: p),

            AppServerMethods.AccountLoginCompleted => new AccountLoginCompletedNotification(
                LoginId: GetStringOrNull(p, JsonFieldNames.LoginId),
                Success: GetBool(p, JsonFieldNames.Success),
                Error: GetStringOrNull(p, JsonFieldNames.Error),
                Params: p),

            _ => TryMapCodex155Notification(method, p) ?? TryMapCodex152Notification(method, p) ?? TryMapCodex149Notification(method, p) ?? new UnknownNotification(method, p)
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
        if (!obj.TryGetProperty(JsonFieldNames.Plan, out var prop) || prop.ValueKind != JsonValueKind.Array)
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
                Step: GetString(el, JsonFieldNames.Step) ?? string.Empty,
                Status: GetString(el, JsonFieldNames.Status) ?? string.Empty));
        }

        return list;
    }

    private static JsonElement EmptyObject()
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        return emptyDoc.RootElement.Clone();
    }
}
