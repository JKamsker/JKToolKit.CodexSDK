using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents a command execution item emitted by a tool or the agent.
/// </summary>
public sealed record class CodexThreadItemCommandExecution(
    string Id,
    string Type,
    string Command,
    string WorkingDirectory,
    string? ProcessId,
    CodexCommandExecutionSource Source,
    CodexCommandExecutionStatus Status,
    IReadOnlyList<CodexCommandAction> CommandActions,
    string? AggregatedOutput,
    int? ExitCode,
    long? DurationMs,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a file change item.
/// </summary>
public sealed record class CodexThreadItemFileChange(
    string Id,
    string Type,
    IReadOnlyList<CodexFileUpdateChange> Changes,
    CodexPatchApplyStatus Status,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an MCP tool call item.
/// </summary>
public sealed record class CodexThreadItemMcpToolCall(
    string Id,
    string Type,
    string Server,
    string Tool,
    CodexMcpToolCallStatus Status,
    JsonElement Arguments,
    JsonElement? Result,
    string? ErrorMessage,
    long? DurationMs,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a dynamic tool call item.
/// </summary>
public sealed record class CodexThreadItemDynamicToolCall(
    string Id,
    string Type,
    string Tool,
    string Status,
    JsonElement Arguments,
    IReadOnlyList<JsonElement>? ContentItems,
    bool? Success,
    long? DurationMs,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a collaboration-tool call item.
/// </summary>
public sealed record class CodexThreadItemCollabAgentToolCall(
    string Id,
    string Type,
    string Tool,
    string Status,
    string SenderThreadId,
    IReadOnlyList<string> ReceiverThreadIds,
    string? Prompt,
    string? Model,
    CodexReasoningEffort? ReasoningEffort,
    IReadOnlyDictionary<string, CodexCollabAgentState> AgentsStates,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a web-search item.
/// </summary>
public sealed record class CodexThreadItemWebSearch(
    string Id,
    string Type,
    string Query,
    CodexWebSearchAction? Action,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an image-view item.
/// </summary>
public sealed record class CodexThreadItemImageView(
    string Id,
    string Type,
    string Path,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an image-generation item.
/// </summary>
public sealed record class CodexThreadItemImageGeneration(
    string Id,
    string Type,
    string Status,
    string Result,
    string? RevisedPrompt,
    string? SavedPath,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a review-mode item.
/// </summary>
public sealed record class CodexThreadItemEnteredReviewMode(
    string Id,
    string Type,
    string Review,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a review-mode exit item.
/// </summary>
public sealed record class CodexThreadItemExitedReviewMode(
    string Id,
    string Type,
    string Review,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a parsed command action discovered while analyzing command execution output.
/// </summary>
public sealed record class CodexCommandAction(string Command, string Type, string? Name, string? Path, string? Query);

/// <summary>
/// Represents a single file change.
/// </summary>
public sealed record class CodexFileUpdateChange(string Path, CodexPatchChangeKind Kind, string Diff);

/// <summary>
/// Represents the patch change-kind union used by file-change items.
/// </summary>
public sealed record class CodexPatchChangeKind(CodexPatchChangeKindType Type, string? MovePath = null);

/// <summary>
/// Enumerates the known patch change kinds.
/// </summary>
public enum CodexPatchChangeKindType
{
    /// <summary>
    /// The change kind is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The patch adds a new file.
    /// </summary>
    Add,

    /// <summary>
    /// The patch deletes an existing file.
    /// </summary>
    Delete,

    /// <summary>
    /// The patch updates an existing file.
    /// </summary>
    Update
}

/// <summary>
/// Enumerates the command execution sources exposed by upstream thread history.
/// </summary>
public enum CodexCommandExecutionSource
{
    /// <summary>
    /// The command execution source is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The agent initiated the command.
    /// </summary>
    Agent,

    /// <summary>
    /// The command came from an explicit user shell interaction.
    /// </summary>
    UserShell,

    /// <summary>
    /// The command was started during unified exec bootstrap.
    /// </summary>
    UnifiedExecStartup,

    /// <summary>
    /// The command was started during unified exec interaction.
    /// </summary>
    UnifiedExecInteraction
}

internal static class CodexCommandExecutionSourceExtensions
{
    public static CodexCommandExecutionSource Parse(string? value) => value switch
    {
        null or "agent" => CodexCommandExecutionSource.Agent,
        "userShell" => CodexCommandExecutionSource.UserShell,
        "unifiedExecStartup" => CodexCommandExecutionSource.UnifiedExecStartup,
        "unifiedExecInteraction" => CodexCommandExecutionSource.UnifiedExecInteraction,
        _ => CodexCommandExecutionSource.Unknown
    };
}

/// <summary>
/// Represents the last known state of a collaboration target agent.
/// </summary>
public sealed record class CodexCollabAgentState(string Status, string? Message);

/// <summary>
/// Represents the web-search action union surfaced in thread history.
/// </summary>
public sealed record class CodexWebSearchAction(
    CodexWebSearchActionKind Kind,
    string? Query = null,
    IReadOnlyList<string>? Queries = null,
    string? Url = null,
    string? Pattern = null);

/// <summary>
/// Enumerates the web-search action kinds surfaced in thread history.
/// </summary>
public enum CodexWebSearchActionKind
{
    /// <summary>
    /// The action kind is not recognized.
    /// </summary>
    Other,

    /// <summary>
    /// The action performed a search query.
    /// </summary>
    Search,

    /// <summary>
    /// The action opened a page.
    /// </summary>
    OpenPage,

    /// <summary>
    /// The action searched for text within a page.
    /// </summary>
    FindInPage
}

/// <summary>
/// Enumerates the command execution statuses produced by Codex.
/// </summary>
public enum CodexCommandExecutionStatus
{
    /// <summary>
    /// The status is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The command execution is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The command execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The command execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The command execution was declined.
    /// </summary>
    Declined
}

internal static class CodexCommandExecutionStatusExtensions
{
    public static CodexCommandExecutionStatus Parse(string? value) => value switch
    {
        "inProgress" or "in_progress" => CodexCommandExecutionStatus.InProgress,
        "completed" => CodexCommandExecutionStatus.Completed,
        "failed" => CodexCommandExecutionStatus.Failed,
        "declined" => CodexCommandExecutionStatus.Declined,
        _ => CodexCommandExecutionStatus.Unknown
    };
}

/// <summary>
/// Enumerates the patch apply statuses surfaced by file change items.
/// </summary>
public enum CodexPatchApplyStatus
{
    /// <summary>
    /// The status is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The patch is currently being applied.
    /// </summary>
    InProgress,

    /// <summary>
    /// The patch was applied successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The patch failed to apply.
    /// </summary>
    Failed,

    /// <summary>
    /// The patch application was declined.
    /// </summary>
    Declined
}

internal static class CodexPatchApplyStatusExtensions
{
    public static CodexPatchApplyStatus Parse(string? value) => value switch
    {
        "inProgress" or "in_progress" => CodexPatchApplyStatus.InProgress,
        "completed" => CodexPatchApplyStatus.Completed,
        "failed" => CodexPatchApplyStatus.Failed,
        "declined" => CodexPatchApplyStatus.Declined,
        _ => CodexPatchApplyStatus.Unknown
    };
}

/// <summary>
/// Enumerates the MCP tool call states exposed by the upstream code.
/// </summary>
public enum CodexMcpToolCallStatus
{
    /// <summary>
    /// The MCP tool call status is not recognized.
    /// </summary>
    Unknown,

    /// <summary>
    /// The MCP tool call is in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The MCP tool call completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The MCP tool call failed.
    /// </summary>
    Failed
}

internal static class CodexMcpToolCallStatusExtensions
{
    public static CodexMcpToolCallStatus Parse(string? value) => value switch
    {
        "inProgress" or "in_progress" => CodexMcpToolCallStatus.InProgress,
        "completed" => CodexMcpToolCallStatus.Completed,
        "failed" => CodexMcpToolCallStatus.Failed,
        _ => CodexMcpToolCallStatus.Unknown
    };
}
