using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static class JsonlEventEnvelopeParsers
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
            return null;
        }

        var innerType = payload.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
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
            "user_message" => JsonlEventBasicParsers.ParseUserMessageEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
            "context_compacted" => new ContextCompactedEvent { Timestamp = timestamp, Type = innerType, RawPayload = rawPayload },
            "turn_aborted" => ParseTurnAbortedEvent(innerRoot, timestamp, innerType, rawPayload),
            "entered_review_mode" => ParseEnteredReviewModeEvent(innerRoot, timestamp, innerType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(innerRoot, timestamp, innerType, rawPayload, ctx),
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
            return null;
        }

        if (!payload.TryGetProperty("msg", out var msg) || msg.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("event missing 'payload.msg' object");
            return null;
        }

        var msgType = msg.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(msgType))
        {
            ctx.Logger.LogWarning("event missing 'payload.msg.type'");
            return null;
        }

        return msgType switch
        {
            "agent_message" => JsonlEventBasicParsers.ParseAgentMessageEvent(msg, timestamp, msgType, rawPayload, ctx),
            "agent_reasoning" => JsonlEventBasicParsers.ParseAgentReasoningEvent(msg, timestamp, msgType, rawPayload, ctx),
            "agent_reasoning_section_break" => new AgentReasoningSectionBreakEvent { Timestamp = timestamp, Type = msgType, RawPayload = rawPayload },
            "background_event" => ParseBackgroundEvent(msg, timestamp, msgType, rawPayload),
            "compaction_checkpoint_warning" => ParseCompactionCheckpointWarningEvent(msg, timestamp, msgType, rawPayload),
            "entered_review_mode" => ParseEnteredReviewModeEvent(msg, timestamp, msgType, rawPayload),
            "error" => ParseErrorEvent(msg, timestamp, msgType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(msg, timestamp, msgType, rawPayload, ctx),
            "patch_apply_begin" => ParsePatchApplyBeginEvent(msg, timestamp, msgType, rawPayload),
            "patch_apply_end" => ParsePatchApplyEndEvent(msg, timestamp, msgType, rawPayload),
            "plan_update" => ParsePlanUpdateEvent(msg, timestamp, msgType, rawPayload),
            "task_started" => ParseTaskStartedEvent(msg, timestamp, msgType, rawPayload),
            "task_complete" => ParseTaskCompleteEvent(msg, timestamp, msgType, rawPayload),
            "token_count" => JsonlEventBasicParsers.ParseTokenCountEvent(msg, timestamp, msgType, rawPayload, ctx),
            "turn_aborted" => ParseTurnAbortedEvent(msg, timestamp, msgType, rawPayload),
            "turn_diff" => ParseTurnDiffEvent(msg, timestamp, msgType, rawPayload),
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

    private static EnteredReviewModeEvent? ParseEnteredReviewModeEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var prompt = TryGetString(payload, "prompt");
        var hint = TryGetString(payload, "user_facing_hint");

        ReviewTarget? target = null;
        if (payload.TryGetProperty("target", out var targetEl) && targetEl.ValueKind == JsonValueKind.Object)
        {
            var targetType = TryGetString(targetEl, "type") ?? "unknown";
            target = new ReviewTarget(
                Type: targetType,
                Branch: TryGetString(targetEl, "branch"),
                Sha: TryGetString(targetEl, "sha"),
                Title: TryGetString(targetEl, "title"),
                Instructions: TryGetString(targetEl, "instructions"));
        }

        return new EnteredReviewModeEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Prompt = prompt,
            UserFacingHint = hint,
            Target = target
        };
    }

    private static ExitedReviewModeEvent? ParseExitedReviewModeEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload, in JsonlEventParserContext ctx)
    {
        var payload = GetEventBody(root);
        if (!payload.TryGetProperty("review_output", out var reviewOutputEl) || reviewOutputEl.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("exited_review_mode event missing 'review_output' object");
            return null;
        }

        var reviewOutput = ParseReviewOutput(reviewOutputEl);

        return new ExitedReviewModeEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            ReviewOutput = reviewOutput
        };
    }

    private static ReviewOutput ParseReviewOutput(JsonElement el)
    {
        var correctness = TryGetString(el, "overall_correctness");
        var explanation = TryGetString(el, "overall_explanation");
        var confidence = TryGetDouble(el, "overall_confidence_score");

        var findings = new List<ReviewFinding>();
        if (el.TryGetProperty("findings", out var findingsEl) && findingsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in findingsEl.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object)
                    continue;

                var priority = TryGetInt(f, "priority");
                var findingConfidence = TryGetDouble(f, "confidence_score");
                var title = TryGetString(f, "title");
                var body = TryGetString(f, "body");

                ReviewCodeLocation? location = null;
                if (f.TryGetProperty("code_location", out _) || f.TryGetProperty("codeLocation", out _))
                {
                    location = TryParseReviewCodeLocation(f);
                }

                findings.Add(new ReviewFinding(priority, findingConfidence, title, body, location));
            }
        }

        return new ReviewOutput(correctness, explanation, confidence, findings);
    }

    private static ReviewCodeLocation? TryParseReviewCodeLocation(JsonElement finding)
    {
        if (!finding.TryGetProperty("code_location", out var loc) &&
            !finding.TryGetProperty("codeLocation", out loc))
            return null;

        if (loc.ValueKind != JsonValueKind.Object)
            return null;

        var file = TryGetString(loc, "absolute_file_path");
        ReviewLineRange? range = null;

        if (loc.TryGetProperty("line_range", out var lineRange) && lineRange.ValueKind == JsonValueKind.Object)
        {
            range = new ReviewLineRange(
                Start: TryGetInt(lineRange, "start"),
                End: TryGetInt(lineRange, "end"));
        }

        if (file is null && range is null)
            return null;

        return new ReviewCodeLocation(file, range);
    }

    private static PlanUpdateEvent? ParsePlanUpdateEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var name = TryGetString(payload, "name");
        var steps = new List<PlanStep>();

        if (payload.TryGetProperty("plan", out var planEl) && planEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in planEl.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object)
                    continue;

                var step = TryGetString(p, "step") ?? string.Empty;
                var status = TryGetString(p, "status") ?? string.Empty;
                steps.Add(new PlanStep(step, status));
            }
        }

        return new PlanUpdateEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Name = name, Plan = steps };
    }

    private static TaskStartedEvent ParseTaskStartedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        int? ctx = null;
        if (payload.TryGetProperty("model_context_window", out var ctxEl) && ctxEl.ValueKind == JsonValueKind.Number)
            ctx = ctxEl.GetInt32();

        return new TaskStartedEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, ModelContextWindow = ctx };
    }

    private static TaskCompleteEvent ParseTaskCompleteEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var last = TryGetString(payload, "last_agent_message");
        return new TaskCompleteEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, LastAgentMessage = last };
    }

    private static PatchApplyBeginEvent? ParsePatchApplyBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        bool? autoApproved = null;
        if (payload.TryGetProperty("auto_approved", out var autoEl) &&
            (autoEl.ValueKind == JsonValueKind.True || autoEl.ValueKind == JsonValueKind.False))
        {
            autoApproved = autoEl.GetBoolean();
        }

        var changes = new Dictionary<string, PatchApplyFileChange>(StringComparer.OrdinalIgnoreCase);
        if (payload.TryGetProperty("changes", out var changesEl) && changesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in changesEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var change = ParsePatchApplyFileChange(prop.Value);
                if (change != null)
                {
                    changes[prop.Name] = change;
                }
            }
        }

        return new PatchApplyBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            AutoApproved = autoApproved,
            Changes = changes
        };
    }

    private static PatchApplyFileChange? ParsePatchApplyFileChange(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        PatchApplyAddOperation? add = null;
        if (el.TryGetProperty("add", out var addEl) && addEl.ValueKind == JsonValueKind.Object)
        {
            var content = TryGetString(addEl, "content") ?? string.Empty;
            add = new PatchApplyAddOperation(content);
        }

        PatchApplyUpdateOperation? update = null;
        if (el.TryGetProperty("update", out var updateEl) && updateEl.ValueKind == JsonValueKind.Object)
        {
            update = new PatchApplyUpdateOperation(
                UnifiedDiff: TryGetString(updateEl, "unified_diff"),
                MovePath: TryGetString(updateEl, "move_path"),
                OriginalContent: TryGetString(updateEl, "original_content"),
                NewContent: TryGetString(updateEl, "new_content"));
        }

        PatchApplyDeleteOperation? delete = null;
        if (el.TryGetProperty("delete", out var delEl) && delEl.ValueKind == JsonValueKind.Object)
        {
            delete = new PatchApplyDeleteOperation();
        }

        if (add == null && update == null && delete == null)
            return null;

        return new PatchApplyFileChange { Add = add, Update = update, Delete = delete };
    }

    private static PatchApplyEndEvent? ParsePatchApplyEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        bool? success = null;
        if (payload.TryGetProperty("success", out var successEl) &&
            (successEl.ValueKind == JsonValueKind.True || successEl.ValueKind == JsonValueKind.False))
        {
            success = successEl.GetBoolean();
        }

        return new PatchApplyEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Stdout = TryGetString(payload, "stdout"),
            Stderr = TryGetString(payload, "stderr"),
            Success = success
        };
    }
}
