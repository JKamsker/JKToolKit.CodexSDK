using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventEnvelopeParsers
{
    internal static WebSearchAction? ParseWebSearchAction(JsonElement actionEl)
    {
        if (actionEl.ValueKind != JsonValueKind.Object)
            return null;

        IReadOnlyList<string>? queries = null;
        if (actionEl.TryGetProperty("queries", out var queriesEl) && queriesEl.ValueKind == JsonValueKind.Array)
        {
            queries = queriesEl.EnumerateArray()
                .Select(q => q.ValueKind == JsonValueKind.String ? q.GetString() : null)
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Cast<string>()
                .ToArray();
        }

        return new WebSearchAction(
            Type: TryGetString(actionEl, "type"),
            Query: TryGetString(actionEl, "query"),
            Queries: queries)
        {
            Url = TryGetString(actionEl, "url"),
            Pattern = TryGetString(actionEl, "pattern")
        };
    }

    private static WebSearchEndEvent? ParseWebSearchEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        WebSearchAction? action = null;
        if (payload.TryGetProperty("action", out var actionEl))
        {
            action = ParseWebSearchAction(actionEl);
        }

        return new WebSearchEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Query = TryGetString(payload, "query"),
            Action = action
        };
    }

    private static ExecCommandEndEvent? ParseExecCommandEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        IReadOnlyList<string>? command = null;
        if (payload.TryGetProperty("command", out var cmdEl) && cmdEl.ValueKind == JsonValueKind.Array)
        {
            command = cmdEl.EnumerateArray()
                .Select(s => s.ValueKind == JsonValueKind.String ? s.GetString() : null)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToArray();
        }

        return new ExecCommandEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            ProcessId = TryGetString(payload, "process_id"),
            TurnId = TryGetString(payload, "turn_id"),
            Command = command,
            Cwd = TryGetString(payload, "cwd"),
            Source = TryGetString(payload, "source"),
            InteractionInput = TryGetString(payload, "interaction_input"),
            Stdout = TryGetString(payload, "stdout"),
            Stderr = TryGetString(payload, "stderr"),
            AggregatedOutput = TryGetString(payload, "aggregated_output"),
            ExitCode = TryGetInt(payload, "exit_code"),
            Duration = TryGetString(payload, "duration"),
            FormattedOutput = TryGetString(payload, "formatted_output"),
            Status = TryGetString(payload, "status")
        };
    }

    private static McpToolCallEndEvent? ParseMcpToolCallEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        string? server = null;
        string? tool = null;
        string? argsJson = null;
        if (payload.TryGetProperty("invocation", out var invocationEl) && invocationEl.ValueKind == JsonValueKind.Object)
        {
            server = TryGetString(invocationEl, "server");
            tool = TryGetString(invocationEl, "tool");
            if (invocationEl.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind != JsonValueKind.Null)
            {
                argsJson = argsEl.ValueKind == JsonValueKind.String ? argsEl.GetString() : argsEl.GetRawText();
            }
        }

        string? resultJson = null;
        if (payload.TryGetProperty("result", out var resultEl))
        {
            resultJson = resultEl.ValueKind == JsonValueKind.String ? resultEl.GetString() : resultEl.GetRawText();
        }

        return new McpToolCallEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Server = server,
            Tool = tool,
            Duration = TryGetString(payload, "duration"),
            ArgumentsJson = argsJson,
            ResultJson = resultJson
        };
    }

    private static ViewImageToolCallEvent? ParseViewImageToolCallEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        var path = TryGetString(payload, "path");
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(path))
            return null;

        return new ViewImageToolCallEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Path = path
        };
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

    private static ExitedReviewModeEvent? ParseExitedReviewModeEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
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

        return new TaskStartedEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            TurnId = TryGetString(payload, "turn_id"),
            ModelContextWindow = ctx
        };
    }

    private static TaskCompleteEvent ParseTaskCompleteEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var last = TryGetString(payload, "last_agent_message");
        return new TaskCompleteEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            TurnId = TryGetString(payload, "turn_id"),
            LastAgentMessage = last
        };
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

        return new PatchApplyEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Stdout = TryGetString(payload, "stdout"),
            Stderr = TryGetString(payload, "stderr"),
            Success = success,
            Status = TryGetString(payload, "status"),
            Changes = changes.Count == 0 ? null : changes
        };
    }
}
