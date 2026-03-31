using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventResponseItemParsers
{
    private static bool? ParseNullableBoolean(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static (string? RawText, JsonElement? StructuredValue) ParseStringOrStructured(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
        {
            return (null, null);
        }

        var rawText = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();

        var structuredValue = value.ValueKind == JsonValueKind.String
            ? (JsonElement?)null
            : value.Clone();

        return (rawText, structuredValue);
    }

    private static IReadOnlyList<ReasoningContentPart> ParseReasoningContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ReasoningContentPart>();
        }

        var parts = new List<ReasoningContentPart>();
        foreach (var content in contentArray.EnumerateArray())
        {
            if (content.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var contentType = TryGetString(content, "type");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                continue;
            }

            if (string.Equals(contentType, "reasoning_text", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(contentType, "text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ReasoningTextContentPart
                {
                    ContentType = contentType,
                    Text = TryGetString(content, "text") ?? string.Empty
                });
                continue;
            }

            parts.Add(new UnknownReasoningContentPart
            {
                ContentType = contentType,
                Raw = content.Clone()
            });
        }

        return parts;
    }

    private static IReadOnlyList<FunctionToolOutputContentPart>? ParseFunctionToolOutputContent(JsonElement? outputJson)
    {
        if (!outputJson.HasValue || outputJson.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<FunctionToolOutputContentPart>();
        foreach (var item in outputJson.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var contentType = TryGetString(item, "type");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                continue;
            }

            if (string.Equals(contentType, "input_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new FunctionToolOutputInputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(item, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_image", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new FunctionToolOutputInputImagePart
                {
                    ContentType = contentType,
                    ImageUrl = TryGetString(item, "image_url") ?? string.Empty,
                    Detail = TryGetString(item, "detail")
                });
                continue;
            }

            parts.Add(new UnknownFunctionToolOutputContentPart
            {
                ContentType = contentType,
                Raw = item.Clone()
            });
        }

        return parts;
    }

    private static IReadOnlyList<ResponseMessageContentPart> ParseMessageContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ResponseMessageContentPart>();
        }

        var parts = new List<ResponseMessageContentPart>();
        foreach (var content in contentArray.EnumerateArray())
        {
            if (content.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var contentType = TryGetString(content, "type");
            if (string.IsNullOrWhiteSpace(contentType))
            {
                continue;
            }

            if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageOutputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(content, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(content, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_image", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputImagePart
                {
                    ContentType = contentType,
                    ImageUrl = TryGetString(content, "image_url") ?? string.Empty
                });
                continue;
            }

            parts.Add(new UnknownResponseMessageContentPart
            {
                ContentType = contentType,
                Raw = content.Clone()
            });
        }

        return parts;
    }
}
