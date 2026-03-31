using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents an item emitted during a turn in <c>thread/read</c> when turns are materialized.
/// </summary>
public abstract record class CodexThreadItem(string Id, string Type, JsonElement Raw)
{
    /// <summary>
    /// Gets the upstream item identifier.
    /// </summary>
    public string Id { get; } = Id ?? string.Empty;

    /// <summary>
    /// Gets the upstream item type discriminator.
    /// </summary>
    public string Type { get; } = Type ?? string.Empty;

    /// <summary>
    /// Gets the raw JSON payload for the item.
    /// </summary>
    public JsonElement Raw { get; } = Raw;
}

/// <summary>
/// Represents a reasoning-summary item produced by the agent.
/// </summary>
public sealed record class CodexThreadItemReasoning(
    string Id,
    string Type,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string>? Content,
    string? EncryptedContent,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an agent message item.
/// </summary>
public sealed record class CodexThreadItemAgentMessage(
    string Id,
    string Type,
    string Text,
    string? Phase,
    JsonElement? MemoryCitation,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a command execution item emitted by a tool or the agent.
/// </summary>
public sealed record class CodexThreadItemCommandExecution(
    string Id,
    string Type,
    string Command,
    string? WorkingDirectory,
    string? ProcessId,
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
    JsonElement? Arguments,
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
    string? Status,
    JsonElement? Arguments,
    IReadOnlyList<JsonElement>? ContentItems,
    bool? Success,
    long? DurationMs,
    JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents an uncategorized thread item.
/// </summary>
public sealed record class CodexThreadItemUnknown(string Id, string Type, JsonElement Raw)
    : CodexThreadItem(Id, Type, Raw);

/// <summary>
/// Represents a parsed command action discovered while analyzing command execution output.
/// </summary>
public sealed record class CodexCommandAction(string Command, string Type, string? Name, string? Path, string? Query);

/// <summary>
/// Represents a single file change.
/// </summary>
public sealed record class CodexFileUpdateChange(string Path, string Kind, string Diff);

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

internal static class CodexThreadItemParser
{
    public static IReadOnlyList<CodexThreadItem> ParseItems(JsonElement turnElement)
    {
        var array = CodexAppServerClientJson.TryGetArray(turnElement, "items");
        if (array is null)
        {
            return Array.Empty<CodexThreadItem>();
        }

        var list = new List<CodexThreadItem>();
        foreach (var element in array.Value.EnumerateArray())
        {
            list.Add(Parse(element));
        }

        return list;
    }

    public static CodexThreadItem Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new CodexThreadItemUnknown(string.Empty, string.Empty, element.Clone());
        }

        var type = CodexAppServerClientJson.GetStringOrNull(element, "type") ?? string.Empty;
        var id = CodexAppServerClientJson.GetStringOrNull(element, "id") ?? string.Empty;
        var raw = element.Clone();

        return type switch
        {
            "reasoning" => ParseReasoning(id, type, element, raw),
            "agentMessage" => ParseAgentMessage(id, type, element, raw),
            "commandExecution" => ParseCommandExecution(id, type, element, raw),
            "fileChange" => ParseFileChange(id, type, element, raw),
            "mcpToolCall" => ParseMcpToolCall(id, type, element, raw),
            "dynamicToolCall" => ParseDynamicToolCall(id, type, element, raw),
            _ => new CodexThreadItemUnknown(id, type, raw)
        };
    }

    private static CodexThreadItemReasoning ParseReasoning(string id, string type, JsonElement element, JsonElement raw)
    {
        var summary = GetStringList(element, "summary");
        var content = GetStringList(element, "content");
        var encrypted = CodexAppServerClientJson.GetStringOrNull(element, "encrypted_content");
        return new CodexThreadItemReasoning(id, type, summary, content.Count > 0 ? content : null, encrypted, raw);
    }

    private static CodexThreadItemAgentMessage ParseAgentMessage(string id, string type, JsonElement element, JsonElement raw)
    {
        var text = CodexAppServerClientJson.GetStringOrNull(element, "text") ?? string.Empty;
        var phase = CodexAppServerClientJson.GetStringOrNull(element, "phase");
        var memoryCitation = element.TryGetProperty("memoryCitation", out var mem) ? mem.Clone() : (JsonElement?)null;
        return new CodexThreadItemAgentMessage(id, type, text, phase, memoryCitation, raw);
    }

    private static CodexThreadItemCommandExecution ParseCommandExecution(string id, string type, JsonElement element, JsonElement raw)
    {
        var command = CodexAppServerClientJson.GetStringOrNull(element, "command") ?? string.Empty;
        var cwd = CodexAppServerClientJson.GetStringOrNull(element, "cwd");
        var processId = CodexAppServerClientJson.GetStringOrNull(element, "processId");
        var status = CodexCommandExecutionStatusExtensions.Parse(CodexAppServerClientJson.GetStringOrNull(element, "status"));
        var actions = ParseCommandActions(element);
        var aggregatedOutput = CodexAppServerClientJson.GetStringOrNull(element, "aggregatedOutput");
        var exitCode = CodexAppServerClientJson.GetInt32OrNull(element, "exitCode");
        var durationMs = CodexAppServerClientJson.GetInt64OrNull(element, "durationMs");
        return new CodexThreadItemCommandExecution(id, type, command, cwd, processId, status, actions, aggregatedOutput, exitCode, durationMs, raw);
    }

    private static CodexThreadItemFileChange ParseFileChange(string id, string type, JsonElement element, JsonElement raw)
    {
        var status = CodexPatchApplyStatusExtensions.Parse(CodexAppServerClientJson.GetStringOrNull(element, "status"));
        var changes = ParseFileChanges(element);
        return new CodexThreadItemFileChange(id, type, changes, status, raw);
    }

    private static CodexThreadItemMcpToolCall ParseMcpToolCall(string id, string type, JsonElement element, JsonElement raw)
    {
        var server = CodexAppServerClientJson.GetStringOrNull(element, "server") ?? string.Empty;
        var tool = CodexAppServerClientJson.GetStringOrNull(element, "tool") ?? string.Empty;
        var status = CodexMcpToolCallStatusExtensions.Parse(CodexAppServerClientJson.GetStringOrNull(element, "status"));
        var arguments = element.TryGetProperty("arguments", out var args) ? args.Clone() : (JsonElement?)null;
        var result = element.TryGetProperty("result", out var res) ? res.Clone() : (JsonElement?)null;
        var errorMessage = ExtractErrorMessage(element);
        var durationMs = CodexAppServerClientJson.GetInt64OrNull(element, "durationMs");
        return new CodexThreadItemMcpToolCall(id, type, server, tool, status, arguments, result, errorMessage, durationMs, raw);
    }

    private static CodexThreadItemDynamicToolCall ParseDynamicToolCall(string id, string type, JsonElement element, JsonElement raw)
    {
        var tool = CodexAppServerClientJson.GetStringOrNull(element, "tool") ?? string.Empty;
        var status = CodexAppServerClientJson.GetStringOrNull(element, "status");
        var arguments = element.TryGetProperty("arguments", out var args) ? args.Clone() : (JsonElement?)null;
        var contentItems = element.TryGetProperty("contentItems", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.Clone().EnumerateArray().Select(e => e.Clone()).ToList()
            : null;
        var success = element.TryGetProperty("success", out var successValue)
            && successValue.ValueKind == JsonValueKind.True
            ? true
            : successValue.ValueKind == JsonValueKind.False ? false : (bool?)null;
        var durationMs = CodexAppServerClientJson.GetInt64OrNull(element, "durationMs");
        return new CodexThreadItemDynamicToolCall(id, type, tool, status, arguments, contentItems, success, durationMs, raw);
    }

    private static IReadOnlyList<CodexCommandAction> ParseCommandActions(JsonElement element)
    {
        if (element.TryGetProperty("commandActions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            var list = new List<CodexCommandAction>();
            foreach (var action in actions.EnumerateArray())
            {
                if (action.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                list.Add(new CodexCommandAction(
                    CodexAppServerClientJson.GetStringOrNull(action, "command") ?? string.Empty,
                    CodexAppServerClientJson.GetStringOrNull(action, "type") ?? string.Empty,
                    CodexAppServerClientJson.GetStringOrNull(action, "name"),
                    CodexAppServerClientJson.GetStringOrNull(action, "path"),
                    CodexAppServerClientJson.GetStringOrNull(action, "query")));
            }

            return list;
        }

        return Array.Empty<CodexCommandAction>();
    }

    private static IReadOnlyList<CodexFileUpdateChange> ParseFileChanges(JsonElement element)
    {
        if (element.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
        {
            var list = new List<CodexFileUpdateChange>();
            foreach (var change in changes.EnumerateArray())
            {
                if (change.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                list.Add(new CodexFileUpdateChange(
                    CodexAppServerClientJson.GetStringOrNull(change, "path") ?? string.Empty,
                    CodexAppServerClientJson.GetStringOrNull(change, "kind") ?? string.Empty,
                    CodexAppServerClientJson.GetStringOrNull(change, "diff") ?? string.Empty));
            }

            return list;
        }

        return Array.Empty<CodexFileUpdateChange>();
    }

    private static IReadOnlyList<string> GetStringList(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    list.Add(item.GetString() ?? string.Empty);
                }
            }

            return list;
        }

        return Array.Empty<string>();
    }

    private static string? ExtractErrorMessage(JsonElement element)
    {
        if (!element.TryGetProperty("error", out var error))
        {
            return null;
        }

        if (error.ValueKind == JsonValueKind.Object)
        {
            return CodexAppServerClientJson.GetStringOrNull(error, "message");
        }

        return error.ValueKind == JsonValueKind.String ? error.GetString() : null;
    }
}
