using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class ErrorNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement Error { get; }
    public bool WillRetry { get; }

    public ErrorNotification(string ThreadId, string TurnId, JsonElement Error, bool WillRetry, JsonElement Params)
        : base("error", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Error = Error;
        this.WillRetry = WillRetry;
    }
}

public sealed record class ThreadStartedNotification : AppServerNotification
{
    public JsonElement Thread { get; }

    public ThreadStartedNotification(JsonElement Thread, JsonElement Params)
        : base("thread/started", Params)
    {
        this.Thread = Thread;
    }

    public string? ThreadId =>
        Thread.ValueKind == JsonValueKind.Object &&
        Thread.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}

public sealed record class ThreadNameUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string? ThreadName { get; }

    public ThreadNameUpdatedNotification(string ThreadId, string? ThreadName, JsonElement Params)
        : base("thread/name/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.ThreadName = ThreadName;
    }
}

public sealed record class ThreadTokenUsageUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement TokenUsage { get; }

    public ThreadTokenUsageUpdatedNotification(string ThreadId, string TurnId, JsonElement TokenUsage, JsonElement Params)
        : base("thread/tokenUsage/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.TokenUsage = TokenUsage;
    }
}

public sealed record class TurnStartedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public JsonElement Turn { get; }

    public TurnStartedNotification(string ThreadId, JsonElement Turn, JsonElement Params)
        : base("turn/started", Params)
    {
        this.ThreadId = ThreadId;
        this.Turn = Turn;
    }

    public string? TurnId =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
}

public sealed record class TurnDiffUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string Diff { get; }

    public TurnDiffUpdatedNotification(string ThreadId, string TurnId, string Diff, JsonElement Params)
        : base("turn/diff/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Diff = Diff;
    }
}

public sealed record class TurnPlanStep
{
    public string Step { get; }
    public string Status { get; }

    public TurnPlanStep(string Step, string Status)
    {
        this.Step = Step;
        this.Status = Status;
    }
}

public sealed record class TurnPlanUpdatedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string? Explanation { get; }
    public IReadOnlyList<TurnPlanStep> Plan { get; }

    public TurnPlanUpdatedNotification(
        string ThreadId,
        string TurnId,
        string? Explanation,
        IReadOnlyList<TurnPlanStep> Plan,
        JsonElement Params)
        : base("turn/plan/updated", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Explanation = Explanation;
        this.Plan = Plan ?? throw new ArgumentNullException(nameof(Plan));
    }
}

public sealed record class PlanDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }

    public PlanDeltaNotification(string ThreadId, string TurnId, string ItemId, string Delta, JsonElement Params)
        : base("item/plan/delta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

public sealed record class RawResponseItemCompletedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public JsonElement Item { get; }

    public RawResponseItemCompletedNotification(string ThreadId, string TurnId, JsonElement Item, JsonElement Params)
        : base("rawResponseItem/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.Item = Item;
    }
}

public sealed record class CommandExecutionOutputDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }

    public CommandExecutionOutputDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        JsonElement Params)
        : base("item/commandExecution/outputDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

public sealed record class TerminalInteractionNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string ProcessId { get; }
    public string Stdin { get; }

    public TerminalInteractionNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string ProcessId,
        string Stdin,
        JsonElement Params)
        : base("item/commandExecution/terminalInteraction", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.ProcessId = ProcessId;
        this.Stdin = Stdin;
    }
}

public sealed record class FileChangeOutputDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }

    public FileChangeOutputDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        JsonElement Params)
        : base("item/fileChange/outputDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
    }
}

public sealed record class McpToolCallProgressNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Message { get; }

    public McpToolCallProgressNotification(string ThreadId, string TurnId, string ItemId, string Message, JsonElement Params)
        : base("item/mcpToolCall/progress", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Message = Message;
    }
}

public sealed record class McpServerOauthLoginCompletedNotification : AppServerNotification
{
    public string Name { get; }
    public bool Success { get; }
    public string? Error { get; }

    public McpServerOauthLoginCompletedNotification(string Name, bool Success, string? Error, JsonElement Params)
        : base("mcpServer/oauthLogin/completed", Params)
    {
        this.Name = Name;
        this.Success = Success;
        this.Error = Error;
    }
}

public sealed record class AccountUpdatedNotification : AppServerNotification
{
    public string? AuthMode { get; }

    public AccountUpdatedNotification(string? AuthMode, JsonElement Params)
        : base("account/updated", Params)
    {
        this.AuthMode = AuthMode;
    }
}

