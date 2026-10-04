using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Overrides;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlMutationContractTests
{
    [Theory]
    [InlineData("agent_message", "{}", "agent_message")]
    [InlineData("agent_reasoning", "{}", "agent_reasoning")]
    [InlineData("user_message", "{}", "user_message")]
    [InlineData("compacted", "null", "compacted")]
    [InlineData("session_meta", "missing", "payload")]
    [InlineData("session_meta", "null", "non-object")]
    [InlineData("session_meta", "{}", "payload.id")]
    [InlineData("session_meta", "{\"id\":\" \"}", "empty")]
    [InlineData("turn_context", "null", "payload")]
    [InlineData("turn_context", "{}", "required")]
    [InlineData("event_msg", "null", "payload")]
    [InlineData("event_msg", "{}", "payload.type")]
    [InlineData("event", "null", "payload")]
    [InlineData("event", "{}", "payload.msg")]
    [InlineData("event", "{\"msg\":{}}", "payload.msg.type")]
    [InlineData("event_msg", "{\"type\":\"exited_review_mode\"}", "review_output")]
    [InlineData("event_msg", "{\"type\":\"exited_review_mode\",\"review_output\":false}", "invalid")]
    [InlineData("response_item", "missing", "payload")]
    [InlineData("response_item", "null", "non-object")]
    [InlineData("response_item", "{}", "payload.type")]
    [InlineData("future_type", "{}", "future_type")]
    public void MalformedOrUnknownEvents_ProduceActionableDiagnostics(string type, string payload, string diagnostic)
    {
        var logger = new RecordingLogger();
        var root = new JsonObject { ["timestamp"] = "2026-01-02T03:04:05Z", ["type"] = type };
        if (payload != "missing") root["payload"] = JsonNode.Parse(payload);
        new JsonlEventParser(logger).TryParseLine(root.ToJsonString(), out _, out _);
        logger.Messages.Should().Contain(message => message.Contains(diagnostic, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{}", "timestamp")]
    [InlineData("{\"timestamp\":\"2026-01-02T03:04:05Z\"}", "type")]
    [InlineData("{\"timestamp\":\"2026-01-02T03:04:05Z\",\"type\":\" \"}", "empty")]
    public void MalformedRoot_ExplainsSkippedLine(string line, string expected)
    {
        var logger = new RecordingLogger();
        new JsonlEventParser(logger).TryParseLine(line, out _, out _).Should().BeFalse();
        logger.Messages.Should().ContainSingle().Which.Should().Contain(expected);
    }

    [Fact]
    public void FailedOverrideHooks_AreDiagnosedAndDefaultMappingContinues()
    {
        var logger = new RecordingLogger();
        var parser = new JsonlEventParser(logger, Options.Create(new CodexClientOptions
        {
            EventTransformers = [new ThrowingHook()], EventMappers = [new ThrowingHook()]
        }));
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"agent_message","message":"hello"}""", out var evt, out _).Should().BeTrue();
        evt.Should().BeOfType<AgentMessageEvent>().Which.Text.Should().Be("hello");
        logger.Messages.Should().Contain(message => message.Contains("transformer"));
        logger.Messages.Should().Contain(message => message.Contains("mapper"));
    }

    private sealed class ThrowingHook : IExecEventMapper, IExecEventTransformer
    {
        public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload) => throw new InvalidOperationException("mapper failure");
        public (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload) => throw new InvalidOperationException("transformer failure");
    }

    private sealed class RecordingLogger : ILogger<JsonlEventParser>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    [Fact]
    public void TokenUsage_LegacyValuesTakePrecedenceIndependently()
    {
        var evt = Parse("token_count", """{"input_tokens":10,"output_tokens":20,"reasoning_output_tokens":30,"info":{"last_token_usage":{"input_tokens":1,"output_tokens":2,"reasoning_output_tokens":3}}}""").Should().BeOfType<TokenCountEvent>().Subject;
        evt.InputTokens.Should().Be(10); evt.OutputTokens.Should().Be(20); evt.ReasoningTokens.Should().Be(30);
        foreach (var field in new[] { "input_tokens", "output_tokens", "reasoning_output_tokens" })
            Parse("token_count", "{\"" + field + "\":5}").Should().BeOfType<TokenCountEvent>().Which.LastTokenUsage.Should().NotBeNull();
        Parse("token_count", """{"info":false,"rate_limits":{"primary":{"resets_in_seconds":false},"credits":{"balance":false}}}""").Should().BeOfType<TokenCountEvent>().Which.RateLimits!.Primary!.ResetsAt.Should().BeNull();
        Parse("token_count", """{"rate_limits":{"primary":{}}}""").Should().BeOfType<TokenCountEvent>().Which.RateLimits!.Primary.Should().NotBeNull();
        Parse("token_count", """{"rate_limits":{"secondary":{}}}""").Should().BeOfType<TokenCountEvent>().Which.RateLimits!.Secondary.Should().NotBeNull();
    }

    [Theory]
    [InlineData("background_event", "Message")]
    [InlineData("compaction_checkpoint_warning", "Message")]
    [InlineData("error", "Message")]
    [InlineData("agent_reasoning_raw_content", "Text")]
    public void TextAliases_HaveDefinedPrecedence(string type, string property)
    {
        var evt = Parse("event_msg", "{\"type\":\"" + type + "\",\"message\":\"message\",\"text\":\"text\"}");
        evt.GetType().GetProperty(property)!.GetValue(evt).Should().Be(type == "agent_reasoning_raw_content" ? "text" : "message");
    }

    [Fact]
    public void SessionAndContext_MetadataFieldsAndAliasPrecedence()
    {
        var meta = Parse("session_meta", """{"id":"s","cli_version":"version","originator":"origin","agent_role":"role","agent_type":"legacy"}""").Should().BeOfType<SessionMetaEvent>().Subject;
        meta.CliVersion.Should().Be("version"); meta.Originator.Should().Be("origin"); meta.AgentRole.Should().Be("role");
        Parse("session_meta", """{"id":"s","agent_type":"legacy"}""").Should().BeOfType<SessionMetaEvent>().Which.AgentRole.Should().Be("legacy");
        var context = Parse("turn_context", """{"approval_policy":"never","sandbox_policy_type":"read-only","cwd":"/repo","model":"gpt-5","summary":"auto","trace_id":"trace","current_date":"2026-10-04","timezone":"UTC","user_instructions":"user","developer_instructions":"developer","network":{"allowedDomains":["allow"],"denied_domains":["deny"],"deniedDomains":["ignored"]}}""").Should().BeOfType<TurnContextEvent>().Subject;
        context.TraceId.Should().Be("trace"); context.CurrentDate.Should().Be("2026-10-04"); context.Timezone.Should().Be("UTC");
        context.UserInstructions.Should().Be("user"); context.DeveloperInstructions.Should().Be("developer");
        context.Network!.AllowedDomains.Should().Equal("allow"); context.Network.DeniedDomains.Should().Equal("deny");
    }

    [Theory]
    [InlineData("{\"network_access\":true,\"nested\":{\"network_access\":false}}", true)]
    [InlineData("{\"first\":{\"network_access\":true},\"second\":{\"network_access\":false}}", true)]
    [InlineData("{\"network_access\":\" \",\"fallback\":{\"network_access\":true}}", true)]
    [InlineData("{\"network_access\":{},\"fallback\":{\"network_access\":true}}", true)]
    [InlineData("{\"network_access\":0,\"fallback\":{\"network_access\":true}}", true)]
    public void NetworkAccess_FirstValidValueWins(string policy, bool expected)
    {
        var evt = Parse("turn_context", "{\"approval_policy\":\"never\",\"sandbox_policy_type\":\"read-only\",\"cwd\":\"/repo\",\"model\":\"gpt-5\",\"summary\":\"auto\",\"sandbox_policy\":" + policy + "}").Should().BeOfType<TurnContextEvent>().Subject;
        evt.NetworkAccess.Should().Be(expected);
    }

    [Fact]
    public void Reviews_PreserveTextAndPartialLocations()
    {
        var entered = Parse("event_msg", """{"type":"entered_review_mode","target":{"type":"branch"}}""").Should().BeOfType<EnteredReviewModeEvent>().Subject;
        entered.Target!.Type.Should().Be("branch");
        var review = Parse("event_msg", """{"type":"exited_review_mode","review_output":{"overall_explanation":"explanation","findings":[{"title":"title","body":"body","confidence_score":0.9,"code_location":{"absolute_file_path":"/file"}},{"code_location":{"line_range":{"start":1}}}]}}""").Should().BeOfType<ExitedReviewModeEvent>().Which.ReviewOutput!;
        review.Findings[0].ConfidenceScore.Should().Be(0.9);
        review.OverallExplanation.Should().Be("explanation"); review.Findings[0].Title.Should().Be("title"); review.Findings[0].Body.Should().Be("body");
        review.Findings[0].CodeLocation.Should().Be(new ReviewCodeLocation("/file", null));
        review.Findings[1].CodeLocation.Should().Be(new ReviewCodeLocation(null, new ReviewLineRange(1, null)));
    }

    [Fact]
    public void CollabMetadataAndNestedText_PreferCurrentFields()
    {
        var evt = Parse("event_msg", """{"type":"collab_waiting_end","call_id":"c","sender_thread_id":"s","statuses":{"r":{"completed":{"message":"message","payload":{"text":"ignored"}}},"r2":{"completed":{"payload":{"text":"text","message":"ignored"}}}},"agent_statuses":[{"thread_id":"r","agent_role":"role","agent_type":"legacy","status":"running"}]}""").Should().BeOfType<CollabWaitingEndEvent>().Subject;
        evt.AgentStatuses[0].AgentRole.Should().Be("role"); evt.StatusInfos!["r"].PayloadText.Should().Be("message"); evt.StatusInfos["r2"].PayloadText.Should().Be("text");
    }

    [Theory]
    [InlineData("response_item", "{\"type\":\"tool_search_output\",\"tools\":false}")]
    [InlineData("response_item", "{\"type\":\"ghost_snapshot\",\"ghost_commit\":false}")]
    [InlineData("event_msg", "{\"type\":\"exec_command_end\",\"call_id\":\"c\",\"command\":false}")]
    [InlineData("event_msg", "{\"type\":\"plan_update\",\"plan\":false}")]
    [InlineData("turn_context", "{\"approval_policy\":\"never\",\"sandbox_policy_type\":\"read-only\",\"cwd\":\"/repo\",\"model\":\"gpt-5\",\"summary\":\"auto\",\"sandbox_policy\":false}")]
    [InlineData("turn_context", "{\"approval_policy\":\"never\",\"sandbox_policy_type\":\"read-only\",\"cwd\":\"/repo\",\"model\":\"gpt-5\",\"summary\":\"auto\",\"sandbox_policy\":{\"type\":false}}")]
    public void InvalidOptionalShapes_DoNotDiscardTheEvent(string type, string body)
    {
        Parse(type, body).Should().NotBeNull();
    }

    [Fact]
    public void ResponseAndCompaction_PreserveDefaultTypesAndText()
    {
        var parser = new JsonlEventParser(new RecordingLogger());
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"response_item"}""", out var evt, out _).Should().BeTrue();
        var response = evt.Should().BeOfType<ResponseItemEvent>().Subject;
        response.PayloadType.Should().Be("unknown"); response.Payload.PayloadType.Should().Be("unknown");
        Parse("response_item", "null").Should().BeOfType<ResponseItemEvent>().Which.PayloadType.Should().Be("unknown");
        Parse("compacted", """{"message":"summary"}""").Should().BeOfType<CompactedEvent>().Which.Message.Should().Be("summary");
        var output = Parse("response_item", """{"type":"function_call_output","output":[{"type":"input_text","text":"tool text"}]}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<FunctionCallOutputResponseItemPayload>().Subject;
        output.OutputContent![0].Should().BeOfType<FunctionToolOutputInputTextPart>().Which.Text.Should().Be("tool text");
    }
    [Theory]
    [InlineData("{\"content\":[]}", null)]
    [InlineData("{\"text\":\"\",\"content\":[{},false]}", "")]
    [InlineData("""{"text":" ","content":[{"text":"fallback"}]}""", "fallback")]
    [InlineData("{\"text\":\"direct\",\"content\":[{\"text\":\"ignored\"}]}", "direct")]
    public void CompletedItem_UsesFirstNonBlankContentOnlyWhenTextIsBlank(string item, string? expected)
    {
        Parse("event_msg", "{\"type\":\"item_completed\",\"item\":" + item + "}").Should().BeOfType<TurnItemCompletedEvent>().Which.Text.Should().Be(expected);
    }

    [Fact]
    public void NullRequiredContextSummary_IsRejected()
    {
        var parser = new JsonlEventParser(new RecordingLogger());
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"turn_context","payload":{"approval_policy":"never","sandbox_policy_type":"read-only","cwd":"/repo","model":"gpt-5","summary":null}}""", out var evt, out _).Should().BeFalse();
        evt.Should().BeNull();
    }

    [Theory]
    [InlineData("user_message")]
    [InlineData("agent_message")]
    [InlineData("agent_reasoning")]
    public void NullText_IsNotAnEmptyMessage(string type)
    {
        var parser = new JsonlEventParser(new RecordingLogger());
        parser.TryParseLine("{\"timestamp\":\"2026-01-02T03:04:05Z\",\"type\":\"" + type + "\",\"payload\":{\"text\":null}}", out var evt, out _).Should().BeFalse();
        evt.Should().BeNull();
    }

    [Fact]
    public void NullHooks_AreIgnoredWithoutSpuriousFailureDiagnostics()
    {
        var logger = new RecordingLogger();
        var parser = new JsonlEventParser(logger, Options.Create(new CodexClientOptions
        {
            EventTransformers = new IExecEventTransformer[] { null! }, EventMappers = new IExecEventMapper[] { null! }
        }));
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"agent_message","message":"hello"}""", out _, out _).Should().BeTrue();
        logger.Messages.Should().BeEmpty();
    }

}
