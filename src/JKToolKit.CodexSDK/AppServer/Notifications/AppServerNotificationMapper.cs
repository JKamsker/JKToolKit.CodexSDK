using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

internal static class AppServerNotificationMapper
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

            "turn/started" => new TurnStartedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                Turn: GetAny(p, "turn"),
                Params: p),

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

            "rawResponseItem/completed" => new RawResponseItemCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Item: GetAny(p, "item"),
                Params: p),

            "item/commandExecution/outputDelta" => new CommandExecutionOutputDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                Params: p),

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

            "account/updated" => new AccountUpdatedNotification(
                AuthMode: GetStringOrNull(p, "authMode"),
                Params: p),

            "account/rateLimits/updated" => new AccountRateLimitsUpdatedNotification(
                RateLimits: GetAny(p, "rateLimits"),
                Params: p),

            "app/list/updated" => new AppListUpdatedNotification(
                apps: GetOptionalAny(p, "data") ?? GetAny(p, "apps"),
                @params: p),

            "fuzzyFileSearch/sessionUpdated" => new FuzzyFileSearchSessionUpdatedNotification(
                sessionId: GetString(p, "sessionId") ?? string.Empty,
                query: GetString(p, "query") ?? string.Empty,
                files: ParseFuzzyFileSearchResults(p),
                @params: p),

            "fuzzyFileSearch/sessionCompleted" => new FuzzyFileSearchSessionCompletedNotification(
                sessionId: GetString(p, "sessionId") ?? string.Empty,
                @params: p),

            "item/reasoning/summaryTextDelta" => new ReasoningSummaryTextDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                SummaryIndex: GetInt32(p, "summaryIndex"),
                Params: p),

            "item/reasoning/summaryPartAdded" => new ReasoningSummaryPartAddedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                SummaryIndex: GetInt32(p, "summaryIndex"),
                Params: p),

            "item/reasoning/textDelta" => new ReasoningTextDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                ContentIndex: GetInt32(p, "contentIndex"),
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

            "account/login/completed" => new AccountLoginCompletedNotification(
                LoginId: GetStringOrNull(p, "loginId"),
                Success: GetBool(p, "success"),
                Error: GetStringOrNull(p, "error"),
                Params: p),

            "authStatusChange" => new AuthStatusChangeNotification(
                AuthMethod: GetStringOrNull(p, "authMethod"),
                Params: p),

            "loginChatGptComplete" => new LoginChatGptCompleteNotification(
                LoginId: GetString(p, "loginId") ?? string.Empty,
                Success: GetBool(p, "success"),
                Error: GetStringOrNull(p, "error"),
                Params: p),

            "sessionConfigured" => new SessionConfiguredNotification(
                SessionId: GetString(p, "sessionId") ?? string.Empty,
                Model: GetString(p, "model") ?? string.Empty,
                ReasoningEffort: GetStringOrNull(p, "reasoningEffort"),
                HistoryLogId: GetInt64(p, "historyLogId"),
                HistoryEntryCount: GetInt32(p, "historyEntryCount"),
                InitialMessages: GetOptionalAny(p, "initialMessages"),
                RolloutPath: GetStringOrNull(p, "rolloutPath"),
                Params: p),

            _ => new UnknownNotification(method, p)
        };
    }

    private static string? GetString(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

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

    private static IReadOnlyList<FuzzyFileSearchResult> ParseFuzzyFileSearchResults(JsonElement obj)
    {
        if (!obj.TryGetProperty("files", out var filesProp) || filesProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FuzzyFileSearchResult>();
        }

        var list = new List<FuzzyFileSearchResult>();
        foreach (var item in filesProp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var root = GetString(item, "root") ?? string.Empty;
            var path = GetString(item, "path") ?? string.Empty;
            var fileName = GetString(item, "fileName") ?? GetString(item, "file_name") ?? string.Empty;

            uint score = 0;
            if (item.TryGetProperty("score", out var scoreProp) &&
                scoreProp.ValueKind == JsonValueKind.Number &&
                scoreProp.TryGetUInt32(out var s))
            {
                score = s;
            }

            IReadOnlyList<uint>? indices = null;
            if (item.TryGetProperty("indices", out var indicesProp) && indicesProp.ValueKind == JsonValueKind.Array)
            {
                var idx = new List<uint>();
                foreach (var i in indicesProp.EnumerateArray())
                {
                    if (i.ValueKind == JsonValueKind.Number && i.TryGetUInt32(out var n))
                    {
                        idx.Add(n);
                    }
                }

                indices = idx;
            }

            list.Add(new FuzzyFileSearchResult
            {
                Root = root,
                Path = path,
                FileName = fileName,
                Score = score,
                Indices = indices
            });
        }

        return list;
    }

    private static JsonElement EmptyObject()
    {
        using var emptyDoc = JsonDocument.Parse("{}");
        return emptyDoc.RootElement.Clone();
    }
}
