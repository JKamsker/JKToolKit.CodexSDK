using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkMapperCoverageTests
{
    [Fact]
    public void SingleUserMessage_PreservesTextAndImages_AndIgnoresOtherMedia()
    {
        var message = new ChatMessage(ChatRole.User,
        [
            new TextContent("  "), new TextContent("describe"),
            new UriContent("https://example.org/image", "IMAGE/png"),
            new UriContent("file:///tmp/photo.png", "image/png"),
            new DataContent(new byte[] { 1 }, "image/jpeg"),
            new UriContent("https://example.org/doc", "text/plain"),
            new DataContent(new byte[] { 2 }, "audio/wav"), new ErrorContent("ignored")
        ]);
        var items = CodexAgentMessageMapper.ToTurnInputItems([message]).Select(x => JsonSerializer.SerializeToElement(x.Wire)).ToArray();
        items.Should().HaveCount(4);
        items[0].GetProperty("text").GetString().Should().Be("describe");
        items[1].GetProperty("url").GetString().Should().Be("https://example.org/image");
        items[2].GetProperty("path").GetString().Should().Be("/tmp/photo.png");
        items[3].GetProperty("url").GetString().Should().Be("data:image/jpeg;base64,AQ==");
    }

    [Fact]
    public void ConversationTranscript_KeepsRolesAndStructuredContentInOrder()
    {
        var toolCall = new FunctionCallContent("c1", "search", new Dictionary<string, object?> { ["query"] = "cats", ["page"] = 2 });
        var approval = new ToolApprovalResponseContent("approval", true, toolCall) { Reason = "permitted" };
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "instructions"),
            new ChatMessage(ChatRole.Assistant, new AIContent[]
            {
                new UriContent("https://example.org/file", "application/pdf"),
                new DataContent(new byte[] { 1 }, "image/png"), toolCall,
                new FunctionCallContent("c2", "ping", null), new FunctionResultContent("c1", "found"),
                approval, new ErrorContent("failure"), new CustomContent("custom"), new CustomContent(null)
            }),
            new ChatMessage(ChatRole.User, " ")
        };
        var text = JsonSerializer.SerializeToElement(CodexAgentMessageMapper.ToTurnInputItems(messages).Single().Wire).GetProperty("text").GetString();
        text.Should().Be("system: instructions" + Environment.NewLine + Environment.NewLine + "assistant: " + string.Join(Environment.NewLine,
            "[uri:application/pdf] https://example.org/file", "[data:image/png] data:image/png;base64,AQ==", "[function_call:search] query=cats, page=2", "[function_call:ping] ", "[function_result] found", "[tool_approval:True] permitted", "[error] failure", "custom"));
    }

    [Fact]
    public void EmptyMessages_DoNotProduceEmptyTurnInput()
    {
        CodexAgentMessageMapper.ToTurnInputItems([]).Should().BeEmpty();
        CodexAgentMessageMapper.ToTurnInputItems([new ChatMessage(ChatRole.User, " ")]).Should().BeEmpty();
        CodexAgentMessageMapper.ToTurnInputItems([new ChatMessage(ChatRole.Assistant, " ")]).Should().BeEmpty();
    }

    [Fact]
    public void Options_RespectDirectThenAttachedThenChatThenAgentPrecedence()
    {
        var defaults = new CodexAIAgentOptions
        { SafetyOptions = new() { DefaultSandbox = CodexSandboxMode.DangerFullAccess }, Model = "default", Cwd = "/default", ApprovalPolicy = CodexApprovalPolicy.Never, Sandbox = CodexSandboxMode.ReadOnly, Effort = CodexReasoningEffort.Low, Summary = "default" };
        var chat = new ChatOptions { ModelId = "chat", Reasoning = new() { Effort = ReasoningEffort.Medium, Output = ReasoningOutput.Summary } };
        var run = new CodexAgentRunOptions
        { Model = "direct", Cwd = "/direct", ApprovalPolicy = CodexApprovalPolicy.OnRequest, Sandbox = CodexSandboxMode.WorkspaceWrite, Effort = CodexReasoningEffort.High, Summary = "direct" };
        run.WithCodex(new CodexAgentRunConfiguration
        { Model = "attached", Cwd = "/attached", ApprovalPolicy = CodexApprovalPolicy.Untrusted, Sandbox = CodexSandboxMode.DangerFullAccess, Effort = CodexReasoningEffort.XHigh, Summary = "attached" });
        AssertOptions(defaults, run, chat, "direct", "/direct", CodexApprovalPolicy.OnRequest, CodexSandboxMode.WorkspaceWrite, CodexReasoningEffort.High, "direct");
        run.Model = run.Cwd = run.Summary = null;
        run.ApprovalPolicy = null; run.Sandbox = null; run.Effort = null;
        AssertOptions(defaults, run, chat, "attached", "/attached", CodexApprovalPolicy.Untrusted, CodexSandboxMode.DangerFullAccess, CodexReasoningEffort.XHigh, "attached");
        AssertOptions(defaults, null, chat, "chat", "/default", CodexApprovalPolicy.Never, CodexSandboxMode.ReadOnly, CodexReasoningEffort.Medium, "auto");
        AssertOptions(defaults, new AgentRunOptions(), null, "default", "/default", CodexApprovalPolicy.Never, CodexSandboxMode.ReadOnly, CodexReasoningEffort.Low, "default");
        defaults.Sandbox = null;
        defaults.SafetyOptions = new() { DefaultSandbox = CodexSandboxMode.ReadOnly };
        CodexAgentOptionsMapper.GetSandbox(defaults, null).Should().Be(CodexSandboxMode.ReadOnly);
        defaults.SafetyOptions = null!;
        CodexAgentOptionsMapper.GetSandbox(defaults, null).Should().BeNull();
    }

    [Theory]
    [InlineData(ReasoningEffort.None, "none")]
    [InlineData(ReasoningEffort.Low, "low")]
    [InlineData(ReasoningEffort.Medium, "medium")]
    [InlineData(ReasoningEffort.High, "high")]
    [InlineData(ReasoningEffort.ExtraHigh, "xhigh")]
    public void ReasoningEffort_MapsToCodex(ReasoningEffort source, string expected) =>
        CodexAgentOptionsMapper.GetRunEffort(null, new ChatOptions { Reasoning = new() { Effort = source } }).Should().Be(CodexReasoningEffort.Parse(expected));

    [Theory]
    [InlineData(ReasoningOutput.None, "none")]
    [InlineData(ReasoningOutput.Summary, "auto")]
    [InlineData(ReasoningOutput.Full, "detailed")]
    public void ReasoningOutput_MapsToCodex(ReasoningOutput source, string expected) =>
        CodexAgentOptionsMapper.GetRunSummary(null, new ChatOptions { Reasoning = new() { Output = source } }).Should().Be(expected);

    [Fact]
    public void MissingReasoning_RemainsUnspecified()
    {
        CodexAgentOptionsMapper.GetRunEffort(null, new ChatOptions { Reasoning = new() }).Should().BeNull();
        CodexAgentOptionsMapper.GetRunSummary(null, new ChatOptions { Reasoning = new() }).Should().BeNull();
    }

    [Fact]
    public void ResponseSchema_PrefersChatOptions_AndSurvivesDocumentDisposal()
    {
        using var doc = JsonDocument.Parse("{\"type\":\"object\"}");
        var run = new AgentRunOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(doc.RootElement) };
        var fromRun = CodexAgentOptionsMapper.GetOutputSchema(run, null);
        doc.Dispose();
        fromRun!.Value.GetProperty("type").GetString().Should().Be("object");
        var chat = new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema(JsonSerializer.SerializeToElement(new { type = "array" })) };
        CodexAgentOptionsMapper.GetOutputSchema(run, chat)!.Value.GetProperty("type").GetString().Should().Be("array");
        chat.ResponseFormat = ChatResponseFormat.Text;
        CodexAgentOptionsMapper.GetOutputSchema(run, chat).Should().BeNull();
        chat.ResponseFormat = ChatResponseFormat.Json;
        CodexAgentOptionsMapper.GetOutputSchema(run, chat).Should().BeNull();
        CodexAgentOptionsMapper.GetOutputSchema(null, null).Should().BeNull();
    }

    [Fact]
    public void ConfigureTurn_AppliesAttachedCallbackBeforeDirectCallback()
    {
        var run = new CodexAgentRunOptions { ConfigureTurn = turn => turn.Model += "-direct" };
        run.WithCodex(new() { ConfigureTurn = turn => turn.Model = "attached" });
        var turn = new TurnStartOptions();
        CodexAgentOptionsMapper.ConfigureRunTurn(turn, run);
        turn.Model.Should().Be(CodexModel.Parse("attached-direct"));
        CodexAgentOptionsMapper.ConfigureRunTurn(turn, null);
        CodexAgentOptionsMapper.ConfigureRunTurn(turn, new CodexAgentRunOptions().WithCodex(new()));
        turn.Model.Should().Be(CodexModel.Parse("attached-direct"));
    }

    [Fact]
    public void ToolSources_AreCombinedInOrder_AndUnsupportedToolsRejected()
    {
        var configured = AIFunctionFactory.Create(() => "a", "a");
        var attached = AIFunctionFactory.Create(() => "b", "b");
        var direct = AIFunctionFactory.Create(() => "c", "c");
        var chatTool = AIFunctionFactory.Create(() => "d", "d");
        CodexAgentToolMapper.GetAIFunctions([configured], [attached], new CodexAgentRunOptions { Tools = [direct] }, new ChatOptions { Tools = [chatTool] })
            .Should().Equal(configured, attached, direct, chatTool);
        CodexAgentToolMapper.GetAIFunctions(null, null, null, null).Should().BeEmpty();
        Assert.Throws<NotSupportedException>(() => CodexAgentToolMapper.GetAIFunctions([new NonFunctionTool()], null, null, null).ToArray()).Message.Should().Contain("unsupported").And.Contain("NonFunctionTool");
        CodexAgentToolMapper.HasRunTools(null, null).Should().BeFalse();
        CodexAgentToolMapper.HasRunTools(new CodexAgentRunOptions { Tools = [] }, new ChatOptions { Tools = [] }).Should().BeFalse();
        CodexAgentToolMapper.HasRunTools(new CodexAgentRunOptions { Tools = [direct] }, null).Should().BeTrue();
        CodexAgentToolMapper.HasRunTools(new AgentRunOptions().WithCodex(new() { Tools = [attached] }), null).Should().BeTrue();
        CodexAgentToolMapper.HasRunTools(null, new ChatOptions { Tools = [chatTool] }).Should().BeTrue();
    }

    [Fact]
    public void InvocationServicesAndApprovalCallbacks_RespectRunPrecedence()
    {
        var defaultServices = new EmptyServices();
        var attachedServices = new EmptyServices();
        var directServices = new EmptyServices();
        Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>> defaultApproval = (_, _) => ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Approve("default"));
        Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>> attachedApproval = (_, _) => ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Approve("attached"));
        Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>> directApproval = (_, _) => ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Approve("direct"));
        var defaults = new CodexAIAgentOptions { FunctionInvocationServices = defaultServices, ToolApprovalHandler = defaultApproval };
        var run = new CodexAgentRunOptions { FunctionInvocationServices = directServices, ToolApprovalHandler = directApproval }
            .WithCodex(new() { FunctionInvocationServices = attachedServices, ToolApprovalHandler = attachedApproval });
        CodexAgentOptionsMapper.GetFunctionInvocationServices(defaults, run).Should().BeSameAs(directServices);
        CodexAgentOptionsMapper.GetToolApprovalHandler(defaults, run).Should().BeSameAs(directApproval);
        run.FunctionInvocationServices = null; run.ToolApprovalHandler = null;
        CodexAgentOptionsMapper.GetFunctionInvocationServices(defaults, run).Should().BeSameAs(attachedServices);
        CodexAgentOptionsMapper.GetToolApprovalHandler(defaults, run).Should().BeSameAs(attachedApproval);
        run.WithCodex(new());
        CodexAgentOptionsMapper.GetFunctionInvocationServices(defaults, run).Should().BeSameAs(defaultServices);
        CodexAgentOptionsMapper.GetToolApprovalHandler(defaults, run).Should().BeSameAs(defaultApproval);
    }

    [Fact]
    public void ChatClientAgentOptions_AreClonedAndModelOverrideIsOptional()
    {
        Assert.Throws<ArgumentNullException>(() => CodexAgentOptionsMapper.FromChatClientAgentOptions(null!, null)).ParamName.Should().Be("options");
        var options = new ChatClientAgentOptions { Id = "id", Name = "agent", Description = "description", ChatOptions = new() { ModelId = "configured" } };
        var mapped = CodexAgentOptionsMapper.FromChatClientAgentOptions(options, "override");
        mapped.Id.Should().Be("id"); mapped.Name.Should().Be("agent"); mapped.Description.Should().Be("description");
        mapped.ChatOptions.Should().NotBeSameAs(options.ChatOptions);
        mapped.ChatOptions!.ModelId.Should().Be("override");
        options.ChatOptions.ModelId.Should().Be("configured");
        CodexAgentOptionsMapper.FromChatClientAgentOptions(options, " ").ChatOptions!.ModelId.Should().Be("configured");
        CodexAgentOptionsMapper.FromChatClientAgentOptions(new(), null).ChatOptions.Should().NotBeNull();
        var defaults = new CodexAIAgentOptions { Instructions = "default" };
        CodexAgentOptionsMapper.GetInstructions(defaults, new() { Instructions = "run" }).Should().Be("run");
        CodexAgentOptionsMapper.GetInstructions(defaults, new()).Should().Be("default");
        CodexAgentOptionsMapper.GetRunEffort(new AgentRunOptions().WithCodex(new()), new() { Reasoning = new() }).Should().BeNull();
        CodexAgentOptionsMapper.GetRunSummary(new AgentRunOptions().WithCodex(new()), new() { Reasoning = new() }).Should().BeNull();
        CodexAgentToolMapper.HasRunTools(new AgentRunOptions().WithCodex(new() { Tools = [] }), new() { Tools = [] }).Should().BeFalse();
        CodexAgentToolMapper.HasRunTools(new AgentRunOptions().WithCodex(new()), new()).Should().BeFalse();
    }

    [Fact]
    public void UnspecifiedRunAndChatSettings_PreserveAgentDefaults()
    {
        var services = new EmptyServices();
        Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>> approve = (_, _) => ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Approve());
        var defaults = new CodexAIAgentOptions { Instructions = "instructions", Effort = CodexReasoningEffort.High, Summary = "detailed", FunctionInvocationServices = services, ToolApprovalHandler = approve };
        AgentRunOptions?[] runs = [null, new AgentRunOptions(), new CodexAgentRunOptions(), new AgentRunOptions().WithCodex(new()), new CodexAgentRunOptions().WithCodex(new())];
        ChatOptions?[] chats = [null, new(), new() { Reasoning = new() }];
        foreach (var run in runs)
        {
            CodexAgentOptionsMapper.GetFunctionInvocationServices(defaults, run).Should().BeSameAs(services);
            CodexAgentOptionsMapper.GetToolApprovalHandler(defaults, run).Should().BeSameAs(approve);
            foreach (var chat in chats)
            {
                CodexAgentOptionsMapper.GetInstructions(defaults, chat).Should().Be("instructions");
                CodexAgentOptionsMapper.GetEffort(defaults, run, chat).Should().Be(CodexReasoningEffort.High);
                CodexAgentOptionsMapper.GetSummary(defaults, run, chat).Should().Be("detailed");
                CodexAgentToolMapper.HasRunTools(run, chat).Should().BeFalse();
            }
        }
    }

    private sealed class EmptyServices : IServiceProvider { public object? GetService(Type serviceType) => null; }

    private static void AssertOptions(CodexAIAgentOptions defaults, AgentRunOptions? run, ChatOptions? chat, string model, string cwd, CodexApprovalPolicy approval, CodexSandboxMode sandbox, CodexReasoningEffort effort, string summary)
    {
        CodexAgentOptionsMapper.GetModel(defaults, run, chat).Should().Be(model);
        CodexAgentOptionsMapper.GetCwd(defaults, run).Should().Be(cwd);
        CodexAgentOptionsMapper.GetApprovalPolicy(defaults, run).Should().Be(approval);
        CodexAgentOptionsMapper.GetSandbox(defaults, run).Should().Be(sandbox);
        CodexAgentOptionsMapper.GetEffort(defaults, run, chat).Should().Be(effort);
        CodexAgentOptionsMapper.GetSummary(defaults, run, chat).Should().Be(summary);
    }
    private sealed class CustomContent(string? text) : AIContent { public override string? ToString() => text; }
    private sealed class NonFunctionTool : AITool { public override string Name => "unsupported"; }
}
