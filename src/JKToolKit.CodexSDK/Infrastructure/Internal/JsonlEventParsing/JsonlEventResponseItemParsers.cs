using System.Linq;
using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static class JsonlEventResponseItemParsers
{
    public static ResponseItemEvent? ParseResponseItemEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            ctx.Logger.LogWarning("response_item event missing 'payload' field");
            return null;
        }

        var payloadType = payload.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(payloadType))
        {
            ctx.Logger.LogWarning("response_item event missing 'payload.type' field");
            return null;
        }

        var normalized = ParseResponseItemPayload(payloadType, payload);

        return new ResponseItemEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            PayloadType = payloadType,
            Payload = normalized
        };
    }

    public static ResponseItemPayload ParseResponseItemPayload(string payloadType, JsonElement payload)
    {
        if (string.Equals(payloadType, "reasoning", StringComparison.OrdinalIgnoreCase))
        {
            var summaries = Array.Empty<string>();
            if (payload.TryGetProperty("summary", out var summaryArray) && summaryArray.ValueKind == JsonValueKind.Array)
            {
                summaries = summaryArray
                    .EnumerateArray()
                    .Select(s => s.ValueKind == JsonValueKind.Object ? TryGetString(s, "text") : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }

            var encrypted = TryGetString(payload, "encrypted_content");

            return new ReasoningResponseItemPayload
            {
                PayloadType = payloadType,
                SummaryTexts = summaries,
                EncryptedContent = encrypted
            };
        }

        if (string.Equals(payloadType, "message", StringComparison.OrdinalIgnoreCase))
        {
            var role = TryGetString(payload, "role");
            var parts = ParseMessageContent(payload);
            return new MessageResponseItemPayload
            {
                PayloadType = payloadType,
                Role = role,
                Content = parts
            };
        }

        if (string.Equals(payloadType, "function_call", StringComparison.OrdinalIgnoreCase))
        {
            var name = TryGetString(payload, "name");
            string? argsJson = null;
            if (payload.TryGetProperty("arguments", out var argsEl))
            {
                argsJson = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString()
                    : argsEl.GetRawText();
            }
            var callId = TryGetString(payload, "call_id");

            return new FunctionCallResponseItemPayload
            {
                PayloadType = payloadType,
                Name = name,
                ArgumentsJson = argsJson,
                CallId = callId
            };
        }

        if (string.Equals(payloadType, "function_call_output", StringComparison.OrdinalIgnoreCase))
        {
            var callId = TryGetString(payload, "call_id");
            string? output = null;
            if (payload.TryGetProperty("output", out var outputEl))
            {
                output = outputEl.ValueKind == JsonValueKind.String ? outputEl.GetString() : outputEl.GetRawText();
            }

            return new FunctionCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = callId,
                Output = output
            };
        }

        if (string.Equals(payloadType, "custom_tool_call", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomToolCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                Name = TryGetString(payload, "name"),
                Input = TryGetString(payload, "input")
            };
        }

        if (string.Equals(payloadType, "custom_tool_call_output", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomToolCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = TryGetString(payload, "call_id"),
                Output = TryGetString(payload, "output")
            };
        }

        if (string.Equals(payloadType, "web_search_call", StringComparison.OrdinalIgnoreCase))
        {
            WebSearchAction? action = null;
            if (payload.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.Object)
            {
                IReadOnlyList<string>? queries = null;
                if (actionEl.TryGetProperty("queries", out var queriesEl) && queriesEl.ValueKind == JsonValueKind.Array)
                {
                    queries = queriesEl.EnumerateArray()
                        .Select(q => q.ValueKind == JsonValueKind.String ? q.GetString() : null)
                        .Where(q => !string.IsNullOrWhiteSpace(q))
                        .Cast<string>()
                        .ToArray();
                }

                action = new WebSearchAction(
                    Type: TryGetString(actionEl, "type"),
                    Query: TryGetString(actionEl, "query"),
                    Queries: queries);
            }

            return new WebSearchCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                Action = action
            };
        }

        if (string.Equals(payloadType, "ghost_snapshot", StringComparison.OrdinalIgnoreCase))
        {
            GhostCommit? commit = null;
            if (payload.TryGetProperty("ghost_commit", out var commitEl) && commitEl.ValueKind == JsonValueKind.Object)
            {
                IReadOnlyList<string>? files = null;
                if (commitEl.TryGetProperty("preexisting_untracked_files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
                {
                    files = filesEl.EnumerateArray()
                        .Select(f => f.ValueKind == JsonValueKind.String ? f.GetString() : null)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Cast<string>()
                        .ToArray();
                }

                IReadOnlyList<string>? dirs = null;
                if (commitEl.TryGetProperty("preexisting_untracked_dirs", out var dirsEl) && dirsEl.ValueKind == JsonValueKind.Array)
                {
                    dirs = dirsEl.EnumerateArray()
                        .Select(d => d.ValueKind == JsonValueKind.String ? d.GetString() : null)
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Cast<string>()
                        .ToArray();
                }

                commit = new GhostCommit(
                    Id: TryGetString(commitEl, "id"),
                    Parent: TryGetString(commitEl, "parent"),
                    PreexistingUntrackedFiles: files,
                    PreexistingUntrackedDirs: dirs);
            }

            return new GhostSnapshotResponseItemPayload
            {
                PayloadType = payloadType,
                GhostCommit = commit
            };
        }

        if (string.Equals(payloadType, "compaction", StringComparison.OrdinalIgnoreCase))
        {
            return new CompactionResponseItemPayload
            {
                PayloadType = payloadType,
                EncryptedContent = TryGetString(payload, "encrypted_content")
            };
        }

        return new UnknownResponseItemPayload
        {
            PayloadType = payloadType,
            Raw = payload.Clone()
        };
    }

    private static IReadOnlyList<ResponseMessageContentPart> ParseMessageContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ResponseMessageContentPart>();
        }

        var parts = new List<ResponseMessageContentPart>();
        foreach (var c in contentArray.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object)
                continue;

            var contentType = TryGetString(c, "type");
            if (string.IsNullOrWhiteSpace(contentType))
                continue;

            if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageOutputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(c, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(c, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_image", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputImagePart
                {
                    ContentType = contentType,
                    ImageUrl = TryGetString(c, "image_url") ?? string.Empty
                });
                continue;
            }

            parts.Add(new UnknownResponseMessageContentPart
            {
                ContentType = contentType,
                Raw = c.Clone()
            });
        }

        return parts;
    }
}
