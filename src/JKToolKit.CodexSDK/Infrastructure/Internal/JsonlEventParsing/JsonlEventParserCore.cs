using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

internal static class JsonlEventParserCore
{
    public static CodexEvent? ParseLine(string line, ILogger logger)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        var ctx = new JsonlEventParserContext(logger);

        if (!root.TryGetProperty("timestamp", out var timestampElement))
        {
            ctx.Logger.LogWarning("Event missing 'timestamp' field, skipping: {Line}", line);
            return null;
        }

        if (!root.TryGetProperty("type", out var typeElement))
        {
            ctx.Logger.LogWarning("Event missing 'type' field, skipping: {Line}", line);
            return null;
        }

        var timestamp = timestampElement.GetDateTimeOffset();
        var type = typeElement.GetString();

        if (string.IsNullOrWhiteSpace(type))
        {
            ctx.Logger.LogWarning("Event has empty 'type' field, skipping: {Line}", line);
            return null;
        }

        var rawPayload = root.Clone();

        return type switch
        {
            "session_meta" => JsonlEventBasicParsers.ParseSessionMetaEvent(root, timestamp, type, rawPayload, ctx),
            "user_message" => JsonlEventBasicParsers.ParseUserMessageEvent(root, timestamp, type, rawPayload, ctx),
            "agent_message" => JsonlEventBasicParsers.ParseAgentMessageEvent(root, timestamp, type, rawPayload, ctx),
            "agent_reasoning" => JsonlEventBasicParsers.ParseAgentReasoningEvent(root, timestamp, type, rawPayload, ctx),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(root, timestamp, type, rawPayload, ctx),
            "turn_context" => JsonlEventBasicParsers.ParseTurnContextEvent(root, timestamp, type, rawPayload, ctx),
            "response_item" => JsonlEventResponseItemParsers.ParseResponseItemEvent(root, timestamp, type, rawPayload, ctx),
            "event_msg" => JsonlEventEnvelopeParsers.ParseEventMsgEvent(root, timestamp, rawPayload, ctx),
            "event" => JsonlEventEnvelopeParsers.ParseEventEnvelopeEvent(root, timestamp, rawPayload, ctx),
            "compacted" => JsonlEventBasicParsers.ParseCompactedEvent(root, timestamp, type, rawPayload, ctx),
            _ => JsonlEventBasicParsers.ParseUnknownEvent(timestamp, type, rawPayload, ctx)
        };
    }
}

