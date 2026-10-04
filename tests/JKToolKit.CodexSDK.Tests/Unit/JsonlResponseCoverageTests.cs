using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using static JKToolKit.CodexSDK.Tests.Unit.JsonlParserCoverageTests;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonlResponseCoverageTests
{
    private static T Response<T>(string body) where T : ResponseItemPayload => Parse("response_item", body).Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<T>().Subject;

    [Theory]
    [InlineData("null", "unknown")]
    [InlineData("false", "unknown")]
    [InlineData("{}", "unknown")]
    [InlineData("[]", "batch")]
    [InlineData("{\"type\":\"future\",\"new\":1}", "future")]
    public void UnknownResponse_RetainsRawPayload(string body, string kind)
    {
        var unknown = Response<UnknownResponseItemPayload>(body);
        unknown.PayloadType.Should().Be(kind); unknown.Raw.GetRawText().Should().Be(body);
    }

    [Fact]
    public void MissingResponsePayload_PreservesRoot()
    {
        const string json = """{"type":"response_item","timestamp":"2026-01-02T03:04:05Z"}""";
        new JsonlEventParser(NullLogger<JsonlEventParser>.Instance).TryParseLine(json, out var evt, out _).Should().BeTrue();
        evt.Should().BeOfType<ResponseItemEvent>().Which.Payload.Should().BeOfType<UnknownResponseItemPayload>().Which.Raw.GetRawText().Should().Be(json);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    [InlineData("\"no\"", null)]
    [InlineData("null", null)]
    public void Message_EndTurnAndContentVariants(string end, bool? expected)
    {
        var message = Response<MessageResponseItemPayload>("{\"type\":\"MESSAGE\",\"end_turn\":" + end + ",\"content\":[null,{}, {\"type\":\"output_text\",\"text\":\"out\"},{\"type\":\"output_text\"},{\"type\":\"input_text\",\"text\":\"in\"},{\"type\":\"input_text\"},{\"type\":\"input_image\",\"image_url\":\"url\"},{\"type\":\"input_image\"},{\"type\":\"future\",\"value\":7}]}");
        message.EndTurn.Should().Be(expected); message.Content.Should().HaveCount(7);
        message.Content[0].Should().BeOfType<ResponseMessageOutputTextPart>().Which.Text.Should().Be("out");
        message.Content[1].Should().BeOfType<ResponseMessageOutputTextPart>().Which.Text.Should().BeEmpty();
        message.Content[2].Should().BeOfType<ResponseMessageInputTextPart>().Which.Text.Should().Be("in");
        message.Content[3].Should().BeOfType<ResponseMessageInputTextPart>().Which.Text.Should().BeEmpty();
        message.Content[4].Should().BeOfType<ResponseMessageInputImagePart>().Which.ImageUrl.Should().Be("url");
        message.Content[5].Should().BeOfType<ResponseMessageInputImagePart>().Which.ImageUrl.Should().BeEmpty();
        message.Content[6].Should().BeOfType<UnknownResponseMessageContentPart>().Which.Raw.GetProperty("value").GetInt32().Should().Be(7);
    }

    [Fact]
    public void ReasoningContent_RecognizesTextAliasesAndPreservesFutureParts()
    {
        var payload = Response<ReasoningResponseItemPayload>("""{"type":"reasoning","summary":[null,{}, {"text":" "},{"text":"summary"}],"content":[null,{}, {"type":"reasoning_text","text":"reason"},{"type":"text"},{"type":"future","v":2}]}""");
        payload.SummaryTexts.Should().Equal("summary"); payload.Content.Should().HaveCount(3);
        payload.Content[0].Should().BeOfType<ReasoningTextContentPart>().Which.Text.Should().Be("reason");
        payload.Content[1].Should().BeOfType<ReasoningTextContentPart>().Which.Text.Should().BeEmpty();
        payload.Content[2].Should().BeOfType<UnknownReasoningContentPart>().Which.Raw.GetProperty("v").GetInt32().Should().Be(2);
        Response<ReasoningResponseItemPayload>("""{"type":"reasoning","summary":false,"content":false}""").Content.Should().BeEmpty();
        Response<MessageResponseItemPayload>("""{"type":"message","content":false}""").Content.Should().BeEmpty();
    }

    [Theory]
    [InlineData("42", 42L)]
    [InlineData("\"43\"", 43L)]
    [InlineData("1.5", null)]
    [InlineData("\"bad\"", null)]
    [InlineData("false", null)]
    public void ShellAction_TimeoutAndCommandValues(string timeout, long? expected)
    {
        var shell = Response<LocalShellCallResponseItemPayload>("{\"type\":\"local_shell_call\",\"action\":{\"type\":\"exec\",\"command\":[\"echo\",2,null],\"timeout_ms\":" + timeout + ",\"env\":{\"text\":\"value\",\"number\":2},\"working_directory\":\"/repo\",\"user\":\"test\"}}");
        shell.TimeoutMs.Should().Be(expected); shell.Command.Should().Equal("echo", "2", "null");
        shell.Env.Should().BeEquivalentTo(new Dictionary<string,string> { ["text"] = "value", ["number"] = "2" });
        shell.WorkingDirectory.Should().Be("/repo"); shell.User.Should().Be("test");
    }

    [Theory]
    [InlineData("function_call_output")]
    [InlineData("custom_tool_call_output")]
    public void ToolOutput_NormalizesImagesAndUnknownParts(string type)
    {
        var evt = Parse("response_item", "{\"type\":\"" + type + "\",\"output\":[null,{}, {\"type\":\"input_text\"},{\"type\":\"input_image\",\"image_url\":\"url\",\"detail\":\"high\"},{\"type\":\"input_image\"},{\"type\":\"future\",\"v\":3}]}").Should().BeOfType<ResponseItemEvent>().Subject;
        var parts = evt.Payload is FunctionCallOutputResponseItemPayload f ? f.OutputContent : ((CustomToolCallOutputResponseItemPayload)evt.Payload).OutputContent;
        parts.Should().HaveCount(4);
        parts![0].Should().BeOfType<FunctionToolOutputInputTextPart>().Which.Text.Should().BeEmpty();
        var image = parts[1].Should().BeOfType<FunctionToolOutputInputImagePart>().Subject;
        image.ImageUrl.Should().Be("url"); image.Detail.Should().Be("high");
        parts[2].Should().BeOfType<FunctionToolOutputInputImagePart>().Which.ImageUrl.Should().BeEmpty();
        parts[3].Should().BeOfType<UnknownFunctionToolOutputContentPart>().Which.Raw.GetProperty("v").GetInt32().Should().Be(3);
    }

    [Theory]
    [InlineData("function_call")]
    [InlineData("function_call_output")]
    [InlineData("custom_tool_call")]
    [InlineData("custom_tool_call_output")]
    [InlineData("tool_search_call")]
    [InlineData("tool_search_output")]
    [InlineData("local_shell_call")]
    [InlineData("image_generation_call")]
    [InlineData("web_search_call")]
    [InlineData("ghost_snapshot")]
    [InlineData("compaction")]
    [InlineData("compaction_summary")]
    public void MinimalResponseVariants_PreserveDiscriminator(string type)
    {
        var evt = Parse("response_item", "{\"type\":\"" + type + "\"}").Should().BeOfType<ResponseItemEvent>().Subject;
        evt.PayloadType.Should().Be(type); evt.Payload.PayloadType.Should().Be(type); evt.Payload.Should().NotBeOfType<UnknownResponseItemPayload>();
    }

    [Fact]
    public void GhostSnapshot_FiltersFileAndDirectoryLists()
    {
        var ghost = Response<GhostSnapshotResponseItemPayload>("""{"type":"ghost_snapshot","ghost_commit":{"id":"id","parent":"parent","preexisting_untracked_files":["file",false,"",null],"preexisting_untracked_dirs":["dir",false," ",null]}}""");
        ghost.GhostCommit!.Id.Should().Be("id"); ghost.GhostCommit.Parent.Should().Be("parent");
        ghost.GhostCommit.PreexistingUntrackedFiles.Should().Equal("file"); ghost.GhostCommit.PreexistingUntrackedDirs.Should().Equal("dir");
        Response<GhostSnapshotResponseItemPayload>("""{"type":"ghost_snapshot","ghost_commit":{"preexisting_untracked_files":false,"preexisting_untracked_dirs":false}}""").GhostCommit!.PreexistingUntrackedFiles.Should().BeNull();
    }

    [Fact]
    public void CompactedHistory_PreservesUnknownEntriesAndSkipsNonObjects()
    {
        var evt = Parse("compacted", """{"message":false,"replacement_history":[null,{}, {"type":"message","content":[]}]}""").Should().BeOfType<CompactedEvent>().Subject;
        evt.Message.Should().BeEmpty(); evt.ReplacementHistory.Should().HaveCount(2);
        evt.ReplacementHistory[0].Should().BeOfType<UnknownResponseItemPayload>().Which.PayloadType.Should().Be("unknown");
        evt.ReplacementHistory[1].Should().BeOfType<MessageResponseItemPayload>();
    }
}
