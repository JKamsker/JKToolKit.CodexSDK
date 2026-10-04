using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlContextCoverageTests
{
    [Theory]
    [InlineData("null", null, null)]
    [InlineData("false", null, null)]
    [InlineData("\"cli\"", "cli", null)]
    [InlineData("{\"future\":1}", "{\"future\":1}", null)]
    [InlineData("{\"subagent\":\"review\"}", "subagent", "review")]
    [InlineData("{\"subagent\":null}", "subagent", null)]
    [InlineData("{\"subagent\":{}}", "subagent", null)]
    [InlineData("{\"subagent\":{\"other\":\"custom\"}}", "subagent", "custom")]
    [InlineData("{\"subagent\":{\"other\":false}}", "subagent", "other")]
    [InlineData("{\"subagent\":{\"future\":{}}}", "subagent", "future")]
    public void SessionSource_PreservesUnknownValuesAndSubagentKinds(string source, string? expected, string? subagent)
    {
        var evt = Parse("session_meta", "{\"id\":\"session\",\"source\":" + source + "}").Should().BeOfType<SessionMetaEvent>().Subject;
        evt.Source.Should().Be(expected); evt.SourceSubagent.Should().Be(subagent);
        if (source == "null") evt.SourceJson.Should().BeNull();
        else evt.SourceJson!.Value.GetRawText().Should().Be(source);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"payload\":null}")]
    [InlineData("{\"payload\":{}}")]
    [InlineData("{\"payload\":{\"id\":null}}")]
    [InlineData("{\"payload\":{\"id\":\" \"}}")]
    public void SessionMeta_InvalidRequiredFieldsFail(string fragment)
    {
        var root = JsonNode.Parse(fragment)!.AsObject();
        root["type"] = "session_meta"; root["timestamp"] = "2026-01-02T03:04:05Z";
        new JsonlEventParser(NullLogger<JsonlEventParser>.Instance).TryParseLine(root.ToJsonString(), out var evt, out var error).Should().BeFalse();
        evt.Should().BeNull(); error.Should().NotBeNull();
    }

    [Fact]
    public void SessionMeta_NumericIdAndNullOptionalFields()
    {
        var evt = Parse("session_meta", """{"id":123,"forked_from_id":false,"base_instructions":null,"dynamic_tools":null,"git":null}""").Should().BeOfType<SessionMetaEvent>().Subject;
        evt.SessionId.Value.Should().Be("123"); evt.ForkedFromSessionId.Should().NotBeNull();
        evt.BaseInstructions.Should().BeNull(); evt.DynamicTools.Should().BeNull(); evt.Git.Should().BeNull();
    }

    private const string Context = """{"approval_policy":"never","sandbox_policy_type":"read-only","cwd":"/repo","model":"gpt-5","summary":"auto"}""";

    [Theory]
    [InlineData("true", true, "enabled")]
    [InlineData("false", false, "restricted")]
    [InlineData("\"ENABLED\"", true, "ENABLED")]
    [InlineData("\"restricted\"", false, "restricted")]
    [InlineData("\"future\"", null, "future")]
    [InlineData("\" \"", null, null)]
    [InlineData("null", null, null)]
    [InlineData("{}", null, null)]
    [InlineData("{\"network_access\":false}", false, "restricted")]
    public void TurnContext_NetworkAccessUnions(string network, bool? access, string? mode)
    {
        foreach (var nested in new[] { false, true })
        {
            var payload = JsonNode.Parse(Context)!.AsObject();
            payload["sandbox_policy"] = nested ? JsonNode.Parse("{\"legacy\":{\"network_access\":" + network + "}}") : JsonNode.Parse("{\"network_access\":" + network + "}");
            var evt = Parse("turn_context", payload.ToJsonString()).Should().BeOfType<TurnContextEvent>().Subject;
            evt.NetworkAccess.Should().Be(access); evt.NetworkAccessMode.Should().Be(mode);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"TRUE\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("\"invalid\"", null)]
    [InlineData("0", null)]
    public void TurnContext_RealtimeBooleanShapes(string value, bool? expected)
    {
        var payload = JsonNode.Parse(Context)!.AsObject();
        payload["realtime_active"] = JsonNode.Parse(value);
        payload["collaboration_mode"] = null; payload["truncation_policy"] = null; payload["final_output_json_schema"] = null;
        payload["network"] = JsonNode.Parse("""{"allowed_domains":["one",false,"",null],"allowedDomains":["ignored"],"deniedDomains":["two"]}""");
        var evt = Parse("turn_context", payload.ToJsonString()).Should().BeOfType<TurnContextEvent>().Subject;
        evt.RealtimeActive.Should().Be(expected);
        evt.Network!.AllowedDomains.Should().Equal("one"); evt.Network.DeniedDomains.Should().Equal("two");
        evt.CollaborationMode.Should().BeNull(); evt.TruncationPolicy.Should().BeNull(); evt.FinalOutputJsonSchema.Should().BeNull();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("{\"allowed_domains\":false,\"denied_domains\":null}")]
    public void TurnContext_InvalidNetworkIsTolerated(string network)
    {
        var payload = JsonNode.Parse(Context)!.AsObject(); payload["network"] = JsonNode.Parse(network);
        var evt = Parse("turn_context", payload.ToJsonString()).Should().BeOfType<TurnContextEvent>().Subject;
        evt.Network?.AllowedDomains.Should().BeNull(); evt.Network?.DeniedDomains.Should().BeNull();
    }

    [Theory]
    [InlineData("approval_policy")]
    [InlineData("sandbox_policy_type")]
    [InlineData("cwd")]
    [InlineData("model")]
    [InlineData("summary")]
    public void TurnContext_EachRequiredFieldIsValidated(string field)
    {
        var payload = JsonNode.Parse(Context)!.AsObject(); payload.Remove(field);
        var root = new JsonObject { ["timestamp"] = "2026-01-02T03:04:05Z", ["type"] = "turn_context", ["payload"] = payload };
        new JsonlEventParser(NullLogger<JsonlEventParser>.Instance).TryParseLine(root.ToJsonString(), out var evt, out _).Should().BeFalse();
        evt.Should().BeNull();
    }
}
