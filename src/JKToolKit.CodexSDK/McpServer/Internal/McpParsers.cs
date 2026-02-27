using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Internal;

internal static class McpToolsListParser
{
    public static IReadOnlyList<McpToolDescriptor> Parse(JsonElement result)
    {
        return TryParse(result, out var tools, out _)
            ? tools
            : Array.Empty<McpToolDescriptor>();
    }

    public static bool TryParse(JsonElement result, out IReadOnlyList<McpToolDescriptor> tools, out string? nextCursor)
    {
        tools = Array.Empty<McpToolDescriptor>();
        nextCursor = null;

        if (result.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (result.TryGetProperty("nextCursor", out var cursorProp) && cursorProp.ValueKind == JsonValueKind.String)
        {
            nextCursor = cursorProp.GetString();
        }
        else if (result.TryGetProperty("next_cursor", out cursorProp) && cursorProp.ValueKind == JsonValueKind.String)
        {
            nextCursor = cursorProp.GetString();
        }

        if (!result.TryGetProperty("tools", out var toolsProp) || toolsProp.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var list = new List<McpToolDescriptor>();
        foreach (var tool in toolsProp.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object) continue;

            var name = tool.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var description = tool.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                ? descProp.GetString()
                : null;

            JsonElement? schema = null;
            if (tool.TryGetProperty("inputSchema", out var schemaProp))
            {
                schema = schemaProp.Clone();
            }

            list.Add(new McpToolDescriptor(name!, description, schema));
        }

        tools = list;
        return true;
    }
}

internal static class CodexMcpResultParser
{
    public static (string ThreadId, string? Text, JsonElement StructuredContent, JsonElement Raw) Parse(JsonElement raw)
    {
        var structured = TryGetObject(raw, "structuredContent") ?? TryGetObject(raw, "structured_content");

        var threadId = string.Empty;
        if (structured is { } s)
        {
            threadId =
                (TryGetString(s, "threadId") is { Length: > 0 } sid) ? sid :
                (TryGetString(s, "thread_id") is { Length: > 0 } sid2) ? sid2 :
                (TryGetString(s, "conversationId") is { Length: > 0 } cid) ? cid :
                (TryGetString(s, "conversation_id") is { Length: > 0 } cid2) ? cid2 :
                string.Empty;
        }

        if (string.IsNullOrWhiteSpace(threadId))
        {
            threadId =
                (TryGetString(raw, "threadId") is { Length: > 0 } sid) ? sid :
                (TryGetString(raw, "thread_id") is { Length: > 0 } sid2) ? sid2 :
                (TryGetString(raw, "conversationId") is { Length: > 0 } cid) ? cid :
                (TryGetString(raw, "conversation_id") is { Length: > 0 } cid2) ? cid2 :
                string.Empty;
        }

        var text = TryExtractText(raw);

        JsonElement structuredElement;
        if (structured is { } se)
        {
            structuredElement = se;
        }
        else
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            structuredElement = emptyDoc.RootElement.Clone();
        }

        return (threadId, text, structuredElement, raw);
    }

    internal static string? TryExtractText(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (raw.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            string? combined = null;
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (item.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    var t = textProp.GetString();
                    if (string.IsNullOrEmpty(t))
                    {
                        continue;
                    }

                    combined = combined is null ? t : combined + t;
                }
            }

            if (!string.IsNullOrWhiteSpace(combined))
            {
                return combined;
            }
        }

        if (raw.TryGetProperty("structuredContent", out var structured) && structured.ValueKind == JsonValueKind.Object &&
            structured.TryGetProperty("content", out var structuredText) && structuredText.ValueKind == JsonValueKind.String)
        {
            return structuredText.GetString();
        }

        if (raw.TryGetProperty("structured_content", out structured) && structured.ValueKind == JsonValueKind.Object &&
            structured.TryGetProperty("content", out structuredText) && structuredText.ValueKind == JsonValueKind.String)
        {
            return structuredText.GetString();
        }

        return null;
    }

    private static JsonElement? TryGetObject(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.Object
            ? prop.Clone()
            : null;

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

