using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventEnvelopeParsers
{
    public static CodexEvent? ParseEventMsgEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("event_msg missing 'payload' object");
            return JsonlEventBasicParsers.ParseUnknownEvent(timestamp, "event_msg", rawPayload, ctx);
        }

        var innerType = TryGetString(payload, "type");
        if (string.IsNullOrWhiteSpace(innerType))
        {
            ctx.Logger.LogDebug("event_msg missing inner 'payload.type'; returning unknown event");
            return JsonlEventBasicParsers.ParseUnknownEvent(timestamp, "event_msg", rawPayload, ctx);
        }

        var innerRoot = payload.TryGetProperty("payload", out var innerPayload) && innerPayload.ValueKind == JsonValueKind.Object
            ? innerPayload
            : payload;

        return innerType switch
        {
            "agent_message" => JsonlEventBasicParsers.ParseAgentMessageEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "agent_reasoning" => JsonlEventBasicParsers.ParseAgentReasoningEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "agent_reasoning_raw_content" => ParseAgentReasoningRawContentEvent(innerRoot, timestamp, innerType, rawPayload),
            "agent_reasoning_section_break" => new AgentReasoningSectionBreakEvent { Timestamp = timestamp, Type = innerType, RawPayload = rawPayload },
            "user_message" => JsonlEventBasicParsers.ParseUserMessageEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "context_compacted" => new ContextCompactedEvent { Timestamp = timestamp, Type = innerType, RawPayload = rawPayload },
            "thread_rolled_back" => ParseThreadRolledBackEvent(innerRoot, timestamp, innerType, rawPayload),
            "undo_completed" => ParseUndoCompletedEvent(innerRoot, timestamp, innerType, rawPayload),
            "item_completed" => ParseItemCompletedEvent(innerRoot, timestamp, innerType, rawPayload),
            "background_event" => ParseBackgroundEvent(innerRoot, timestamp, innerType, rawPayload),
            "compaction_checkpoint_warning" => ParseCompactionCheckpointWarningEvent(innerRoot, timestamp, innerType, rawPayload),
            "error" => ParseErrorEvent(innerRoot, timestamp, innerType, rawPayload),
            "web_search_end" => ParseWebSearchEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "exec_command_end" => ParseExecCommandEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "mcp_tool_call_end" => ParseMcpToolCallEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "view_image_tool_call" => ParseViewImageToolCallEvent(innerRoot, timestamp, innerType, rawPayload),
            "patch_apply_begin" => ParsePatchApplyBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "patch_apply_end" => ParsePatchApplyEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "plan_update" => ParsePlanUpdateEvent(innerRoot, timestamp, innerType, rawPayload),
            "task_started" or "turn_started" => ParseTaskStartedEvent(innerRoot, timestamp, innerType, rawPayload),
            "task_complete" or "turn_complete" => ParseTaskCompleteEvent(innerRoot, timestamp, innerType, rawPayload),
            "turn_aborted" => ParseTurnAbortedEvent(innerRoot, timestamp, innerType, rawPayload),
            "turn_diff" => ParseTurnDiffEvent(innerRoot, timestamp, innerType, rawPayload),
            "entered_review_mode" => ParseEnteredReviewModeEvent(innerRoot, timestamp, innerType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "collab_agent_spawn_begin" => ParseCollabAgentSpawnBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_agent_spawn_end" => ParseCollabAgentSpawnEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_agent_interaction_begin" => ParseCollabAgentInteractionBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_agent_interaction_end" => ParseCollabAgentInteractionEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_waiting_begin" => ParseCollabWaitingBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_waiting_end" => ParseCollabWaitingEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_close_begin" => ParseCollabCloseBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_close_end" => ParseCollabCloseEndEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_resume_begin" => ParseCollabResumeBeginEvent(innerRoot, timestamp, innerType, rawPayload),
            "collab_resume_end" => ParseCollabResumeEndEvent(innerRoot, timestamp, innerType, rawPayload),
            _ => JsonlEventBasicParsers.ParseUnknownEvent(timestamp, innerType, rawPayload, ctx)
        };
    }

    public static CodexEvent? ParseEventEnvelopeEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("event missing 'payload' object");
            return JsonlEventBasicParsers.ParseUnknownEvent(timestamp, "event", rawPayload, ctx);
        }

        if (!payload.TryGetProperty("msg", out var msg) || msg.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("event missing 'payload.msg' object");
            return JsonlEventBasicParsers.ParseUnknownEvent(timestamp, "event", rawPayload, ctx);
        }

        var msgType = TryGetString(msg, "type");
        if (string.IsNullOrWhiteSpace(msgType))
        {
            ctx.Logger.LogWarning("event missing 'payload.msg.type'");
            return JsonlEventBasicParsers.ParseUnknownEvent(timestamp, "event", rawPayload, ctx);
        }

        return msgType switch
        {
            "agent_message" => JsonlEventBasicParsers.ParseAgentMessageEvent(msg, timestamp, msgType, rawPayload, ctx),
            "agent_reasoning" => JsonlEventBasicParsers.ParseAgentReasoningEvent(msg, timestamp, msgType, rawPayload, ctx),
            "agent_reasoning_raw_content" => ParseAgentReasoningRawContentEvent(msg, timestamp, msgType, rawPayload),
            "agent_reasoning_section_break" => new AgentReasoningSectionBreakEvent { Timestamp = timestamp, Type = msgType, RawPayload = rawPayload },
            "background_event" => ParseBackgroundEvent(msg, timestamp, msgType, rawPayload),
            "compaction_checkpoint_warning" => ParseCompactionCheckpointWarningEvent(msg, timestamp, msgType, rawPayload),
            "context_compacted" => new ContextCompactedEvent { Timestamp = timestamp, Type = msgType, RawPayload = rawPayload },
            "entered_review_mode" => ParseEnteredReviewModeEvent(msg, timestamp, msgType, rawPayload),
            "error" => ParseErrorEvent(msg, timestamp, msgType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(msg, timestamp, msgType, rawPayload, ctx),
            "thread_rolled_back" => ParseThreadRolledBackEvent(msg, timestamp, msgType, rawPayload),
            "undo_completed" => ParseUndoCompletedEvent(msg, timestamp, msgType, rawPayload),
            "item_completed" => ParseItemCompletedEvent(msg, timestamp, msgType, rawPayload),
            "user_message" => JsonlEventBasicParsers.ParseUserMessageEvent(msg, timestamp, msgType, rawPayload, ctx),
            "web_search_end" => ParseWebSearchEndEvent(msg, timestamp, msgType, rawPayload),
            "exec_command_end" => ParseExecCommandEndEvent(msg, timestamp, msgType, rawPayload),
            "mcp_tool_call_end" => ParseMcpToolCallEndEvent(msg, timestamp, msgType, rawPayload),
            "view_image_tool_call" => ParseViewImageToolCallEvent(msg, timestamp, msgType, rawPayload),
            "patch_apply_begin" => ParsePatchApplyBeginEvent(msg, timestamp, msgType, rawPayload),
            "patch_apply_end" => ParsePatchApplyEndEvent(msg, timestamp, msgType, rawPayload),
            "plan_update" => ParsePlanUpdateEvent(msg, timestamp, msgType, rawPayload),
            "task_started" or "turn_started" => ParseTaskStartedEvent(msg, timestamp, msgType, rawPayload),
            "task_complete" or "turn_complete" => ParseTaskCompleteEvent(msg, timestamp, msgType, rawPayload),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(msg, timestamp, msgType, rawPayload, ctx),
            "turn_aborted" => ParseTurnAbortedEvent(msg, timestamp, msgType, rawPayload),
            "turn_diff" => ParseTurnDiffEvent(msg, timestamp, msgType, rawPayload),
            "collab_agent_spawn_begin" => ParseCollabAgentSpawnBeginEvent(msg, timestamp, msgType, rawPayload),
            "collab_agent_spawn_end" => ParseCollabAgentSpawnEndEvent(msg, timestamp, msgType, rawPayload),
            "collab_agent_interaction_begin" => ParseCollabAgentInteractionBeginEvent(msg, timestamp, msgType, rawPayload),
            "collab_agent_interaction_end" => ParseCollabAgentInteractionEndEvent(msg, timestamp, msgType, rawPayload),
            "collab_waiting_begin" => ParseCollabWaitingBeginEvent(msg, timestamp, msgType, rawPayload),
            "collab_waiting_end" => ParseCollabWaitingEndEvent(msg, timestamp, msgType, rawPayload),
            "collab_close_begin" => ParseCollabCloseBeginEvent(msg, timestamp, msgType, rawPayload),
            "collab_close_end" => ParseCollabCloseEndEvent(msg, timestamp, msgType, rawPayload),
            "collab_resume_begin" => ParseCollabResumeBeginEvent(msg, timestamp, msgType, rawPayload),
            "collab_resume_end" => ParseCollabResumeEndEvent(msg, timestamp, msgType, rawPayload),
            _ => JsonlEventBasicParsers.ParseUnknownEvent(timestamp, msgType, rawPayload, ctx)
        };
    }

    private static BackgroundEvent? ParseBackgroundEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new BackgroundEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private static CompactionCheckpointWarningEvent? ParseCompactionCheckpointWarningEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new CompactionCheckpointWarningEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private static ErrorEvent? ParseErrorEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new ErrorEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private static AgentReasoningRawContentEvent? ParseAgentReasoningRawContentEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var text = TryGetString(payload, "text") ?? TryGetString(payload, "message");
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new AgentReasoningRawContentEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Text = text };
    }

    private static ThreadRolledBackEvent? ParseThreadRolledBackEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var numTurns = TryGetInt(payload, "num_turns");
        if (!numTurns.HasValue)
            return null;

        return new ThreadRolledBackEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, NumTurns = numTurns.Value };
    }

    private static UndoCompletedEvent? ParseUndoCompletedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        if (!payload.TryGetProperty("success", out var successEl) ||
            (successEl.ValueKind != JsonValueKind.True && successEl.ValueKind != JsonValueKind.False))
        {
            return null;
        }

        return new UndoCompletedEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Success = successEl.GetBoolean(),
            Message = TryGetString(payload, "message")
        };
    }

    private static TurnItemCompletedEvent ParseItemCompletedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var threadId = TryGetString(payload, "thread_id");
        var turnId = TryGetString(payload, "turn_id");

        string? itemType = null;
        string? itemId = null;
        string? text = null;

        if (payload.TryGetProperty("item", out var itemEl) && itemEl.ValueKind == JsonValueKind.Object)
        {
            itemType = TryGetString(itemEl, "type");
            itemId = TryGetString(itemEl, "id");
            text = TryGetString(itemEl, "text");

            if (string.IsNullOrWhiteSpace(text) &&
                itemEl.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in contentEl.EnumerateArray())
                {
                    if (part.ValueKind != JsonValueKind.Object)
                        continue;

                    var partText = TryGetString(part, "text");
                    if (!string.IsNullOrWhiteSpace(partText))
                    {
                        text = partText;
                        break;
                    }
                }
            }
        }

        return new TurnItemCompletedEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            ThreadId = threadId,
            TurnId = turnId,
            ItemType = itemType,
            ItemId = itemId,
            Text = text
        };
    }

    private static TurnAbortedEvent? ParseTurnAbortedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var reason = TryGetString(payload, "reason");
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        return new TurnAbortedEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Reason = reason };
    }

    private static TurnDiffEvent? ParseTurnDiffEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var diff = TryGetString(payload, "unified_diff");
        if (string.IsNullOrWhiteSpace(diff))
            return null;

        return new TurnDiffEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, UnifiedDiff = diff };
    }
}
