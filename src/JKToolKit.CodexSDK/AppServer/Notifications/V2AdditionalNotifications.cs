using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record ErrorNotification(
    string ThreadId,
    string TurnId,
    JsonElement Error,
    bool WillRetry,
    JsonElement Params)
    : AppServerNotification("error", Params);

public sealed record ThreadStartedNotification(
    JsonElement Thread,
    JsonElement Params)
    : AppServerNotification("thread/started", Params)
{
    public string? ThreadId =>
        Thread.ValueKind == JsonValueKind.Object &&
        Thread.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}

public sealed record ThreadNameUpdatedNotification(
    string ThreadId,
    string? ThreadName,
    JsonElement Params)
    : AppServerNotification("thread/name/updated", Params);

public sealed record ThreadTokenUsageUpdatedNotification(
    string ThreadId,
    string TurnId,
    JsonElement TokenUsage,
    JsonElement Params)
    : AppServerNotification("thread/tokenUsage/updated", Params);

public sealed record TurnStartedNotification(
    string ThreadId,
    JsonElement Turn,
    JsonElement Params)
    : AppServerNotification("turn/started", Params)
{
    public string? TurnId =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}

public sealed record TurnDiffUpdatedNotification(
    string ThreadId,
    string TurnId,
    string Diff,
    JsonElement Params)
    : AppServerNotification("turn/diff/updated", Params);

public sealed record TurnPlanStep(
    string Step,
    string Status);

public sealed record TurnPlanUpdatedNotification(
    string ThreadId,
    string TurnId,
    string? Explanation,
    IReadOnlyList<TurnPlanStep> Plan,
    JsonElement Params)
    : AppServerNotification("turn/plan/updated", Params);

public sealed record PlanDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    JsonElement Params)
    : AppServerNotification("item/plan/delta", Params);

public sealed record RawResponseItemCompletedNotification(
    string ThreadId,
    string TurnId,
    JsonElement Item,
    JsonElement Params)
    : AppServerNotification("rawResponseItem/completed", Params);

public sealed record CommandExecutionOutputDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    JsonElement Params)
    : AppServerNotification("item/commandExecution/outputDelta", Params);

public sealed record TerminalInteractionNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string ProcessId,
    string Stdin,
    JsonElement Params)
    : AppServerNotification("item/commandExecution/terminalInteraction", Params);

public sealed record FileChangeOutputDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    JsonElement Params)
    : AppServerNotification("item/fileChange/outputDelta", Params);

public sealed record McpToolCallProgressNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Message,
    JsonElement Params)
    : AppServerNotification("item/mcpToolCall/progress", Params);

public sealed record McpServerOauthLoginCompletedNotification(
    string Name,
    bool Success,
    string? Error,
    JsonElement Params)
    : AppServerNotification("mcpServer/oauthLogin/completed", Params);

public sealed record AccountUpdatedNotification(
    string? AuthMode,
    JsonElement Params)
    : AppServerNotification("account/updated", Params);

public sealed record AccountRateLimitsUpdatedNotification(
    JsonElement RateLimits,
    JsonElement Params)
    : AppServerNotification("account/rateLimits/updated", Params);

public sealed record ReasoningSummaryTextDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    int SummaryIndex,
    JsonElement Params)
    : AppServerNotification("item/reasoning/summaryTextDelta", Params);

public sealed record ReasoningSummaryPartAddedNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    int SummaryIndex,
    JsonElement Params)
    : AppServerNotification("item/reasoning/summaryPartAdded", Params);

public sealed record ReasoningTextDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    int ContentIndex,
    JsonElement Params)
    : AppServerNotification("item/reasoning/textDelta", Params);

public sealed record ContextCompactedNotification(
    string ThreadId,
    string TurnId,
    JsonElement Params)
    : AppServerNotification("thread/compacted", Params);

public sealed record DeprecationNoticeNotification(
    string Summary,
    string? Details,
    JsonElement Params)
    : AppServerNotification("deprecationNotice", Params);

public sealed record ConfigWarningNotification(
    string Summary,
    string? Details,
    string? Path,
    JsonElement? Range,
    JsonElement Params)
    : AppServerNotification("configWarning", Params);

public sealed record WindowsWorldWritableWarningNotification(
    IReadOnlyList<string> SamplePaths,
    int ExtraCount,
    bool FailedScan,
    JsonElement Params)
    : AppServerNotification("windows/worldWritableWarning", Params);

public sealed record AccountLoginCompletedNotification(
    string? LoginId,
    bool Success,
    string? Error,
    JsonElement Params)
    : AppServerNotification("account/login/completed", Params);

public sealed record AuthStatusChangeNotification(
    string? AuthMethod,
    JsonElement Params)
    : AppServerNotification("authStatusChange", Params);

public sealed record LoginChatGptCompleteNotification(
    string LoginId,
    bool Success,
    string? Error,
    JsonElement Params)
    : AppServerNotification("loginChatGptComplete", Params);

public sealed record SessionConfiguredNotification(
    string SessionId,
    string Model,
    string? ReasoningEffort,
    long HistoryLogId,
    int HistoryEntryCount,
    JsonElement? InitialMessages,
    string RolloutPath,
    JsonElement Params)
    : AppServerNotification("sessionConfigured", Params);

