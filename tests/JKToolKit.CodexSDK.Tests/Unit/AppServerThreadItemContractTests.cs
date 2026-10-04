using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ThreadRead;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerThreadItemContractTests
{
    private static CodexThreadItem Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CodexThreadItemParser.Parse(document.RootElement);
    }

    public static IEnumerable<object[]> StrictPayloads()
    {
        yield return ["""{"id":"i","type":"userMessage","content":[]}""", "content", ""];
        yield return ["""{"id":"i","type":"hookPrompt","fragments":[]}""", "fragments", ""];
        yield return ["""{"id":"i","type":"agentMessage","text":"answer"}""", "text", "phase"];
        yield return ["""{"id":"i","type":"plan","text":"plan"}""", "text", ""];
        yield return ["""{"id":"i","type":"reasoning"}""", "", "summary,content,encrypted_content"];
        yield return ["""{"id":"i","type":"commandExecution","command":"pwd","cwd":"/repo","status":"completed"}""", "command,cwd,status", "processId,pluginId,scriptPath,source,aggregatedOutput,exitCode,durationMs,commandActions"];
        yield return ["""{"id":"i","type":"fileChange","status":"completed","changes":[]}""", "status,changes", ""];
        yield return ["""{"id":"i","type":"mcpToolCall","server":"s","tool":"t","status":"completed","arguments":{}}""", "server,tool,status", "durationMs"];
        yield return ["""{"id":"i","type":"dynamicToolCall","tool":"t","status":"done","arguments":{}}""", "tool,status", "contentItems,success,durationMs"];
        yield return ["""{"id":"i","type":"functionCallOutput","name":"tool","output":"result"}""", "name", "namespace"];
        yield return ["""{"id":"i","type":"collabAgentToolCall","tool":"spawn","status":"done","senderThreadId":"s","receiverThreadIds":[],"agentsStates":{}}""", "tool,status,senderThreadId,receiverThreadIds,agentsStates", "prompt,model,reasoningEffort"];
        yield return ["""{"id":"i","type":"webSearch","query":"q"}""", "query", "action"];
        yield return ["""{"id":"i","type":"imageView","path":"/image.png"}""", "path", ""];
        yield return ["""{"id":"i","type":"imageGeneration","status":"done","result":"base64"}""", "status,result", "revisedPrompt,savedPath"];
        yield return ["""{"id":"i","type":"enteredReviewMode","review":"review"}""", "review", ""];
        yield return ["""{"id":"i","type":"exitedReviewMode","review":"review"}""", "review", ""];
    }

    [Theory]
    [MemberData(nameof(StrictPayloads))]
    public void StrictItems_RejectEveryMissingRequiredOrWrongTypedField_AndRetainRawPayload(string json, string requiredFields, string optionalFields)
    {
        Parse(json).Should().NotBeOfType<CodexThreadItemUnknown>();
        foreach (var field in requiredFields.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var missing = JsonNode.Parse(json)!.AsObject(); missing.Remove(field);
            AssertUnknown(missing.ToJsonString());
        }
        foreach (var field in (requiredFields + "," + optionalFields).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var invalid = JsonNode.Parse(json)!.AsObject(); invalid[field] = "wrong type";
            // String fields need a non-string invalid value; array/object/number/bool fields need a string.
            if (!new[] { "content", "fragments", "summary", "exitCode", "durationMs", "commandActions", "changes", "contentItems", "success", "receiverThreadIds", "agentsStates", "action" }.Contains(field))
                invalid[field] = new JsonObject();
            AssertUnknown(invalid.ToJsonString());
        }
    }

    private static void AssertUnknown(string json)
    {
        var item = Parse(json).Should().BeOfType<CodexThreadItemUnknown>().Subject;
        item.Id.Should().Be("i"); item.Raw.GetRawText().Should().Be(json);
        item.Type.Should().Be(JsonNode.Parse(json)!["type"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("[]")]
    public void NonObjectItemsRemainUnknownWithRawPayload(string json)
    {
        var item = Parse(json).Should().BeOfType<CodexThreadItemUnknown>().Subject;
        item.Id.Should().BeEmpty(); item.Type.Should().BeEmpty(); item.Raw.GetRawText().Should().Be(json);
    }

    [Theory]
    [InlineData("inProgress", CodexCommandExecutionStatus.InProgress, CodexPatchApplyStatus.InProgress, CodexMcpToolCallStatus.InProgress)]
    [InlineData("in_progress", CodexCommandExecutionStatus.InProgress, CodexPatchApplyStatus.InProgress, CodexMcpToolCallStatus.InProgress)]
    [InlineData("completed", CodexCommandExecutionStatus.Completed, CodexPatchApplyStatus.Completed, CodexMcpToolCallStatus.Completed)]
    [InlineData("failed", CodexCommandExecutionStatus.Failed, CodexPatchApplyStatus.Failed, CodexMcpToolCallStatus.Failed)]
    [InlineData("declined", CodexCommandExecutionStatus.Declined, CodexPatchApplyStatus.Declined, CodexMcpToolCallStatus.Unknown)]
    [InlineData("future", CodexCommandExecutionStatus.Unknown, CodexPatchApplyStatus.Unknown, CodexMcpToolCallStatus.Unknown)]
    [InlineData(null, CodexCommandExecutionStatus.Unknown, CodexPatchApplyStatus.Unknown, CodexMcpToolCallStatus.Unknown)]
    public void ExecutionStatuses_KeepAliasesAndUnknownValues(string? status, CodexCommandExecutionStatus command, CodexPatchApplyStatus patch, CodexMcpToolCallStatus mcp)
    {
        CodexCommandExecutionStatusExtensions.Parse(status).Should().Be(command);
        CodexPatchApplyStatusExtensions.Parse(status).Should().Be(patch);
        CodexMcpToolCallStatusExtensions.Parse(status).Should().Be(mcp);
    }

    [Theory]
    [InlineData(null, CodexCommandExecutionSource.Agent)]
    [InlineData("agent", CodexCommandExecutionSource.Agent)]
    [InlineData("userShell", CodexCommandExecutionSource.UserShell)]
    [InlineData("unifiedExecStartup", CodexCommandExecutionSource.UnifiedExecStartup)]
    [InlineData("unifiedExecInteraction", CodexCommandExecutionSource.UnifiedExecInteraction)]
    [InlineData("future", CodexCommandExecutionSource.Unknown)]
    public void CommandSource_MapsEachWireValue(string? source, CodexCommandExecutionSource expected) =>
        CodexCommandExecutionSourceExtensions.Parse(source).Should().Be(expected);

    [Fact]
    public void CommandExecution_PreservesEveryActionAndExecutionDetail()
    {
        var item = (CodexThreadItemCommandExecution)Parse("""
            {"id":"i","type":"commandExecution","command":"cat a","cwd":"/repo","status":"completed","source":"userShell",
             "processId":"p","pluginId":"plug","scriptPath":"scripts/a.sh","aggregatedOutput":"contents","exitCode":-2,"durationMs":4294967296,
             "commandActions":[{"command":"cat a","type":"read","name":"a","path":"/repo/a","query":"needle"},{"type":"future"}]}
            """);
        item.Command.Should().Be("cat a"); item.WorkingDirectory.Should().Be("/repo"); item.ProcessId.Should().Be("p");
        item.PluginId.Should().Be("plug"); item.ScriptPath.Should().Be("scripts/a.sh"); item.AggregatedOutput.Should().Be("contents");
        item.ExitCode.Should().Be(-2); item.DurationMs.Should().Be(4294967296); item.Source.Should().Be(CodexCommandExecutionSource.UserShell);
        item.Status.Should().Be(CodexCommandExecutionStatus.Completed);
        item.CommandActions.Should().Equal(new CodexCommandAction("cat a", "read", "a", "/repo/a", "needle"), new CodexCommandAction("", "future", null, null, null));
    }

    [Theory]
    [InlineData("\"add\"", CodexPatchChangeKindType.Add, null)]
    [InlineData("\"delete\"", CodexPatchChangeKindType.Delete, null)]
    [InlineData("\"update\"", CodexPatchChangeKindType.Update, null)]
    [InlineData("{\"type\":\"add\"}", CodexPatchChangeKindType.Add, null)]
    [InlineData("{\"type\":\"delete\"}", CodexPatchChangeKindType.Delete, null)]
    [InlineData("{\"type\":\"update\",\"movePath\":\"new.txt\"}", CodexPatchChangeKindType.Update, "new.txt")]
    public void FileChange_ParsesStringAndObjectUnionCases(string kind, CodexPatchChangeKindType expected, string? move)
    {
        var item = (CodexThreadItemFileChange)Parse("{\"id\":\"i\",\"type\":\"fileChange\",\"status\":\"completed\",\"changes\":[{\"path\":\"a.txt\",\"diff\":\"+line\",\"kind\":" + kind + "}]}");
        item.Changes.Should().Equal(new CodexFileUpdateChange("a.txt", new CodexPatchChangeKind(expected, move), "+line"));
    }

    [Theory]
    [InlineData("{\"path\":\"a\",\"diff\":\"d\"}")]
    [InlineData("{\"path\":\"a\",\"diff\":\"d\",\"kind\":\"future\"}")]
    [InlineData("{\"path\":\"a\",\"diff\":\"d\",\"kind\":{\"type\":\"future\"}}")]
    [InlineData("{\"path\":\"a\",\"diff\":\"d\",\"kind\":{\"type\":\"update\",\"movePath\":false}}")]
    [InlineData("{\"path\":\"a\",\"diff\":\"d\",\"kind\":null}")]
    [InlineData("{\"path\":\"a\"}")]
    [InlineData("{}")]
    public void FileChange_InvalidNestedChangeFallsBackToUnknown(string change) =>
        AssertUnknown("{\"id\":\"i\",\"type\":\"fileChange\",\"status\":\"completed\",\"changes\":[" + change + "]}");

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"text\":\"prompt\",\"hookRunId\":\"run\"}]")]
    public void HookPrompt_ParsesFragments(string fragments)
    {
        var item = (CodexThreadItemHookPrompt)Parse("{\"id\":\"i\",\"type\":\"hookPrompt\",\"fragments\":" + fragments + "}");
        if (fragments == "[]") item.Fragments.Should().BeEmpty();
        else item.Fragments.Should().Equal(new CodexHookPromptFragment("prompt", "run"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"text\":\"prompt\"}")]
    public void HookPrompt_RejectsIncompleteFragments(string fragment) =>
        AssertUnknown("{\"id\":\"i\",\"type\":\"hookPrompt\",\"fragments\":[" + fragment + "]}");

    [Theory]
    [InlineData("", null)]
    [InlineData(",\"error\":false", null)]
    [InlineData(",\"error\":\"failure\"", "failure")]
    [InlineData(",\"error\":{\"message\":\"details\"}", "details")]
    public void McpCall_ParsesStringAndObjectErrors(string error, string? expected)
    {
        var item = (CodexThreadItemMcpToolCall)Parse("{\"id\":\"i\",\"type\":\"mcpToolCall\",\"server\":\"s\",\"tool\":\"t\",\"status\":\"failed\",\"arguments\":{}" + error + "}");
        item.Server.Should().Be("s"); item.Tool.Should().Be("t"); item.ErrorMessage.Should().Be(expected);
        item.Result.Should().BeNull(); item.DurationMs.Should().BeNull(); item.Arguments.GetRawText().Should().Be("{}");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    public void DynamicToolCall_PreservesNullableSuccess(string success, bool? expected)
    {
        var item = (CodexThreadItemDynamicToolCall)Parse("{\"id\":\"i\",\"type\":\"dynamicToolCall\",\"tool\":\"t\",\"status\":\"done\",\"arguments\":[1],\"contentItems\":[{\"text\":\"result\"}],\"success\":" + success + ",\"durationMs\":123}");
        item.Tool.Should().Be("t"); item.Status.Should().Be("done"); item.Success.Should().Be(expected); item.DurationMs.Should().Be(123);
        item.Arguments.GetRawText().Should().Be("[1]"); item.ContentItems.Should().ContainSingle().Which.GetProperty("text").GetString().Should().Be("result");
    }

    [Fact]
    public void FunctionOutput_PreservesOutputAndNamespace()
    {
        var item = (CodexThreadItemFunctionCallOutput)Parse("""{"id":"i","type":"functionCallOutput","name":"tool","namespace":"ns","output":{"value":1}}""");
        item.Name.Should().Be("tool"); item.Namespace.Should().Be("ns"); item.Output.GetProperty("value").GetInt32().Should().Be(1);
    }

    [Fact]
    public void ExplicitNullOptionalFields_HaveTheSameMeaningAsAbsentFields()
    {
        var command = (CodexThreadItemCommandExecution)Parse("""{"type":"commandExecution","command":"pwd","cwd":"/repo","status":"completed","processId":null,"pluginId":null,"scriptPath":null,"source":null,"aggregatedOutput":null,"exitCode":null,"durationMs":null,"commandActions":null}""");
        command.ProcessId.Should().BeNull(); command.PluginId.Should().BeNull(); command.ScriptPath.Should().BeNull();
        command.AggregatedOutput.Should().BeNull(); command.ExitCode.Should().BeNull(); command.DurationMs.Should().BeNull();
        command.CommandActions.Should().BeEmpty(); command.Source.Should().Be(CodexCommandExecutionSource.Agent);
        var reasoning = (CodexThreadItemReasoning)Parse("""{"type":"reasoning","summary":null,"content":null,"encrypted_content":null}""");
        reasoning.Summary.Should().BeEmpty(); reasoning.Content.Should().BeNull(); reasoning.EncryptedContent.Should().BeNull();
        var web = (CodexThreadItemWebSearch)Parse("""{"type":"webSearch","query":"q","action":null}""");
        web.Action.Should().BeNull();
        var dynamic = (CodexThreadItemDynamicToolCall)Parse("""{"type":"dynamicToolCall","tool":"t","status":"done","arguments":{},"contentItems":null,"durationMs":null,"success":null}""");
        dynamic.ContentItems.Should().BeNull(); dynamic.DurationMs.Should().BeNull(); dynamic.Success.Should().BeNull();
    }

    [Theory]
    [InlineData("mcpToolCall", "arguments", "\"server\":\"s\",\"tool\":\"t\",\"status\":\"done\"")]
    [InlineData("dynamicToolCall", "arguments", "\"tool\":\"t\",\"status\":\"done\"")]
    [InlineData("functionCallOutput", "output", "\"name\":\"t\"")]
    public void RequiredOpaquePayload_RejectsMissingAndNullValues(string type, string field, string otherFields)
    {
        var prefix = "{\"id\":\"i\",\"type\":\"" + type + "\"," + otherFields;
        AssertUnknown(prefix + "}");
        AssertUnknown(prefix + ",\"" + field + "\":null}");
    }

    [Fact]
    public void RichHistoryFixture_ExposesInteractionAndActionDetails()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "thread-read-rich-items-response.json")));
        var items = CodexThreadItemParser.ParseItems(document.RootElement.GetProperty("thread").GetProperty("turns")[0]);
        var user = (CodexThreadItemUserMessage)items[0];
        user.ContentItems.Should().ContainSingle().Which.GetProperty("text").GetString().Should().Be("hello");
        user.ClientId.Should().Be("client_msg_1");
        var hook = (CodexThreadItemHookPrompt)items[1];
        hook.Fragments[0].Text.Should().Be("run tests"); hook.Fragments[0].HookRunId.Should().Be("hook-1");
        ((CodexThreadItemPlan)items[2]).Text.Should().Be("Plan text");
        var reasoning = (CodexThreadItemReasoning)items[3];
        reasoning.Summary.Should().Equal("Step 1"); reasoning.Content.Should().Equal("raw reasoning"); reasoning.EncryptedContent.Should().BeNull();
        var message = (CodexThreadItemAgentMessage)items[4];
        message.Text.Should().Be("done"); message.Phase.Should().Be("analysis"); message.MemoryCitation!.Value.GetProperty("kind").GetString().Should().Be("memory");
        ((CodexThreadItemFileChange)items[6]).Status.Should().Be(CodexPatchApplyStatus.Completed);
        ((CodexThreadItemMcpToolCall)items[7]).Status.Should().Be(CodexMcpToolCallStatus.Completed);
        var collab = (CodexThreadItemCollabAgentToolCall)items[9];
        collab.Tool.Should().Be("spawnAgent"); collab.Status.Should().Be("completed"); collab.SenderThreadId.Should().Be("t_parent");
        collab.ReceiverThreadIds.Should().Equal("t_child"); collab.Prompt.Should().Be("review it"); collab.Model.Should().Be("gpt-5");
        collab.ReasoningEffort!.Value.ToString().Should().Be("high");
        collab.AgentsStates["child"].Status.Should().Be("completed"); collab.AgentsStates["child"].Message.Should().Be("done");
        ((CodexThreadItemWebSearch)items[10]).Query.Should().Be("codex cli");
        ((CodexThreadItemImageView)items[11]).Path.Should().Be(@"C:\img.png");
        var image = (CodexThreadItemImageGeneration)items[12];
        image.Status.Should().Be("completed"); image.Result.Should().Be("ok"); image.RevisedPrompt.Should().Be("improved"); image.SavedPath.Should().Be(@"C:\out.png");
        ((CodexThreadItemEnteredReviewMode)items[13]).Review.Should().Be("rev-1");
        ((CodexThreadItemExitedReviewMode)items[14]).Review.Should().Be("rev-1");
    }

    [Theory]
    [InlineData("search", CodexWebSearchActionKind.Search)]
    [InlineData("openPage", CodexWebSearchActionKind.OpenPage)]
    [InlineData("findInPage", CodexWebSearchActionKind.FindInPage)]
    [InlineData("other", CodexWebSearchActionKind.Other)]
    public void WebAction_MapsEachUnionCase(string type, CodexWebSearchActionKind expected)
    {
        var json = JsonSerializer.Serialize(new { id = "i", type = "webSearch", query = "outer", action = new { type, query = "inner", queries = new[] { "one", "two" }, url = "https://example.test", pattern = "needle" } });
        var action = ((CodexThreadItemWebSearch)Parse(json)).Action!;
        action.Kind.Should().Be(expected);
        action.Query.Should().Be(expected == CodexWebSearchActionKind.Search ? "inner" : null);
        if (expected == CodexWebSearchActionKind.Search) action.Queries.Should().Equal("one", "two");
        else action.Queries.Should().BeNull();
        action.Url.Should().Be(expected is CodexWebSearchActionKind.OpenPage or CodexWebSearchActionKind.FindInPage ? "https://example.test" : null);
        action.Pattern.Should().Be(expected == CodexWebSearchActionKind.FindInPage ? "needle" : null);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"type\":\"future\"}")]
    [InlineData("{\"type\":\"search\",\"query\":false}")]
    [InlineData("{\"type\":\"search\",\"queries\":[false]}")]
    [InlineData("{\"type\":\"search\",\"url\":false}")]
    [InlineData("{\"type\":\"search\",\"pattern\":false}")]
    public void WebAction_InvalidNestedPayloadRemainsUnknown(string action) =>
        AssertUnknown("{\"id\":\"i\",\"type\":\"webSearch\",\"query\":\"q\",\"action\":" + action + "}");

    [Theory]
    [InlineData("[false]", "{}")]
    [InlineData("[\" \"]", "{}")]
    [InlineData("[]", "{\"child\":{}}")]
    [InlineData("[]", "{\"child\":{\"status\":\"done\",\"message\":false}}")]
    public void Collaboration_InvalidReceiverOrStateRemainsUnknown(string receivers, string states) =>
        AssertUnknown("{\"id\":\"i\",\"type\":\"collabAgentToolCall\",\"tool\":\"spawn\",\"status\":\"done\",\"senderThreadId\":\"s\",\"receiverThreadIds\":" + receivers + ",\"agentsStates\":" + states + "}");

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("42")]
    public void TurnError_NonObjectIsAbsent(string? json) => CodexTurnError.Parse(json is null ? null : JsonSerializer.Deserialize<JsonElement>(json)).Should().BeNull();

    [Fact]
    public void TurnError_ClonesDetailsAndSuppliesDefaults()
    {
        CodexTurnError error;
        using (var document = JsonDocument.Parse("""{"message":"failed","additionalDetails":"detail","codexErrorInfo":{"httpStatusCode":429}}"""))
            error = CodexTurnError.Parse(document.RootElement)!;
        error.Message.Should().Be("failed"); error.AdditionalDetails.Should().Be("detail");
        error.CodexErrorInfo!.Value.GetProperty("httpStatusCode").GetInt32().Should().Be(429);
        error.Raw.GetProperty("message").GetString().Should().Be("failed");
        var empty = CodexTurnError.Parse(JsonSerializer.Deserialize<JsonElement>("{}"))!;
        empty.Message.Should().BeEmpty(); empty.AdditionalDetails.Should().BeNull(); empty.CodexErrorInfo.Should().BeNull();
    }
}
