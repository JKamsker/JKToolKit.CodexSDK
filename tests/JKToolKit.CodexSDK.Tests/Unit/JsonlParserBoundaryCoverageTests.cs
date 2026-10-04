using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Overrides;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlParserBoundaryCoverageTests
{
    [Fact]
    public void Constructor_RequiresLoggerInBothOverloads()
    {
        Action first = () => new JsonlEventParser(null!);
        Action second = () => new JsonlEventParser(null!, null);
        first.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        second.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance, null);
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"future"}""", out var evt, out _).Should().BeTrue();
        evt.Should().BeOfType<UnknownCodexEvent>();
    }

    [Fact]
    public void NullHooksAndDecliningMapper_DoNotPreventBuiltInParsing()
    {
        var mapper = new DecliningMapper();
        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance, Options.Create(new CodexClientOptions
        {
            EventTransformers = new IExecEventTransformer[] { null! },
            EventMappers = new IExecEventMapper[] { null!, mapper }
        }));
        parser.TryParseLine("""{"timestamp":"2026-01-02T03:04:05Z","type":"agent_message","message":"hello"}""", out var evt, out _).Should().BeTrue();
        evt.Should().BeOfType<AgentMessageEvent>().Which.Text.Should().Be("hello");
        mapper.Calls.Should().Be(1);
    }

    private sealed class DecliningMapper : IExecEventMapper
    {
        public int Calls { get; private set; }
        public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload) { Calls++; return null; }
    }

    [Fact]
    public async Task ParseAsync_SkipsEmptyLinesWithoutLosingFollowingEvent()
    {
        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance);
        var events = await parser.ParseAsync(new[] { "", " \t", "{", """{"timestamp":"2026-01-02T03:04:05Z","type":"agent_message","text":"hello"}""" }.ToAsyncEnumerable()).ToListAsync();
        events.Should().ContainSingle().Which.Should().BeOfType<AgentMessageEvent>().Which.Text.Should().Be("hello");
    }

    [Theory]
    [InlineData("event")]
    [InlineData("event_msg")]
    [InlineData("compacted")]
    [InlineData("turn_context")]
    public void MissingPayload_IsRejectedOrPreservedAccordingToEnvelopeContract(string type)
    {
        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance);
        var result = parser.TryParseLine("{\"timestamp\":\"2026-01-02T03:04:05Z\",\"type\":\"" + type + "\"}", out var evt, out _);
        result.Should().Be(type is "event" or "event_msg");
        if (result) evt.Should().BeOfType<UnknownCodexEvent>(); else evt.Should().BeNull();
    }

    [Fact]
    public void OptionalContextFields_PreserveStructuredJsonAndParsedPolicies()
    {
        var context = Parse("turn_context", """{"approval_policy":"never","sandbox_policy_type":"read-only","cwd":"/repo","model":"gpt-5","summary":{},"effort":"high","collaboration_mode":{"mode":"plan"},"final_output_json_schema":{"type":"object"},"truncation_policy":{"mode":"tokens","budget":100}}""").Should().BeOfType<TurnContextEvent>().Subject;
        context.CollaborationMode!.Value.GetProperty("mode").GetString().Should().Be("plan");
        context.FinalOutputJsonSchema!.Value.GetProperty("type").GetString().Should().Be("object");
        context.TruncationPolicy!.Value.GetProperty("budget").GetInt32().Should().Be(100);
        context.ReasoningEffort!.Value.Value.Should().Be("high");
        context.ParsedApprovalPolicy.Should().NotBeNull(); context.ParsedSandboxMode.Should().NotBeNull();
        (context with { ApprovalPolicy = null, SandboxPolicyType = null }).ParsedApprovalPolicy.Should().BeNull();
        (context with { ApprovalPolicy = null, SandboxPolicyType = null }).ParsedSandboxMode.Should().BeNull();
    }

    [Theory]
    [InlineData("function_call", "ArgumentsJson", "arguments")]
    [InlineData("function_call_output", "Output", "output")]
    [InlineData("custom_tool_call", "Input", "input")]
    [InlineData("custom_tool_call_output", "Output", "output")]
    public void StringToolValues_ArePreservedWithoutStructuredOutput(string type, string property, string wireProperty)
    {
        var evt = Parse("response_item", "{\"type\":\"" + type + "\",\"" + wireProperty + "\":\"literal\"}").Should().BeOfType<ResponseItemEvent>().Subject;
        evt.Payload.GetType().GetProperty(property)!.GetValue(evt.Payload).Should().Be("literal");
        if (evt.Payload is FunctionCallResponseItemPayload call) call.Arguments.Should().BeNull();
        if (evt.Payload is FunctionCallOutputResponseItemPayload output) { output.OutputJson.Should().BeNull(); output.OutputContent.Should().BeNull(); }
        if (evt.Payload is CustomToolCallResponseItemPayload custom) custom.InputJson.Should().BeNull();
        if (evt.Payload is CustomToolCallOutputResponseItemPayload customOutput) { customOutput.OutputJson.Should().BeNull(); customOutput.OutputContent.Should().BeNull(); }
    }

    [Theory]
    [InlineData("false")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"future\"}")]
    [InlineData("{\"type\":\"exec\",\"command\":false,\"env\":false}")]
    public void ShellAction_MissingOrInvalidFieldsRemainAbsent(string action)
    {
        var shell = Parse("response_item", "{\"type\":\"local_shell_call\",\"action\":" + action + "}").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<LocalShellCallResponseItemPayload>().Subject;
        shell.Command.Should().BeNull(); shell.TimeoutMs.Should().BeNull(); shell.Env.Should().BeNull();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("{\"queries\":false}")]
    [InlineData("{\"queries\":[]}")]
    public void WebSearchAction_InvalidShapeAndEmptyQueries(string action)
    {
        var search = Parse("response_item", "{\"type\":\"web_search_call\",\"action\":" + action + ",\"results\":false}").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<WebSearchCallResponseItemPayload>().Subject;
        search.Results.Should().BeNull();
        if (action == "false") search.Action.Should().BeNull();
        else if (action.Contains("[]")) search.Action!.Queries.Should().BeEmpty();
        else search.Action!.Queries.Should().BeNull();
    }

    [Fact]
    public void OptionalCollections_MissingOrWrongShapesRemainEmpty()
    {
        Parse("response_item", """{"type":"reasoning","summary":null}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<ReasoningResponseItemPayload>().Which.SummaryTexts.Should().BeEmpty();
        Parse("response_item", """{"type":"ghost_snapshot","ghost_commit":{}}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<GhostSnapshotResponseItemPayload>().Which.GhostCommit!.PreexistingUntrackedDirs.Should().BeNull();
        Parse("event_msg", """{"type":"item_completed","item":false}""").Should().BeOfType<TurnItemCompletedEvent>().Which.Text.Should().BeNull();
        Parse("event_msg", """{"type":"exited_review_mode","review_output":{"findings":false}}""").Should().BeOfType<ExitedReviewModeEvent>().Which.ReviewOutput!.Findings.Should().BeEmpty();
        Parse("event_msg", """{"type":"mcp_tool_call_end","call_id":"c","invocation":false}""").Should().BeOfType<McpToolCallEndEvent>().Which.Server.Should().BeNull();
        Parse("compacted", """{"replacement_history":false}""").Should().BeOfType<CompactedEvent>().Which.ReplacementHistory.Should().BeEmpty();
        var token = Parse("token_count", """{"info":{},"output_tokens":2,"reasoning_output_tokens":3,"rate_limits":{"credits":{}}}""").Should().BeOfType<TokenCountEvent>().Subject;
        token.LastTokenUsage.Should().Be(new TokenUsage(null, null, 2, 3, null)); token.RateLimits!.Credits.Should().Be(new RateLimitCredits(null, null, null));
    }
    [Fact]
    public void MinimalOptionalObjects_AndStructuredSearchResults()
    {
        var usage = Parse("token_count", """{"info":{"last_token_usage":{"input_tokens":9}}}""").Should().BeOfType<TokenCountEvent>().Subject;
        usage.InputTokens.Should().Be(9);
        Parse("response_item", """{"type":"reasoning"}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<ReasoningResponseItemPayload>().Which.SummaryTexts.Should().BeEmpty();
        Parse("response_item", """{"type":"message"}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<MessageResponseItemPayload>().Which.Content.Should().BeEmpty();
        var shell = Parse("response_item", """{"type":"local_shell_call","action":{"type":"exec"}}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<LocalShellCallResponseItemPayload>().Subject;
        shell.Command.Should().BeNull(); shell.Env.Should().BeNull();
        var search = Parse("response_item", """{"type":"web_search_call","results":[{"title":"result"},null]}""").Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<WebSearchCallResponseItemPayload>().Subject;
        search.Results.Should().HaveCount(2); search.Results![0].GetProperty("title").GetString().Should().Be("result"); search.Results[1].ValueKind.Should().Be(JsonValueKind.Null);
        Parse("event_msg", """{"type":"item_completed"}""").Should().BeOfType<TurnItemCompletedEvent>().Which.Text.Should().BeNull();
        Parse("event_msg", """{"type":"exited_review_mode","review_output":{}}""").Should().BeOfType<ExitedReviewModeEvent>().Which.ReviewOutput!.Findings.Should().BeEmpty();
        Parse("event_msg", """{"type":"mcp_tool_call_end","call_id":"c","invocation":{}}""").Should().BeOfType<McpToolCallEndEvent>().Which.ArgumentsJson.Should().BeNull();
        Parse("compacted", "{}").Should().BeOfType<CompactedEvent>().Which.ReplacementHistory.Should().BeEmpty();
    }

}
