using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

internal static class AgentFrameworkToolResultMapper
{
    public static IReadOnlyList<DynamicToolCallOutputContentItem> CreateContentItems(
        object? value,
        JsonSerializerOptions serializerOptions)
    {
        var contentItems = new List<DynamicToolCallOutputContentItem>();
        AddResultContent(contentItems, value, serializerOptions);
        return contentItems.Count == 0
            ? [DynamicToolCallOutputContentItem.InputText(string.Empty)]
            : contentItems;
    }

    private static void AddResultContent(
        List<DynamicToolCallOutputContentItem> contentItems,
        object? value,
        JsonSerializerOptions serializerOptions)
    {
        switch (value)
        {
            case null:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(string.Empty));
                return;
            case string text:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(text));
                return;
            case JsonElement json:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(FormatJsonElement(json)));
                return;
            case FunctionResultContent { Exception: { } exception }:
                throw new InvalidOperationException("Agent Framework function result contains an exception.", exception);
            case FunctionResultContent result:
                AddResultContent(contentItems, result.Result, serializerOptions);
                return;
            case IEnumerable<AIContent> aiContents:
                foreach (var content in aiContents)
                {
                    AddAIContent(contentItems, content, serializerOptions);
                }

                return;
            case AIContent aiContent:
                AddAIContent(contentItems, aiContent, serializerOptions);
                return;
            default:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(
                    JsonSerializer.Serialize(value, value.GetType(), serializerOptions)));
                return;
        }
    }

    private static void AddAIContent(
        List<DynamicToolCallOutputContentItem> contentItems,
        AIContent content,
        JsonSerializerOptions serializerOptions)
    {
        switch (content)
        {
            case TextContent text:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(text.Text));
                break;
            case DataContent data when data.HasTopLevelMediaType("image"):
                contentItems.Add(DynamicToolCallOutputContentItem.InputImage(data.Uri));
                break;
            case UriContent uri when uri.HasTopLevelMediaType("image"):
                contentItems.Add(DynamicToolCallOutputContentItem.InputImage(uri.Uri.ToString()));
                break;
            case FunctionResultContent { Exception: { } exception }:
                throw new InvalidOperationException("Agent Framework function result contains an exception.", exception);
            case FunctionResultContent result:
                AddResultContent(contentItems, result.Result, serializerOptions);
                break;
            default:
                contentItems.Add(DynamicToolCallOutputContentItem.InputText(
                    JsonSerializer.Serialize(content, content.GetType(), serializerOptions)));
                break;
        }
    }

    private static string FormatJsonElement(JsonElement json)
    {
        return json.ValueKind == JsonValueKind.String
            ? json.GetString() ?? string.Empty
            : json.GetRawText();
    }
}
