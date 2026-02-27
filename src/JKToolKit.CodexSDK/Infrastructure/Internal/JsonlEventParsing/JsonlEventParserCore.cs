using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

internal static class JsonlEventParserCore
{
    public static CodexEvent? ParseLine(string line, JsonlEventParserContext ctx)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("timestamp", out var timestampElement))
        {
            var snippet = CodexDiagnosticsSanitizer.Sanitize(line, maxChars: 300);
            ctx.Logger.LogWarning("Event missing 'timestamp' field, skipping. LineSnippet: {LineSnippet}", snippet);
            return null;
        }

        if (!root.TryGetProperty("type", out var typeElement))
        {
            var snippet = CodexDiagnosticsSanitizer.Sanitize(line, maxChars: 300);
            ctx.Logger.LogWarning("Event missing 'type' field, skipping. LineSnippet: {LineSnippet}", snippet);
            return null;
        }

        var timestamp = timestampElement.GetDateTimeOffset();
        var type = typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : typeElement.GetRawText();

        if (string.IsNullOrWhiteSpace(type))
        {
            var snippet = CodexDiagnosticsSanitizer.Sanitize(line, maxChars: 300);
            ctx.Logger.LogWarning("Event has empty 'type' field, skipping. LineSnippet: {LineSnippet}", snippet);
            return null;
        }

        var payload = root.Clone();

        if (ctx.Transformers is { Count: > 0 })
        {
            foreach (var transformer in ctx.Transformers)
            {
                if (transformer is null)
                {
                    continue;
                }

                try
                {
                    (type, payload) = transformer.Transform(type, payload);
                }
                catch (Exception ex)
                {
                    ctx.Logger.LogTrace(ex, "Exec event transformer threw (type={Type}).", type);
                }
            }
        }

        if (ctx.Mappers is { Count: > 0 })
        {
            foreach (var mapper in ctx.Mappers)
            {
                if (mapper is null)
                {
                    continue;
                }

                try
                {
                    var mapped = mapper.TryMap(timestamp, type, payload);
                    if (mapped is not null)
                    {
                        return mapped;
                    }
                }
                catch (Exception ex)
                {
                    ctx.Logger.LogTrace(ex, "Exec event mapper threw (type={Type}).", type);
                }
            }
        }

        var rawPayload = payload;
        return type switch
        {
            "session_meta" => JsonlEventBasicParsers.ParseSessionMetaEvent(payload, timestamp, type, rawPayload, ctx),
            "user_message" => JsonlEventBasicParsers.ParseUserMessageEvent(payload, timestamp, type, rawPayload, ctx),
            "agent_message" => JsonlEventBasicParsers.ParseAgentMessageEvent(payload, timestamp, type, rawPayload, ctx),
            "agent_reasoning" => JsonlEventBasicParsers.ParseAgentReasoningEvent(payload, timestamp, type, rawPayload, ctx),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(payload, timestamp, type, rawPayload, ctx),
            "turn_context" => JsonlEventBasicParsers.ParseTurnContextEvent(payload, timestamp, type, rawPayload, ctx),
            "response_item" => JsonlEventResponseItemParsers.ParseResponseItemEvent(payload, timestamp, type, rawPayload, ctx),
            "event_msg" => JsonlEventEnvelopeParsers.ParseEventMsgEvent(payload, timestamp, rawPayload, ctx),
            "event" => JsonlEventEnvelopeParsers.ParseEventEnvelopeEvent(payload, timestamp, rawPayload, ctx),
            "compacted" => JsonlEventBasicParsers.ParseCompactedEvent(payload, timestamp, type, rawPayload, ctx),
            _ => JsonlEventBasicParsers.ParseUnknownEvent(timestamp, type, rawPayload, ctx)
        };
    }
}

