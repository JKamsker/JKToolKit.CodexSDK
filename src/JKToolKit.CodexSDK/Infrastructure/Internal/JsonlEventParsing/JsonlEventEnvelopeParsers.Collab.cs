using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventEnvelopeParsers
{
    private static CollabAgentSpawnEndEvent? ParseCollabAgentSpawnEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        return new CollabAgentSpawnEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            NewThreadId = TryGetString(payload, "new_thread_id"),
            NewAgentNickname = TryGetString(payload, "new_agent_nickname"),
            NewAgentRole = TryGetString(payload, "new_agent_role"),
            Prompt = TryGetString(payload, "prompt"),
            Status = TryGetString(payload, "status")
        };
    }

    private static CollabAgentInteractionEndEvent? ParseCollabAgentInteractionEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        return new CollabAgentInteractionEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            ReceiverThreadId = TryGetString(payload, "receiver_thread_id"),
            ReceiverAgentNickname = TryGetString(payload, "receiver_agent_nickname"),
            ReceiverAgentRole = TryGetString(payload, "receiver_agent_role"),
            Prompt = TryGetString(payload, "prompt"),
            Status = TryGetString(payload, "status")
        };
    }

    private static CollabWaitingEndEvent? ParseCollabWaitingEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        IReadOnlyDictionary<string, string>? statuses = null;
        if (payload.TryGetProperty("statuses", out var statusesEl) && statusesEl.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in statusesEl.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? (prop.Value.GetString() ?? string.Empty)
                    : prop.Value.GetRawText();
            }

            statuses = dict;
        }

        return new CollabWaitingEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            Statuses = statuses
        };
    }

    private static CollabCloseEndEvent? ParseCollabCloseEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        return new CollabCloseEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            ReceiverThreadId = TryGetString(payload, "receiver_thread_id"),
            ReceiverAgentNickname = TryGetString(payload, "receiver_agent_nickname"),
            ReceiverAgentRole = TryGetString(payload, "receiver_agent_role"),
            Status = TryGetString(payload, "status")
        };
    }

    private static CollabResumeEndEvent? ParseCollabResumeEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        return new CollabResumeEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            ReceiverThreadId = TryGetString(payload, "receiver_thread_id"),
            ReceiverAgentNickname = TryGetString(payload, "receiver_agent_nickname"),
            ReceiverAgentRole = TryGetString(payload, "receiver_agent_role"),
            Status = TryGetString(payload, "status")
        };
    }
}

