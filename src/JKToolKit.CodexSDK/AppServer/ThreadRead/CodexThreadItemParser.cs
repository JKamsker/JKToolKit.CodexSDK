using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

using static CodexThreadItemParserJson;

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

        var item = type switch
        {
            "userMessage" => ParseUserMessage(id, type, element, raw),
            "hookPrompt" => ParseHookPrompt(id, type, element, raw),
            "agentMessage" => ParseAgentMessage(id, type, element, raw),
            "plan" => ParsePlan(id, type, element, raw),
            "reasoning" => ParseReasoning(id, type, element, raw),
            "commandExecution" => ParseCommandExecution(id, type, element, raw),
            "fileChange" => ParseFileChange(id, type, element, raw),
            "mcpToolCall" => ParseMcpToolCall(id, type, element, raw),
            "dynamicToolCall" => ParseDynamicToolCall(id, type, element, raw),
            "collabAgentToolCall" => ParseCollabAgentToolCall(id, type, element, raw),
            "webSearch" => ParseWebSearch(id, type, element, raw),
            "imageView" => ParseImageView(id, type, element, raw),
            "imageGeneration" => ParseImageGeneration(id, type, element, raw),
            "enteredReviewMode" => ParseEnteredReviewMode(id, type, element, raw),
            "exitedReviewMode" => ParseExitedReviewMode(id, type, element, raw),
            "contextCompaction" => new CodexThreadItemContextCompaction(id, type, raw),
            _ => null
        };

        return item ?? new CodexThreadItemUnknown(id, type, raw);
    }

    private static CodexThreadItem? ParseUserMessage(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredArray(element, "content", out var content)
            ? new CodexThreadItemUserMessage(id, type, CloneElements(content), raw)
            : null;

    private static CodexThreadItem? ParseHookPrompt(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredArray(element, "fragments", out var fragments))
        {
            return null;
        }

        var parsed = new List<CodexHookPromptFragment>();
        foreach (var fragment in fragments.EnumerateArray())
        {
            if (!TryGetRequiredString(fragment, "text", out var text) ||
                !TryGetRequiredString(fragment, "hookRunId", out var hookRunId))
            {
                return null;
            }

            parsed.Add(new CodexHookPromptFragment(text, hookRunId));
        }

        return new CodexThreadItemHookPrompt(id, type, parsed, raw);
    }

    private static CodexThreadItem? ParseAgentMessage(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "text", out var text) &&
        TryGetOptionalString(element, "phase", out var phase)
            ? new CodexThreadItemAgentMessage(
                id,
                type,
                text,
                phase,
                element.TryGetProperty("memoryCitation", out var memoryCitation) ? memoryCitation.Clone() : (JsonElement?)null,
                raw)
            : null;

    private static CodexThreadItem? ParsePlan(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "text", out var text)
            ? new CodexThreadItemPlan(id, type, text, raw)
            : null;

    private static CodexThreadItem? ParseReasoning(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetStringArray(element, "summary", out var summary) ||
            !TryGetStringArray(element, "content", out var content) ||
            !TryGetOptionalString(element, "encrypted_content", out var encryptedContent))
        {
            return null;
        }

        return new CodexThreadItemReasoning(id, type, summary, content.Count > 0 ? content : null, encryptedContent, raw);
    }

    private static CodexThreadItem? ParseCommandExecution(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, "command", out var command) ||
            !TryGetRequiredString(element, "cwd", out var cwd) ||
            !TryGetRequiredString(element, "status", out var statusValue) ||
            !TryGetOptionalString(element, "processId", out var processId) ||
            !TryGetOptionalString(element, "source", out var sourceValue) ||
            !TryGetOptionalString(element, "aggregatedOutput", out var aggregatedOutput) ||
            !TryGetOptionalInt32(element, "exitCode", out var exitCode) ||
            !TryGetOptionalInt64(element, "durationMs", out var durationMs) ||
            !TryParseCommandActions(element, out var actions))
        {
            return null;
        }

        return new CodexThreadItemCommandExecution(
            id,
            type,
            command,
            cwd,
            processId,
            CodexCommandExecutionSourceExtensions.Parse(sourceValue),
            CodexCommandExecutionStatusExtensions.Parse(statusValue),
            actions,
            aggregatedOutput,
            exitCode,
            durationMs,
            raw);
    }

    private static CodexThreadItem? ParseFileChange(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, "status", out var statusValue) ||
            !TryParseFileChanges(element, out var changes))
        {
            return null;
        }

        return new CodexThreadItemFileChange(
            id,
            type,
            changes,
            CodexPatchApplyStatusExtensions.Parse(statusValue),
            raw);
    }

    private static CodexThreadItem? ParseMcpToolCall(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, "server", out var server) ||
            !TryGetRequiredString(element, "tool", out var tool) ||
            !TryGetRequiredString(element, "status", out var statusValue) ||
            !TryGetRequiredElement(element, "arguments", out var arguments) ||
            !TryGetOptionalInt64(element, "durationMs", out var durationMs))
        {
            return null;
        }

        var result = element.TryGetProperty("result", out var resultValue) ? resultValue.Clone() : (JsonElement?)null;
        return new CodexThreadItemMcpToolCall(
            id,
            type,
            server,
            tool,
            CodexMcpToolCallStatusExtensions.Parse(statusValue),
            arguments,
            result,
            ExtractErrorMessage(element),
            durationMs,
            raw);
    }

    private static CodexThreadItem? ParseDynamicToolCall(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, "tool", out var tool) ||
            !TryGetRequiredString(element, "status", out var status) ||
            !TryGetRequiredElement(element, "arguments", out var arguments) ||
            !TryGetOptionalArray(element, "contentItems", out var contentItemsArray) ||
            !TryGetOptionalBool(element, "success", out var success) ||
            !TryGetOptionalInt64(element, "durationMs", out var durationMs))
        {
            return null;
        }

        return new CodexThreadItemDynamicToolCall(
            id,
            type,
            tool,
            status,
            arguments,
            contentItemsArray is { } contentItems ? CloneElements(contentItems) : null,
            success,
            durationMs,
            raw);
    }

    private static CodexThreadItem? ParseCollabAgentToolCall(string id, string type, JsonElement element, JsonElement raw)
    {
        if (!TryGetRequiredString(element, "tool", out var tool) ||
            !TryGetRequiredString(element, "status", out var status) ||
            !TryGetRequiredString(element, "senderThreadId", out var senderThreadId) ||
            !TryGetRequiredArray(element, "receiverThreadIds", out var receiverThreadIdsArray) ||
            !TryGetOptionalString(element, "prompt", out var prompt) ||
            !TryGetOptionalString(element, "model", out var model) ||
            !TryGetOptionalString(element, "reasoningEffort", out var reasoningEffortValue) ||
            !TryGetRequiredObject(element, "agentsStates", out var agentsStatesObject))
        {
            return null;
        }

        var receiverThreadIds = receiverThreadIdsArray.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .ToArray();
        if (receiverThreadIds.Any(item => string.IsNullOrWhiteSpace(item)))
        {
            return null;
        }

        var agentsStates = new Dictionary<string, CodexCollabAgentState>(StringComparer.Ordinal);
        foreach (var property in agentsStatesObject.EnumerateObject())
        {
            if (!TryGetRequiredString(property.Value, "status", out var agentStatus) ||
                !TryGetOptionalString(property.Value, "message", out var message))
            {
                return null;
            }

            agentsStates[property.Name] = new CodexCollabAgentState(agentStatus, message);
        }

        CodexReasoningEffort? reasoningEffort = null;
        if (CodexReasoningEffort.TryParse(reasoningEffortValue, out var parsedReasoningEffort))
        {
            reasoningEffort = parsedReasoningEffort;
        }

        return new CodexThreadItemCollabAgentToolCall(
            id,
            type,
            tool,
            status,
            senderThreadId,
            receiverThreadIds!,
            prompt,
            model,
            reasoningEffort,
            agentsStates,
            raw);
    }

    private static CodexThreadItem? ParseWebSearch(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "query", out var query) &&
        TryParseWebSearchAction(element, out var action)
            ? new CodexThreadItemWebSearch(id, type, query, action, raw)
            : null;

    private static CodexThreadItem? ParseImageView(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "path", out var path)
            ? new CodexThreadItemImageView(id, type, path, raw)
            : null;

    private static CodexThreadItem? ParseImageGeneration(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "status", out var status) &&
        TryGetRequiredString(element, "result", out var result) &&
        TryGetOptionalString(element, "revisedPrompt", out var revisedPrompt) &&
        TryGetOptionalString(element, "savedPath", out var savedPath)
            ? new CodexThreadItemImageGeneration(id, type, status, result, revisedPrompt, savedPath, raw)
            : null;

    private static CodexThreadItem? ParseEnteredReviewMode(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "review", out var review)
            ? new CodexThreadItemEnteredReviewMode(id, type, review, raw)
            : null;

    private static CodexThreadItem? ParseExitedReviewMode(string id, string type, JsonElement element, JsonElement raw) =>
        TryGetRequiredString(element, "review", out var review)
            ? new CodexThreadItemExitedReviewMode(id, type, review, raw)
            : null;

    private static bool TryParseCommandActions(JsonElement element, out IReadOnlyList<CodexCommandAction> actions)
    {
        actions = Array.Empty<CodexCommandAction>();
        if (!TryGetOptionalArray(element, "commandActions", out var commandActionsArray))
        {
            return false;
        }

        if (commandActionsArray is null)
        {
            return true;
        }

        actions = commandActionsArray.Value.EnumerateArray().Select(action => new CodexCommandAction(
            CodexAppServerClientJson.GetStringOrNull(action, "command") ?? string.Empty,
            CodexAppServerClientJson.GetStringOrNull(action, "type") ?? string.Empty,
            CodexAppServerClientJson.GetStringOrNull(action, "name"),
            CodexAppServerClientJson.GetStringOrNull(action, "path"),
            CodexAppServerClientJson.GetStringOrNull(action, "query"))).ToArray();
        return actions.All(action => !string.IsNullOrWhiteSpace(action.Type));
    }

    private static bool TryParseFileChanges(JsonElement element, out IReadOnlyList<CodexFileUpdateChange> changes)
    {
        changes = Array.Empty<CodexFileUpdateChange>();
        if (!TryGetRequiredArray(element, "changes", out var changesArray))
        {
            return false;
        }

        var parsed = new List<CodexFileUpdateChange>();
        foreach (var change in changesArray.EnumerateArray())
        {
            if (!TryGetRequiredString(change, "path", out var path) ||
                !TryGetRequiredString(change, "diff", out var diff) ||
                !TryParsePatchChangeKind(change, out var kind))
            {
                return false;
            }

            parsed.Add(new CodexFileUpdateChange(path, kind, diff));
        }

        changes = parsed;
        return true;
    }

    private static bool TryParsePatchChangeKind(JsonElement change, out CodexPatchChangeKind kind)
    {
        kind = new CodexPatchChangeKind(CodexPatchChangeKindType.Unknown);
        if (!change.TryGetProperty("kind", out var kindValue))
        {
            return false;
        }

        if (kindValue.ValueKind == JsonValueKind.String)
        {
            kind = kindValue.GetString() switch
            {
                "add" => new CodexPatchChangeKind(CodexPatchChangeKindType.Add),
                "delete" => new CodexPatchChangeKind(CodexPatchChangeKindType.Delete),
                "update" => new CodexPatchChangeKind(CodexPatchChangeKindType.Update),
                _ => kind
            };

            return kind.Type != CodexPatchChangeKindType.Unknown;
        }

        if (!TryGetRequiredString(kindValue, "type", out var type) ||
            !TryGetOptionalString(kindValue, "movePath", out var movePath))
        {
            return false;
        }

        kind = type switch
        {
            "add" => new CodexPatchChangeKind(CodexPatchChangeKindType.Add),
            "delete" => new CodexPatchChangeKind(CodexPatchChangeKindType.Delete),
            "update" => new CodexPatchChangeKind(CodexPatchChangeKindType.Update, movePath),
            _ => kind
        };

        return kind.Type != CodexPatchChangeKindType.Unknown;
    }

    private static bool TryParseWebSearchAction(JsonElement element, out CodexWebSearchAction? action)
    {
        action = null;
        if (!TryGetOptionalObject(element, "action", out var actionObject))
        {
            return false;
        }

        if (actionObject is null)
        {
            return true;
        }

        if (!TryGetRequiredString(actionObject.Value, "type", out var type) ||
            !TryGetOptionalString(actionObject.Value, "query", out var query) ||
            !TryGetStringArray(actionObject.Value, "queries", out var queries) ||
            !TryGetOptionalString(actionObject.Value, "url", out var url) ||
            !TryGetOptionalString(actionObject.Value, "pattern", out var pattern))
        {
            return false;
        }

        action = type switch
        {
            "search" => new CodexWebSearchAction(CodexWebSearchActionKind.Search, query, queries.Count > 0 ? queries : null),
            "openPage" => new CodexWebSearchAction(CodexWebSearchActionKind.OpenPage, Url: url),
            "findInPage" => new CodexWebSearchAction(CodexWebSearchActionKind.FindInPage, Url: url, Pattern: pattern),
            "other" => new CodexWebSearchAction(CodexWebSearchActionKind.Other),
            _ => null
        };

        return action is not null;
    }

}
