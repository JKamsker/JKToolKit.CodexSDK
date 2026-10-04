using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ResponseItems;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexResponseItemParserContractTests
{
    // Parse inside a short-lived document: every retained payload must remain usable afterwards.
    private static CodexResponseItem Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CodexResponseItemParser.Parse(document.RootElement);
    }

    [Theory]
    [InlineData("null", "")]
    [InlineData("[]", "")]
    [InlineData("42", "")]
    [InlineData("{}", "")]
    [InlineData("{\"type\":42}", "")]
    [InlineData("{\"type\":\"future\",\"newField\":true}", "future")]
    public void UnknownPayload_PreservesDiscriminatorAndRawJson(string json, string type)
    {
        var item = Parse(json).Should().BeOfType<CodexResponseItemUnknown>().Subject;
        item.Type.Should().Be(type);
        item.Raw.GetRawText().Should().Be(json);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    [InlineData("\"true\"", null)]
    public void Message_MapsContentAndNullableEndTurn(string endTurn, bool? expected)
    {
        var item = Parse("""
            {"type":"message","role":"assistant","phase":"final_answer","content":[
              {"type":"input_text","text":"question"},{"type":"input_image","image_url":"data:image/png;base64,AA=="},
              {"type":"output_text","text":"answer"},null,42,{}, {"type":"future"},
              {"type":"input_text"},{"type":"input_image","image_url":false},{"type":"output_text","text":null}],
            "end_turn":END}
            """.Replace("END", endTurn)).Should().BeOfType<CodexResponseItemMessage>().Subject;
        item.Type.Should().Be("message");
        item.Role.Should().Be("assistant");
        item.Phase.Should().Be("final_answer");
        item.EndTurn.Should().Be(expected);
        item.Content.Should().HaveCount(6);
        item.Content[0].Should().BeOfType<CodexContentInputText>().Which.Text.Should().Be("question");
        item.Content[1].Should().BeOfType<CodexContentInputImage>().Which.ImageUrl.Should().Be("data:image/png;base64,AA==");
        item.Content[2].Should().BeOfType<CodexContentOutputText>().Which.Text.Should().Be("answer");
        item.Content[3].Should().BeOfType<CodexContentInputText>().Which.Text.Should().BeEmpty();
        item.Content[4].Should().BeOfType<CodexContentInputImage>().Which.ImageUrl.Should().BeEmpty();
        item.Content[5].Should().BeOfType<CodexContentOutputText>().Which.Text.Should().BeEmpty();
        item.Content.Select(c => c.Type).Should().Equal("input_text", "input_image", "output_text", "input_text", "input_image", "output_text");
        item.Content[0].Raw.GetProperty("text").GetString().Should().Be("question");
        item.Raw.GetProperty("role").GetString().Should().Be("assistant");
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"content\":null")]
    [InlineData(",\"content\":{}")]
    [InlineData(",\"content\":[]")]
    public void Message_MissingOrMalformedOptionalFieldsHaveStableDefaults(string fields)
    {
        var item = (CodexResponseItemMessage)Parse("{\"type\":\"message\"" + fields + "}");
        item.Role.Should().BeEmpty();
        item.Content.Should().BeEmpty();
        item.EndTurn.Should().BeNull();
        item.Phase.Should().BeNull();
    }

    [Fact]
    public void Reasoning_MapsSummaryAndFiltersNonStringContent()
    {
        var item = (CodexResponseItemReasoning)Parse("""
            {"type":"reasoning","summary":[null,7,{"type":"summary_text","text":"thinking"},{}],
             "content":["first",null,{},2,"second"],"encrypted_content":"cipher"}
            """);
        item.Summary.Should().Equal(new CodexReasoningSummaryPart("summary_text", "thinking"), new CodexReasoningSummaryPart("", ""));
        item.Content.Should().Equal("first", "second");
        item.EncryptedContent.Should().Be("cipher");
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"summary\":false,\"content\":{}")]
    [InlineData(",\"summary\":[],\"content\":[]")]
    public void Reasoning_AbsentOrInvalidListsUseEmptySummaryAndNullContent(string fields)
    {
        var item = (CodexResponseItemReasoning)Parse("{\"type\":\"reasoning\"" + fields + "}");
        item.Summary.Should().BeEmpty();
        item.Content.Should().BeNull();
        item.EncryptedContent.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FunctionCall_MapsDistinctFieldsAndDefaults(bool populated)
    {
        var item = (CodexResponseItemFunctionCall)Parse(populated
            ? """{"type":"function_call","name":"lookup","namespace":"tools","arguments":"{\"q\":1}","call_id":"c1"}"""
            : """{"type":"function_call"}""");
        item.Name.Should().Be(populated ? "lookup" : "");
        item.Namespace.Should().Be(populated ? "tools" : null);
        item.Arguments.Should().Be(populated ? "{\"q\":1}" : "");
        item.CallId.Should().Be(populated ? "c1" : "");
    }

    [Theory]
    [InlineData("{\"nested\":[1]}")]
    [InlineData("\"text\"")]
    [InlineData("null")]
    public void FunctionOutput_PreservesArbitraryOutput(string output)
    {
        var item = (CodexResponseItemFunctionCallOutput)Parse("{\"type\":\"function_call_output\",\"call_id\":\"call\",\"output\":" + output + "}");
        item.CallId.Should().Be("call");
        item.Output.GetRawText().Should().Be(output);
    }

    [Fact]
    public void FunctionOutput_MissingOutputIsUndefined()
    {
        var item = (CodexResponseItemFunctionCallOutput)Parse("""{"type":"function_call_output"}""");
        item.CallId.Should().BeEmpty();
        item.Output.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"tools\":null")]
    [InlineData(",\"tools\":{}")]
    public void ToolSearch_MissingOrMalformedToolsUseEmptyList(string fields)
    {
        var item = (CodexResponseItemToolSearchOutput)Parse("{\"type\":\"tool_search_output\"" + fields + "}");
        item.CallId.Should().BeNull();
        item.Status.Should().BeEmpty();
        item.Execution.Should().BeEmpty();
        item.Tools.Should().BeEmpty();
    }

    [Fact]
    public void ToolSearch_ClonesEveryToolIncludingForwardCompatiblePayloads()
    {
        var item = (CodexResponseItemToolSearchOutput)Parse("""{"type":"tool_search_output","call_id":"c","status":"completed","execution":"server","tools":[{"name":"tool"},null,"future"]}""");
        item.CallId.Should().Be("c");
        item.Status.Should().Be("completed");
        item.Execution.Should().Be("server");
        item.Tools.Select(t => t.GetRawText()).Should().Equal("{\"name\":\"tool\"}", "null", "\"future\"");
    }

    [Fact]
    public void WebSearch_MapsEveryActionField()
    {
        var item = (CodexResponseItemWebSearchCall)Parse("""{"type":"web_search_call","status":"completed","action":{"type":"search","query":"single","queries":["one",false,"two"],"url":"https://example.test","pattern":"needle"}}""");
        item.Status.Should().Be("completed");
        item.Action.Should().BeEquivalentTo(new CodexWebSearchAction("search", "single", ["one", "two"], "https://example.test", "needle"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"action\":null")]
    [InlineData(",\"action\":[]")]
    public void WebSearch_MissingOrMalformedActionIsNull(string fields)
    {
        var item = (CodexResponseItemWebSearchCall)Parse("{\"type\":\"web_search_call\"" + fields + "}");
        item.Status.Should().BeNull();
        item.Action.Should().BeNull();
    }

    [Fact]
    public void WebSearch_EmptyActionRetainsUnknownType()
    {
        var item = (CodexResponseItemWebSearchCall)Parse("""{"type":"web_search_call","action":{}}""");
        item.Action.Should().Be(new CodexWebSearchAction("", null, null, null, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ImageGeneration_MapsResultAndPrompt(bool populated)
    {
        var item = (CodexResponseItemImageGenerationCall)Parse(populated
            ? """{"type":"image_generation_call","id":"image-1","status":"done","result":"image-data","revised_prompt":"a cat"}"""
            : """{"type":"image_generation_call"}""");
        item.Id.Should().Be(populated ? "image-1" : "");
        item.Status.Should().Be(populated ? "done" : "");
        item.Result.Should().Be(populated ? "image-data" : "");
        item.RevisedPrompt.Should().Be(populated ? "a cat" : null);
    }

    [Fact]
    public void GhostSnapshot_ClonesCommitAndSupportsMissingCommit()
    {
        ((CodexResponseItemGhostSnapshot)Parse("""{"type":"ghost_snapshot","ghost_commit":{"id":"abc"}}"""))
            .GhostCommit.GetProperty("id").GetString().Should().Be("abc");
        ((CodexResponseItemGhostSnapshot)Parse("""{"type":"ghost_snapshot"}"""))
            .GhostCommit.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Theory]
    [InlineData("{\"type\":\"compaction\",\"encrypted_content\":\"secret\"}", "secret")]
    [InlineData("{\"type\":\"compaction\"}", "")]
    public void Compaction_MapsEncryptedContent(string json, string expected) =>
        ((CodexResponseItemCompaction)Parse(json)).EncryptedContent.Should().Be(expected);
}
