using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer.ResponseItems;

/// <summary>
/// Represents a typed raw response item payload.
/// </summary>
public abstract record class CodexResponseItem(string Type, JsonElement Raw)
{
    /// <summary>
    /// Gets the upstream type discriminator.
    /// </summary>
    public string Type { get; } = Type ?? string.Empty;

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public JsonElement Raw { get; } = Raw;
}

/// <summary>
/// Represents a message response item.
/// </summary>
public sealed record class CodexResponseItemMessage(
    string Type,
    string Role,
    IReadOnlyList<CodexContentItem> Content,
    bool? EndTurn,
    string? Phase,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a reasoning response item.
/// </summary>
public sealed record class CodexResponseItemReasoning(
    string Type,
    IReadOnlyList<CodexReasoningSummaryPart> Summary,
    IReadOnlyList<string>? Content,
    string? EncryptedContent,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a function call request item.
/// </summary>
public sealed record class CodexResponseItemFunctionCall(
    string Type,
    string Name,
    string? Namespace,
    string Arguments,
    string CallId,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a function call output item.
/// </summary>
public sealed record class CodexResponseItemFunctionCallOutput(
    string Type,
    string CallId,
    JsonElement Output,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a tool search output item.
/// </summary>
public sealed record class CodexResponseItemToolSearchOutput(
    string Type,
    string? CallId,
    string Status,
    string Execution,
    IReadOnlyList<JsonElement> Tools,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a web search call item.
/// </summary>
public sealed record class CodexResponseItemWebSearchCall(
    string Type,
    string? Status,
    CodexWebSearchAction? Action,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents an image generation call item.
/// </summary>
public sealed record class CodexResponseItemImageGenerationCall(
    string Type,
    string Id,
    string Status,
    string Result,
    string? RevisedPrompt,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a ghost snapshot item.
/// </summary>
public sealed record class CodexResponseItemGhostSnapshot(
    string Type,
    JsonElement GhostCommit,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents an encrypted compaction item.
/// </summary>
public sealed record class CodexResponseItemCompaction(
    string Type,
    string EncryptedContent,
    JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents an uncategorized response item.
/// </summary>
public sealed record class CodexResponseItemUnknown(string Type, JsonElement Raw)
    : CodexResponseItem(Type, Raw);

/// <summary>
/// Represents a content chunk inside a message item.
/// </summary>
public abstract record class CodexContentItem(string Type, JsonElement Raw)
{
    /// <summary>
    /// Gets the content type discriminator.
    /// </summary>
    public string Type { get; } = Type ?? string.Empty;

    /// <summary>
    /// Gets the raw payload.
    /// </summary>
    public JsonElement Raw { get; } = Raw;
}

/// <summary>
/// Represents an input text content block.
/// </summary>
public sealed record class CodexContentInputText(string Text, JsonElement Raw)
    : CodexContentItem("input_text", Raw);

/// <summary>
/// Represents an input image content block.
/// </summary>
public sealed record class CodexContentInputImage(string ImageUrl, JsonElement Raw)
    : CodexContentItem("input_image", Raw);

/// <summary>
/// Represents an output text content block.
/// </summary>
public sealed record class CodexContentOutputText(string Text, JsonElement Raw)
    : CodexContentItem("output_text", Raw);

/// <summary>
/// Represents a reasoning summary entry inside a reasoning response item.
/// </summary>
public sealed record class CodexReasoningSummaryPart(string Type, string Text);

/// <summary>
/// Represents a web search action descriptor.
/// </summary>
public sealed record class CodexWebSearchAction(string Type, string? Query, IReadOnlyList<string>? Queries, string? Url, string? Pattern);

internal static class CodexResponseItemParser
{
    public static CodexResponseItem Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new CodexResponseItemUnknown(string.Empty, element.Clone());
        }

        var type = CodexAppServerClientJson.GetStringOrNull(element, "type") ?? string.Empty;
        var raw = element.Clone();

        return type switch
        {
            "message" => ParseMessage(type, element, raw),
            "reasoning" => ParseReasoning(type, element, raw),
            "function_call" => ParseFunctionCall(type, element, raw),
            "function_call_output" => ParseFunctionCallOutput(type, element, raw),
            "tool_search_output" => ParseToolSearchOutput(type, element, raw),
            "web_search_call" => ParseWebSearchCall(type, element, raw),
            "image_generation_call" => ParseImageGenerationCall(type, element, raw),
            "ghost_snapshot" => ParseGhostSnapshot(type, element, raw),
            "compaction" => ParseCompaction(type, element, raw),
            _ => new CodexResponseItemUnknown(type, raw)
        };
    }

    private static CodexResponseItemMessage ParseMessage(string type, JsonElement element, JsonElement raw)
    {
        var role = CodexAppServerClientJson.GetStringOrNull(element, "role") ?? string.Empty;
        var content = ParseContentItems(element);
        var endTurn = element.TryGetProperty("end_turn", out var endTurnValue)
            ? endTurnValue.ValueKind == JsonValueKind.True
                ? true
                : endTurnValue.ValueKind == JsonValueKind.False
                    ? false
                    : (bool?)null
            : null;
        var phase = CodexAppServerClientJson.GetStringOrNull(element, "phase");
        return new CodexResponseItemMessage(type, role, content, endTurn, phase, raw);
    }

    private static CodexResponseItemReasoning ParseReasoning(string type, JsonElement element, JsonElement raw)
    {
        var summary = ParseReasoningSummary(element);
        var content = ParseStringList(element, "content");
        var encrypted = CodexAppServerClientJson.GetStringOrNull(element, "encrypted_content");
        return new CodexResponseItemReasoning(type, summary, content.Count > 0 ? content : null, encrypted, raw);
    }

    private static CodexResponseItemFunctionCall ParseFunctionCall(string type, JsonElement element, JsonElement raw)
    {
        var name = CodexAppServerClientJson.GetStringOrNull(element, "name") ?? string.Empty;
        var ns = CodexAppServerClientJson.GetStringOrNull(element, "namespace");
        var arguments = CodexAppServerClientJson.GetStringOrNull(element, "arguments") ?? string.Empty;
        var callId = CodexAppServerClientJson.GetStringOrNull(element, "call_id") ?? string.Empty;
        return new CodexResponseItemFunctionCall(type, name, ns, arguments, callId, raw);
    }

    private static CodexResponseItemFunctionCallOutput ParseFunctionCallOutput(string type, JsonElement element, JsonElement raw)
    {
        var callId = CodexAppServerClientJson.GetStringOrNull(element, "call_id") ?? string.Empty;
        var output = element.TryGetProperty("output", out var value) ? value.Clone() : default;
        return new CodexResponseItemFunctionCallOutput(type, callId, output, raw);
    }

    private static CodexResponseItemToolSearchOutput ParseToolSearchOutput(string type, JsonElement element, JsonElement raw)
    {
        var callId = CodexAppServerClientJson.GetStringOrNull(element, "call_id");
        var status = CodexAppServerClientJson.GetStringOrNull(element, "status") ?? string.Empty;
        var execution = CodexAppServerClientJson.GetStringOrNull(element, "execution") ?? string.Empty;
        var tools = element.TryGetProperty("tools", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(i => i.Clone()).ToList()
            : new List<JsonElement>();
        return new CodexResponseItemToolSearchOutput(type, callId, status, execution, tools, raw);
    }

    private static CodexResponseItemWebSearchCall ParseWebSearchCall(string type, JsonElement element, JsonElement raw)
    {
        var status = CodexAppServerClientJson.GetStringOrNull(element, "status");
        var action = element.TryGetProperty("action", out var actionElement) && actionElement.ValueKind == JsonValueKind.Object
            ? ParseWebSearchAction(actionElement)
            : null;
        return new CodexResponseItemWebSearchCall(type, status, action, raw);
    }

    private static CodexResponseItemImageGenerationCall ParseImageGenerationCall(string type, JsonElement element, JsonElement raw)
    {
        var id = CodexAppServerClientJson.GetStringOrNull(element, "id") ?? string.Empty;
        var status = CodexAppServerClientJson.GetStringOrNull(element, "status") ?? string.Empty;
        var result = CodexAppServerClientJson.GetStringOrNull(element, "result") ?? string.Empty;
        var revised = CodexAppServerClientJson.GetStringOrNull(element, "revised_prompt");
        return new CodexResponseItemImageGenerationCall(type, id, status, result, revised, raw);
    }

    private static CodexResponseItemGhostSnapshot ParseGhostSnapshot(string type, JsonElement element, JsonElement raw)
    {
        var ghost = element.TryGetProperty("ghost_commit", out var commit) ? commit.Clone() : default;
        return new CodexResponseItemGhostSnapshot(type, ghost, raw);
    }

    private static CodexResponseItemCompaction ParseCompaction(string type, JsonElement element, JsonElement raw)
    {
        var encrypted = CodexAppServerClientJson.GetStringOrNull(element, "encrypted_content") ?? string.Empty;
        return new CodexResponseItemCompaction(type, encrypted, raw);
    }

    private static IReadOnlyList<CodexContentItem> ParseContentItems(JsonElement element)
    {
        if (!element.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexContentItem>();
        }

        var list = new List<CodexContentItem>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = CodexAppServerClientJson.GetStringOrNull(item, "type") ?? string.Empty;
            switch (kind)
            {
                case "input_text":
                    list.Add(new CodexContentInputText(CodexAppServerClientJson.GetStringOrNull(item, "text") ?? string.Empty, item.Clone()));
                    break;
                case "input_image":
                    list.Add(new CodexContentInputImage(CodexAppServerClientJson.GetStringOrNull(item, "image_url") ?? string.Empty, item.Clone()));
                    break;
                case "output_text":
                    list.Add(new CodexContentOutputText(CodexAppServerClientJson.GetStringOrNull(item, "text") ?? string.Empty, item.Clone()));
                    break;
            }
        }

        return list;
    }

    private static IReadOnlyList<CodexReasoningSummaryPart> ParseReasoningSummary(JsonElement element)
    {
        if (!element.TryGetProperty("summary", out var summary) || summary.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexReasoningSummaryPart>();
        }

        var list = new List<CodexReasoningSummaryPart>();
        foreach (var entry in summary.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new CodexReasoningSummaryPart(
                CodexAppServerClientJson.GetStringOrNull(entry, "type") ?? string.Empty,
                CodexAppServerClientJson.GetStringOrNull(entry, "text") ?? string.Empty));
        }

        return list;
    }

    private static IReadOnlyList<string> ParseStringList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                results.Add(item.GetString() ?? string.Empty);
            }
        }

        return results;
    }

    private static CodexWebSearchAction ParseWebSearchAction(JsonElement element)
    {
        var type = CodexAppServerClientJson.GetStringOrNull(element, "type") ?? string.Empty;
        var query = CodexAppServerClientJson.GetStringOrNull(element, "query");
        var url = CodexAppServerClientJson.GetStringOrNull(element, "url");
        var pattern = CodexAppServerClientJson.GetStringOrNull(element, "pattern");
        var queries = ParseStringList(element, "queries");
        return new CodexWebSearchAction(type, query, queries.Count > 0 ? queries : null, url, pattern);
    }
}
