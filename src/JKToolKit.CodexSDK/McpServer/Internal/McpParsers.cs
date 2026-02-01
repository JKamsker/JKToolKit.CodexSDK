using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Internal;

internal static class McpToolsListParser
{
    public static IReadOnlyList<McpToolDescriptor> Parse(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("tools", out var toolsProp) ||
            toolsProp.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<McpToolDescriptor>();
        }

        var list = new List<McpToolDescriptor>();
        foreach (var tool in toolsProp.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object) continue;

            var name = tool.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var description = tool.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

            JsonElement? schema = null;
            if (tool.TryGetProperty("inputSchema", out var schemaProp))
            {
                schema = schemaProp.Clone();
            }

            list.Add(new McpToolDescriptor(name!, description, schema));
        }

        return list;
    }
}

internal static class CodexMcpResultParser
{
    public static (string ThreadId, string? Text, JsonElement StructuredContent, JsonElement Raw) Parse(JsonElement raw)
    {
        var structured = TryGet(raw, "structuredContent") ?? TryGet(raw, "structured_content");
        var content = TryGet(raw, "content");

        var threadId =
            (structured is { } s && TryGetString(s, "threadId") is { Length: > 0 } sid) ? sid :
            (structured is { } s2 && TryGetString(s2, "conversationId") is { Length: > 0 } cid) ? cid :
            string.Empty;

        var text = TryExtractText(content, structured);

        var structuredElement = structured ?? JsonDocument.Parse("{}").RootElement.Clone();

        return (threadId, text, structuredElement, raw);
    }

    private static string? TryExtractText(JsonElement? content, JsonElement? structured)
    {
        if (content is { ValueKind: JsonValueKind.Array })
        {
            foreach (var item in content.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                if (item.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    return textProp.GetString();
                }
            }
        }

        if (structured is { } s && s.ValueKind == JsonValueKind.Object)
        {
            if (s.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
            {
                return contentProp.GetString();
            }
        }

        return null;
    }

    private static JsonElement? TryGet(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var prop)
            ? prop.Clone()
            : null;

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

