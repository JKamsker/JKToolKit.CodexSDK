using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventEnvelopeParsers
{
    private static CollabAgentSpawnBeginEvent? ParseCollabAgentSpawnBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        var senderThreadId = TryGetString(payload, "sender_thread_id");
        var prompt = TryGetString(payload, "prompt");
        var model = TryGetString(payload, "model");
        if (string.IsNullOrWhiteSpace(callId) ||
            string.IsNullOrWhiteSpace(senderThreadId) ||
            prompt is null ||
            string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        return new CollabAgentSpawnBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = senderThreadId,
            Prompt = prompt,
            Model = model,
            ReasoningEffort = TryGetString(payload, "reasoning_effort")
        };
    }

    private static CollabAgentSpawnEndEvent? ParseCollabAgentSpawnEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        var statusInfo = ParseCollabAgentStatusInfoUnion(payload, "status");

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
            Model = TryGetString(payload, "model"),
            ReasoningEffort = TryGetString(payload, "reasoning_effort"),
            Status = statusInfo is null ? null : CollabAgentStatusJsonConverter.ToWireValue(statusInfo.Status),
            StatusInfo = statusInfo
        };
    }

    private static CollabAgentInteractionBeginEvent? ParseCollabAgentInteractionBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        var senderThreadId = TryGetString(payload, "sender_thread_id");
        var receiverThreadId = TryGetString(payload, "receiver_thread_id");
        var prompt = TryGetString(payload, "prompt");
        if (string.IsNullOrWhiteSpace(callId) ||
            string.IsNullOrWhiteSpace(senderThreadId) ||
            string.IsNullOrWhiteSpace(receiverThreadId) ||
            prompt is null)
        {
            return null;
        }

        return new CollabAgentInteractionBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = senderThreadId,
            ReceiverThreadId = receiverThreadId,
            Prompt = prompt
        };
    }

    private static CollabAgentInteractionEndEvent? ParseCollabAgentInteractionEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        var statusInfo = ParseCollabAgentStatusInfoUnion(payload, "status");

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
            Status = statusInfo is null ? null : CollabAgentStatusJsonConverter.ToWireValue(statusInfo.Status),
            StatusInfo = statusInfo
        };
    }

    private static CollabWaitingBeginEvent? ParseCollabWaitingBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var senderThreadId = TryGetString(payload, "sender_thread_id");
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(senderThreadId) || string.IsNullOrWhiteSpace(callId))
            return null;

        var receiverThreadIds = new List<string>();
        if (payload.TryGetProperty("receiver_thread_ids", out var receiverThreadIdsEl) &&
            receiverThreadIdsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var receiverThreadIdEl in receiverThreadIdsEl.EnumerateArray())
            {
                if (receiverThreadIdEl.ValueKind == JsonValueKind.String)
                {
                    receiverThreadIds.Add(receiverThreadIdEl.GetString() ?? string.Empty);
                }
            }
        }

        return new CollabWaitingBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            SenderThreadId = senderThreadId,
            ReceiverThreadIds = receiverThreadIds,
            ReceiverAgents = ParseCollabAgentRefs(payload, "receiver_agents"),
            CallId = callId
        };
    }

    private static CollabWaitingEndEvent? ParseCollabWaitingEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        IReadOnlyDictionary<string, string>? statuses = null;
        IReadOnlyDictionary<string, CollabAgentStatusInfo>? statusInfos = null;
        if (payload.TryGetProperty("statuses", out var statusesEl) && statusesEl.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var infoDict = new Dictionary<string, CollabAgentStatusInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in statusesEl.EnumerateObject())
            {
                var info = ParseCollabAgentStatusInfoUnion(prop.Value);
                if (info is null)
                    continue;

                dict[prop.Name] = CollabAgentStatusJsonConverter.ToWireValue(info.Status);
                infoDict[prop.Name] = info;
            }

            statuses = dict.Count == 0 ? null : dict;
            statusInfos = infoDict.Count == 0 ? null : infoDict;
        }

        return new CollabWaitingEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = TryGetString(payload, "sender_thread_id"),
            Statuses = statuses,
            StatusInfos = statusInfos,
            AgentStatuses = ParseCollabAgentStatusEntries(payload, "agent_statuses")
        };
    }

    private static CollabCloseBeginEvent? ParseCollabCloseBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        var senderThreadId = TryGetString(payload, "sender_thread_id");
        var receiverThreadId = TryGetString(payload, "receiver_thread_id");
        if (string.IsNullOrWhiteSpace(callId) ||
            string.IsNullOrWhiteSpace(senderThreadId) ||
            string.IsNullOrWhiteSpace(receiverThreadId))
        {
            return null;
        }

        return new CollabCloseBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = senderThreadId,
            ReceiverThreadId = receiverThreadId
        };
    }

    private static CollabCloseEndEvent? ParseCollabCloseEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        var statusInfo = ParseCollabAgentStatusInfoUnion(payload, "status");

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
            Status = statusInfo is null
                ? CollabReceiverStatus.Unknown
                : ConvertCollabAgentStatus(statusInfo.Status),
            StatusInfo = statusInfo
        };
    }

    private static CollabResumeBeginEvent? ParseCollabResumeBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        var senderThreadId = TryGetString(payload, "sender_thread_id");
        var receiverThreadId = TryGetString(payload, "receiver_thread_id");
        if (string.IsNullOrWhiteSpace(callId) ||
            string.IsNullOrWhiteSpace(senderThreadId) ||
            string.IsNullOrWhiteSpace(receiverThreadId))
        {
            return null;
        }

        return new CollabResumeBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            SenderThreadId = senderThreadId,
            ReceiverThreadId = receiverThreadId,
            ReceiverAgentNickname = TryGetString(payload, "receiver_agent_nickname"),
            ReceiverAgentRole = TryGetString(payload, "receiver_agent_role")
        };
    }

    private static CollabResumeEndEvent? ParseCollabResumeEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        var statusInfo = ParseCollabAgentStatusInfoUnion(payload, "status");

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
            Status = statusInfo is null
                ? CollabReceiverStatus.Unknown
                : ConvertCollabAgentStatus(statusInfo.Status),
            StatusInfo = statusInfo
        };
    }

    private static IReadOnlyList<CollabAgentRef>? ParseCollabAgentRefs(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var refsEl) || refsEl.ValueKind != JsonValueKind.Array)
            return null;

        var refs = new List<CollabAgentRef>();
        foreach (var entry in refsEl.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var threadId = TryGetString(entry, "thread_id");
            if (string.IsNullOrWhiteSpace(threadId))
                continue;

            refs.Add(new CollabAgentRef
            {
                ThreadId = threadId,
                AgentNickname = TryGetString(entry, "agent_nickname"),
                AgentRole = TryGetString(entry, "agent_role") ?? TryGetString(entry, "agent_type")
            });
        }

        return refs.Count == 0 ? null : refs;
    }

    private static IReadOnlyList<CollabAgentStatusEntry>? ParseCollabAgentStatusEntries(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var entriesEl) || entriesEl.ValueKind != JsonValueKind.Array)
            return null;

        var entries = new List<CollabAgentStatusEntry>();
        foreach (var entry in entriesEl.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var threadId = TryGetString(entry, "thread_id");
            var statusInfo = ParseCollabAgentStatusInfoUnion(entry, "status");
            if (string.IsNullOrWhiteSpace(threadId) || statusInfo is null)
                continue;

            entries.Add(new CollabAgentStatusEntry
            {
                ThreadId = threadId,
                AgentNickname = TryGetString(entry, "agent_nickname"),
                AgentRole = TryGetString(entry, "agent_role") ?? TryGetString(entry, "agent_type"),
                StatusInfo = statusInfo
            });
        }

        return entries.Count == 0 ? null : entries;
    }

    private static CollabAgentStatusInfo? ParseCollabAgentStatusInfoUnion(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var statusEl)
            ? ParseCollabAgentStatusInfoUnion(statusEl)
            : null;
    }

    private static CollabAgentStatusInfo? ParseCollabAgentStatusInfoUnion(JsonElement statusEl)
    {
        if (statusEl.ValueKind == JsonValueKind.String)
        {
            var rawStatus = statusEl.GetString();
            return new CollabAgentStatusInfo
            {
                Status = CollabAgentStatusJsonConverter.ParseOrUnknown(rawStatus),
                PayloadText = null,
                Payload = null
            };
        }

        if (statusEl.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var prop in statusEl.EnumerateObject())
        {
            var payloadText = TryExtractCollabStatusPayloadText(prop.Value);
            var payloadValue = prop.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? (JsonElement?)null
                : prop.Value.Clone();

            return new CollabAgentStatusInfo
            {
                Status = CollabAgentStatusJsonConverter.ParseOrUnknown(prop.Name),
                PayloadText = payloadText,
                Payload = payloadValue
            };
        }

        return null;
    }

    private static string? TryExtractCollabStatusPayloadText(JsonElement payload)
    {
        return payload.ValueKind switch
        {
            JsonValueKind.String => payload.GetString(),
            JsonValueKind.Object => TryGetString(payload, "text")
                                    ?? TryGetString(payload, "message")
                                    ?? TryGetNestedPayloadText(payload),
            _ => null
        };
    }

    private static string? TryGetNestedPayloadText(JsonElement payload)
    {
        if (!payload.TryGetProperty("payload", out var nestedPayload) || nestedPayload.ValueKind != JsonValueKind.Object)
            return null;

        return TryGetString(nestedPayload, "text") ?? TryGetString(nestedPayload, "message");
    }

    private static CollabReceiverStatus ConvertCollabAgentStatus(CollabAgentStatus status) =>
        status switch
        {
            CollabAgentStatus.PendingInit => CollabReceiverStatus.PendingInit,
            CollabAgentStatus.Running => CollabReceiverStatus.Running,
            CollabAgentStatus.Interrupted => CollabReceiverStatus.Interrupted,
            CollabAgentStatus.Completed => CollabReceiverStatus.Completed,
            CollabAgentStatus.Errored => CollabReceiverStatus.Errored,
            CollabAgentStatus.Shutdown => CollabReceiverStatus.Shutdown,
            CollabAgentStatus.NotFound => CollabReceiverStatus.NotFound,
            _ => CollabReceiverStatus.Unknown
        };
}
