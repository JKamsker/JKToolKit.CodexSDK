using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlParserCoverageTests
{
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
    private static readonly JsonlEventParser Parser = new(NullLogger<JsonlEventParser>.Instance);

    internal static CodexEvent Parse(string type, string payload)
    {
        var line = JsonSerializer.Serialize(new { timestamp = Timestamp, type, payload = JsonSerializer.Deserialize<JsonElement>(payload) });
        Parser.TryParseLine(line, out var result, out var error).Should().BeTrue(error);
        result!.Timestamp.Should().Be(Timestamp);
        result.RawPayload.GetProperty("type").GetString().Should().Be(type);
        return result;
    }

    private static string Envelope(string type, string body, int shape)
    {
        var payload = JsonNode.Parse(body)!.AsObject();
        payload["type"] = type;
        return shape switch
        {
            0 => payload.ToJsonString(),
            1 => new JsonObject { ["msg"] = payload }.ToJsonString(),
            2 => new JsonObject { ["type"] = type, ["payload"] = payload }.ToJsonString(),
            _ => new JsonObject { ["type"] = type, ["msg"] = payload }.ToJsonString()
        };
    }

    public static IEnumerable<object[]> EventCases()
    {
        var cases = new (string Type, Type Class, string Body, string? Property, object? Value)[]
        {
            ("agent_message", typeof(AgentMessageEvent), "{\"message\":\"hello\"}", "Text", "hello"),
            ("agent_reasoning", typeof(AgentReasoningEvent), "{\"text\":\"think\"}", "Text", "think"),
            ("agent_reasoning_raw_content", typeof(AgentReasoningRawContentEvent), "{\"message\":\"raw\"}", "Text", "raw"),
            ("agent_reasoning_section_break", typeof(AgentReasoningSectionBreakEvent), "{}", null, null),
            ("user_message", typeof(UserMessageEvent), "{\"text\":\"ask\"}", "Text", "ask"),
            ("token_count", typeof(TokenCountEvent), "{\"input_tokens\":7}", "InputTokens", 7),
            ("context_compacted", typeof(ContextCompactedEvent), "{}", null, null),
            ("thread_rolled_back", typeof(ThreadRolledBackEvent), "{\"num_turns\":2}", "NumTurns", 2),
            ("undo_completed", typeof(UndoCompletedEvent), "{\"success\":false}", "Success", false),
            ("item_completed", typeof(TurnItemCompletedEvent), "{\"item\":{\"text\":\"item\"}}", "Text", "item"),
            ("background_event", typeof(BackgroundEvent), "{\"text\":\"background\"}", "Message", "background"),
            ("compaction_checkpoint_warning", typeof(CompactionCheckpointWarningEvent), "{\"text\":\"warning\"}", "Message", "warning"),
            ("error", typeof(ErrorEvent), "{\"text\":\"error\"}", "Message", "error"),
            ("web_search_end", typeof(WebSearchEndEvent), "{\"call_id\":\"search\"}", "CallId", "search"),
            ("exec_command_end", typeof(ExecCommandEndEvent), "{\"call_id\":\"exec\"}", "CallId", "exec"),
            ("mcp_tool_call_end", typeof(McpToolCallEndEvent), "{\"call_id\":\"mcp\"}", "CallId", "mcp"),
            ("view_image_tool_call", typeof(ViewImageToolCallEvent), "{\"call_id\":\"image\",\"path\":\"/image.png\"}", "Path", "/image.png"),
            ("patch_apply_begin", typeof(PatchApplyBeginEvent), "{\"call_id\":\"patch\"}", "CallId", "patch"),
            ("patch_apply_end", typeof(PatchApplyEndEvent), "{\"call_id\":\"patch\"}", "CallId", "patch"),
            ("plan_update", typeof(PlanUpdateEvent), "{\"explanation\":\"why\"}", "Explanation", "why"),
            ("task_started", typeof(TaskStartedEvent), "{\"turn_id\":\"start\"}", "TurnId", "start"),
            ("turn_started", typeof(TaskStartedEvent), "{\"turn_id\":\"start\"}", "TurnId", "start"),
            ("task_complete", typeof(TaskCompleteEvent), "{\"last_agent_message\":\"done\"}", "LastAgentMessage", "done"),
            ("turn_complete", typeof(TaskCompleteEvent), "{\"last_agent_message\":\"done\"}", "LastAgentMessage", "done"),
            ("turn_aborted", typeof(TurnAbortedEvent), "{\"reason\":\"cancel\"}", "Reason", "cancel"),
            ("turn_diff", typeof(TurnDiffEvent), "{\"unified_diff\":\"diff\"}", "UnifiedDiff", "diff"),
            ("entered_review_mode", typeof(EnteredReviewModeEvent), "{\"prompt\":\"review\"}", "Prompt", "review"),
            ("exited_review_mode", typeof(ExitedReviewModeEvent), "{}", "ReviewOutput", null),
            ("future_event", typeof(UnknownCodexEvent), "{}", null, null)
        };
        foreach (var entry in cases)
            for (var shape = 0; shape < 4; shape++)
                yield return [entry.Type, entry.Class, entry.Body, entry.Property!, entry.Value!, shape];
        foreach (var (type, cls) in new (string, Type)[]
        {
            ("collab_agent_spawn_begin", typeof(CollabAgentSpawnBeginEvent)),
            ("collab_agent_spawn_end", typeof(CollabAgentSpawnEndEvent)),
            ("collab_agent_interaction_begin", typeof(CollabAgentInteractionBeginEvent)),
            ("collab_agent_interaction_end", typeof(CollabAgentInteractionEndEvent)),
            ("collab_waiting_begin", typeof(CollabWaitingBeginEvent)),
            ("collab_waiting_end", typeof(CollabWaitingEndEvent)),
            ("collab_close_begin", typeof(CollabCloseBeginEvent)),
            ("collab_close_end", typeof(CollabCloseEndEvent)),
            ("collab_resume_begin", typeof(CollabResumeBeginEvent)),
            ("collab_resume_end", typeof(CollabResumeEndEvent))
        })
            for (var shape = 0; shape < 4; shape++)
                yield return [type, cls, CollabBody, "CallId", "call", shape];
    }

    private const string CollabBody = """{"call_id":"call","sender_thread_id":"sender","receiver_thread_id":"receiver","prompt":"prompt","model":"model","reasoning_effort":"high","status":"running","receiver_thread_ids":["receiver"],"statuses":{"receiver":"running"}}""";

    [Theory, MemberData(nameof(EventCases))]
    public void Envelopes_DispatchEverySupportedEvent(string type, Type expectedType, string body, string? property, object? expected, int shape)
    {
        var fields = JsonNode.Parse(body)!.AsObject();
        var optional = type switch
        {
            "exec_command_end" => """{"process_id":"process","turn_id":"turn","cwd":"/repo","source":"agent","interaction_input":"input","stdout":"stdout","stderr":"stderr","aggregated_output":"aggregate","exit_code":7,"duration":"1s","formatted_output":"formatted","status":"completed"}""",
            "web_search_end" => """{"query":"query"}""",
            "mcp_tool_call_end" => """{"duration":"2s"}""",
            "patch_apply_begin" => """{"auto_approved":true}""",
            "patch_apply_end" => """{"stdout":"stdout","stderr":"stderr","success":true,"status":"completed"}""",
            "plan_update" => """{"name":"plan","explanation":"explain"}""",
            "task_started" or "turn_started" => """{"model_context_window":123}""",
            "task_complete" or "turn_complete" => """{"turn_id":"turn"}""",
            "entered_review_mode" => """{"user_facing_hint":"hint"}""",
            "undo_completed" => """{"message":"undone"}""",
            "item_completed" => """{"thread_id":"thread","turn_id":"turn"}""",
            _ when type.StartsWith("collab_") => """{"receiver_agent_nickname":"nickname","receiver_agent_role":"role","new_thread_id":"newthread","new_agent_nickname":"newnick","new_agent_role":"newrole"}""",
            _ => "{}"
        };
        foreach (var field in JsonNode.Parse(optional)!.AsObject())
            if (!fields.ContainsKey(field.Key)) fields[field.Key] = field.Value?.DeepClone();
        var evt = Parse(shape == 1 ? "event" : "event_msg", Envelope(type, fields.ToJsonString(), shape));
        evt.Should().BeOfType(expectedType);
        evt.Type.Should().Be(type);
        if (property is not null)
            expectedType.GetProperty(property)!.GetValue(evt).Should().Be(expected);
        // Each scalar wire field follows the public PascalCase property convention.
        // Check distinct sentinels so swapping, dropping, or hardcoding a field is observable.
        foreach (var field in fields)
        {
            var name = string.Concat(field.Key.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
            var target = expectedType.GetProperty(name);
            if (target is null || field.Value is not JsonValue scalar) continue;
            var propertyType = Nullable.GetUnderlyingType(target.PropertyType) ?? target.PropertyType;
            if (propertyType == typeof(string) || propertyType == typeof(int) || propertyType == typeof(bool))
                target.GetValue(evt).Should().Be(JsonSerializer.Deserialize(scalar.ToJsonString(), propertyType), $"{type}.{field.Key} must be preserved");
        }
    }

    public static IEnumerable<object[]> RequiredFields()
    {
        foreach (var row in EventCases().Where(x => (int)x[5] == 0))
        {
            var type = (string)row[0];
            var body = JsonNode.Parse((string)row[2])!.AsObject();
            var fields = type switch
            {
                "agent_message" or "agent_reasoning" or "agent_reasoning_raw_content" or "user_message" or "background_event" or "compaction_checkpoint_warning" or "error" => new[] { body.ContainsKey("text") ? "text" : "message" },
                "thread_rolled_back" => ["num_turns"],
                "undo_completed" => ["success"],
                "turn_aborted" => ["reason"],
                "turn_diff" => ["unified_diff"],
                "view_image_tool_call" => ["call_id", "path"],
                "web_search_end" or "exec_command_end" or "mcp_tool_call_end" or "patch_apply_begin" or "patch_apply_end" => ["call_id"],
                "collab_agent_spawn_begin" => ["call_id", "sender_thread_id", "prompt", "model", "reasoning_effort"],
                "collab_agent_spawn_end" => ["call_id", "sender_thread_id", "prompt", "model", "reasoning_effort", "status"],
                "collab_agent_interaction_begin" => ["call_id", "sender_thread_id", "receiver_thread_id", "prompt"],
                "collab_agent_interaction_end" => ["call_id", "sender_thread_id", "receiver_thread_id", "prompt", "status"],
                "collab_waiting_begin" => ["call_id", "sender_thread_id", "receiver_thread_ids"],
                "collab_waiting_end" => ["call_id", "sender_thread_id", "statuses"],
                "collab_close_begin" or "collab_resume_begin" => ["call_id", "sender_thread_id", "receiver_thread_id"],
                "collab_close_end" or "collab_resume_end" => ["call_id", "sender_thread_id", "receiver_thread_id", "status"],
                _ => Array.Empty<string>()
            };
            foreach (var field in fields)
                foreach (var wrong in new[] { false, true })
                {
                    var invalid = body.DeepClone().AsObject();
                    if (wrong) invalid[field] = field == "receiver_thread_ids" ? new JsonObject() : new JsonArray(); else invalid.Remove(field);
                    invalid["type"] = type;
                    yield return [type, field, wrong, invalid.ToJsonString()];
                }
        }
    }

    [Theory, MemberData(nameof(RequiredFields))]
    public void RequiredFields_MissingOrWrongShapeRejectEvent(string type, string field, bool wrong, string body)
    {
        var line = JsonSerializer.Serialize(new { timestamp = Timestamp, type = "event_msg", payload = JsonSerializer.Deserialize<JsonElement>(body) });
        Parser.TryParseLine(line, out var evt, out var error).Should().BeFalse($"{type}.{field} must reject {(wrong ? "an array" : "absence")}");
        evt.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("event_msg", "null")]
    [InlineData("event_msg", "{}")]
    [InlineData("event_msg", "{\"type\":false}")]
    [InlineData("event", "null")]
    [InlineData("event", "{}")]
    [InlineData("event", "{\"msg\":null}")]
    [InlineData("event", "{\"msg\":{}}")]
    [InlineData("event", "{\"msg\":{\"type\":\" \"}}")]
    public void InvalidEnvelope_PreservesUnknown(string type, string body)
    {
        Parse(type, body).Should().BeOfType<UnknownCodexEvent>().Which.Type.Should().Be(type);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("{", false)]
    [InlineData("[]", false)]
    [InlineData("{\"timestamp\":\"2026-01-02T03:04:05Z\",\"type\":\"\"}", false)]
    [InlineData("{\"timestamp\":\"2026-01-02T03:04:05Z\",\"type\":false}", true)]
    public void TryParseLine_ReportsInvalidInputWithoutThrowing(string line, bool success)
    {
        Parser.TryParseLine(line, out var evt, out var error).Should().Be(success);
        if (success) { evt.Should().BeOfType<UnknownCodexEvent>(); error.Should().BeNull(); }
        else { evt.Should().BeNull(); error.Should().NotBeNullOrEmpty(); }
    }
    public static IEnumerable<object[]> NonBlankRequiredFields()
    {
        foreach (var row in RequiredFields().Where(row => !(bool)row[2]))
        {
            var type = (string)row[0];
            var field = (string)row[1];
            if (field is "prompt" or "status" or "success" or "num_turns" or "receiver_thread_ids" or "statuses") continue;
            if (type is "agent_message" or "agent_reasoning" or "user_message") continue;
            foreach (var blank in new[] { "", " \t" })
            {
                var payload = JsonNode.Parse((string)row[3])!.AsObject();
                payload[field] = blank;
                yield return [type, field, blank, payload.ToJsonString()];
            }
        }
    }

    [Theory, MemberData(nameof(NonBlankRequiredFields))]
    public void RequiredIdentifiers_RejectEmptyAndWhitespace(string type, string field, string blank, string body)
    {
        var line = JsonSerializer.Serialize(new { timestamp = Timestamp, type = "event_msg", payload = JsonSerializer.Deserialize<JsonElement>(body) });
        Parser.TryParseLine(line, out var evt, out _).Should().BeFalse($"{type}.{field} must reject {JsonSerializer.Serialize(blank)}");
        evt.Should().BeNull();
    }

    [Theory]
    [InlineData("user_message")]
    [InlineData("agent_message")]
    [InlineData("agent_reasoning")]
    public void MessageFields_PreferMessageThenTextAndAllowEmptyText(string type)
    {
        foreach (var shape in new[] { 0, 1, 2, 3 })
        {
            var evt = Parse(shape == 1 ? "event" : "event_msg", Envelope(type, """{"message":"first","text":"second"}""", shape));
            evt.GetType().GetProperty("Text")!.GetValue(evt).Should().Be("first");
            evt = Parse(shape == 1 ? "event" : "event_msg", Envelope(type, """{"message":false,"text":""}""", shape));
            evt.GetType().GetProperty("Text")!.GetValue(evt).Should().Be("");
        }
    }

}