public sealed record class AccountRateLimitsUpdatedNotification : AppServerNotification
{
    public JsonElement RateLimits { get; }

    public AccountRateLimitsUpdatedNotification(JsonElement RateLimits, JsonElement Params)
        : base("account/rateLimits/updated", Params)
    {
        this.RateLimits = RateLimits;
    }
}

public sealed record class ReasoningSummaryTextDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }
    public int SummaryIndex { get; }

    public ReasoningSummaryTextDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryTextDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
        this.SummaryIndex = SummaryIndex;
    }
}

public sealed record class ReasoningSummaryPartAddedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public int SummaryIndex { get; }

    public ReasoningSummaryPartAddedNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        int SummaryIndex,
        JsonElement Params)
        : base("item/reasoning/summaryPartAdded", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.SummaryIndex = SummaryIndex;
    }
}

public sealed record class ReasoningTextDeltaNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Delta { get; }
    public int ContentIndex { get; }

    public ReasoningTextDeltaNotification(
        string ThreadId,
        string TurnId,
        string ItemId,
        string Delta,
        int ContentIndex,
        JsonElement Params)
        : base("item/reasoning/textDelta", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Delta = Delta;
        this.ContentIndex = ContentIndex;
    }
}

public sealed record class ContextCompactedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }

    public ContextCompactedNotification(string ThreadId, string TurnId, JsonElement Params)
        : base("thread/compacted", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
    }
}

public sealed record class DeprecationNoticeNotification : AppServerNotification
{
    public string Summary { get; }
    public string? Details { get; }

    public DeprecationNoticeNotification(string Summary, string? Details, JsonElement Params)
        : base("deprecationNotice", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
    }
}

public sealed record class ConfigWarningNotification : AppServerNotification
{
    public string Summary { get; }
    public string? Details { get; }
    public string? Path { get; }
    public JsonElement? Range { get; }

    public ConfigWarningNotification(string Summary, string? Details, string? Path, JsonElement? Range, JsonElement Params)
        : base("configWarning", Params)
    {
        this.Summary = Summary;
        this.Details = Details;
        this.Path = Path;
        this.Range = Range;
    }
}

public sealed record class WindowsWorldWritableWarningNotification : AppServerNotification
{
    public IReadOnlyList<string> SamplePaths { get; }
    public int ExtraCount { get; }
    public bool FailedScan { get; }

    public WindowsWorldWritableWarningNotification(
        IReadOnlyList<string> SamplePaths,
        int ExtraCount,
        bool FailedScan,
        JsonElement Params)
        : base("windows/worldWritableWarning", Params)
    {
        this.SamplePaths = SamplePaths ?? throw new ArgumentNullException(nameof(SamplePaths));
        this.ExtraCount = ExtraCount;
        this.FailedScan = FailedScan;
    }
}

public sealed record class AccountLoginCompletedNotification : AppServerNotification
{
    public string? LoginId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public AccountLoginCompletedNotification(string? LoginId, bool Success, string? Error, JsonElement Params)
        : base("account/login/completed", Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}

public sealed record class AuthStatusChangeNotification : AppServerNotification
{
    public string? AuthMethod { get; }

    public AuthStatusChangeNotification(string? AuthMethod, JsonElement Params)
        : base("authStatusChange", Params)
    {
        this.AuthMethod = AuthMethod;
    }
}

public sealed record class LoginChatGptCompleteNotification : AppServerNotification
{
    public string LoginId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public LoginChatGptCompleteNotification(string LoginId, bool Success, string? Error, JsonElement Params)
        : base("loginChatGptComplete", Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}

public sealed record class SessionConfiguredNotification : AppServerNotification
{
    public string SessionId { get; }
    public string Model { get; }
    public string? ReasoningEffort { get; }
    public long HistoryLogId { get; }
    public int HistoryEntryCount { get; }
    public JsonElement? InitialMessages { get; }
    public string RolloutPath { get; }

    public SessionConfiguredNotification(
        string SessionId,
        string Model,
        string? ReasoningEffort,
        long HistoryLogId,
        int HistoryEntryCount,
        JsonElement? InitialMessages,
        string RolloutPath,
        JsonElement Params)
        : base("sessionConfigured", Params)
    {
        this.SessionId = SessionId;
        this.Model = Model;
        this.ReasoningEffort = ReasoningEffort;
        this.HistoryLogId = HistoryLogId;
        this.HistoryEntryCount = HistoryEntryCount;
        this.InitialMessages = InitialMessages;
        this.RolloutPath = RolloutPath;
    }
}
