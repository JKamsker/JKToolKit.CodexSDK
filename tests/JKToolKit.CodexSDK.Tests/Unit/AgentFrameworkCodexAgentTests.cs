using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkCodexAgentTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CodexAgentSession_SerializesThreadIdAndStateBag()
    {
        var agent = new CodexAgentClient().AsAIAgent(name: "NativeCodex");
        var session = (CodexAgentSession)await agent.CreateSessionAsync();
        session.ThreadId = "thread-123";
        session.StateBag.SetValue("memory", new SessionMemory("Alice"), SerializerOptions);

        var serialized = await agent.SerializeSessionAsync(session, SerializerOptions);
        var resumed = (CodexAgentSession)await agent.DeserializeSessionAsync(serialized, SerializerOptions);

        resumed.ThreadId.Should().Be("thread-123");
        var memory = resumed.StateBag.GetValue<SessionMemory>("memory", SerializerOptions);
        memory.Should().NotBeNull();
        memory!.Name.Should().Be("Alice");
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_CreatesNativeAgentWithMetadata()
    {
        var agent = new CodexAgentClient().AsAIAgent(
            model: "gpt-5.5",
            instructions: "You are a helpful assistant.",
            name: "CodexNative",
            description: "Runs Codex as an Agent Framework agent.",
            tools: [AIFunctionFactory.Create((Func<string, string>)(location => location))]);

        agent.Should().BeAssignableTo<AIAgent>();
        agent.Name.Should().Be("CodexNative");
        agent.Description.Should().Be("Runs Codex as an Agent Framework agent.");
        var metadata = agent.GetService(typeof(AIAgentMetadata), serviceKey: null);
        metadata.Should().BeOfType<AIAgentMetadata>().Which.ProviderName.Should().Be("codex");
    }

    [Fact]
    public async Task CodexSdk_AsAIAgent_CreatesNativeAgentWithMetadata()
    {
        await using var sdk = CodexSdk.Create();

        var agent = sdk.AsAIAgent(
            model: "gpt-5.5",
            instructions: "You are a helpful assistant.",
            name: "CodexSdkNative");

        agent.Should().BeAssignableTo<AIAgent>();
        agent.Name.Should().Be("CodexSdkNative");
        var metadata = agent.GetService(typeof(AIAgentMetadata), serviceKey: null);
        metadata.Should().BeOfType<AIAgentMetadata>().Which.ProviderName.Should().Be("codex");
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_AdvertisesFunctionInvocationMiddlewareSupport()
    {
        var agent = new CodexAgentClient().AsAIAgent(name: "NativeCodex");
        Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>> middleware =
            static (_, context, next, cancellationToken) => next(context, cancellationToken);

        var decorated = agent.AsBuilder().Use(middleware).Build();

        agent.GetService(typeof(FunctionInvokingChatClient)).Should().BeOfType<FunctionInvokingChatClient>();
        decorated.Should().BeAssignableTo<AIAgent>();
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_ExposesContextProviders()
    {
        var history = new InMemoryChatHistoryProvider();
        var provider = new TestContextProvider();

        var agent = new CodexAgentClient().AsAIAgent(
            chatHistoryProvider: history,
            aiContextProviders: [provider]);

        var codexAgent = agent.Should().BeOfType<CodexAIAgent>().Subject;
        codexAgent.ChatHistoryProvider.Should().BeSameAs(history);
        codexAgent.AIContextProviders.Should().ContainSingle().Which.Should().BeSameAs(provider);
        agent.GetService(typeof(InMemoryChatHistoryProvider)).Should().BeSameAs(history);
        agent.GetService(typeof(TestContextProvider)).Should().BeSameAs(provider);
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_MapsChatClientAgentOptions()
    {
        var history = new InMemoryChatHistoryProvider();
        var provider = new TestContextProvider();
        var tool = AIFunctionFactory.Create((Func<string>)(() => "ok"), "default_tool");
        var chatOptions = new ChatOptions
        {
            ModelId = "default-model",
            Instructions = "default instructions",
            Tools = [tool]
        };
        var nativeOptions = new ChatClientAgentOptions
        {
            Id = "native-id",
            Name = "NativeCodex",
            Description = "Mapped from ChatClientAgentOptions.",
            ChatOptions = chatOptions,
            ChatHistoryProvider = history,
            AIContextProviders = [provider]
        };

        var agent = new CodexAgentClient().AsAIAgent("override-model", nativeOptions);

        var codexOptions = agent.GetService(typeof(CodexAIAgentOptions)).Should()
            .BeOfType<CodexAIAgentOptions>().Subject;
        codexOptions.Id.Should().Be("native-id");
        codexOptions.Name.Should().Be("NativeCodex");
        codexOptions.Description.Should().Be("Mapped from ChatClientAgentOptions.");
        codexOptions.ChatOptions.Should().NotBeSameAs(chatOptions);
        codexOptions.ChatOptions!.ModelId.Should().Be("override-model");
        codexOptions.ChatOptions.Instructions.Should().Be("default instructions");
        codexOptions.ChatOptions.Tools.Should().ContainSingle().Which.Should().BeSameAs(tool);
        codexOptions.ChatHistoryProvider.Should().BeSameAs(history);
        codexOptions.AIContextProviders.Should().ContainSingle().Which.Should().BeSameAs(provider);
    }

    [Fact]
    public async Task GetEffectiveChatOptionsAsync_MergesDefaultAndRunOptions()
    {
        var defaultTool = AIFunctionFactory.Create((Func<string>)(() => "default"), "default_tool");
        var runTool = AIFunctionFactory.Create((Func<string>)(() => "run"), "run_tool");
        var defaultOptions = new ChatOptions
        {
            ModelId = "default-model",
            Instructions = "default instructions",
            Tools = [defaultTool],
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["default"] = "kept",
                ["shared"] = "default"
            }
        };
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            ModelId = "run-model",
            Tools = [runTool],
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["shared"] = "run"
            }
        });

        var effective = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(
            defaultOptions,
            runOptions,
            CancellationToken.None);

        effective.Should().NotBeSameAs(defaultOptions);
        effective!.ModelId.Should().Be("run-model");
        effective.Instructions.Should().Be("default instructions");
        effective.Tools!.Select(tool => tool.Name).Should().Equal("default_tool", "run_tool");
        effective.AdditionalProperties!["default"].Should().Be("kept");
        effective.AdditionalProperties["shared"].Should().Be("run");
    }

    [Fact]
    public void ChatClientAgentRunOptions_CanCarryCodexConfiguration()
    {
        var options = new ChatClientAgentRunOptions(new ChatOptions())
            .ConfigureCodex(codex =>
            {
                codex.Cwd = "repo";
                codex.Effort = CodexReasoningEffort.High;
            });

        var configuration = options.GetCodexConfiguration();

        configuration.Should().NotBeNull();
        configuration!.Cwd.Should().Be("repo");
        configuration.Effort.Should().Be(CodexReasoningEffort.High);

        var clone = (ChatClientAgentRunOptions)options.Clone();
        clone.GetCodexConfiguration().Should().BeSameAs(configuration);
    }

    [Fact]
    public void CodexAgentRunOptions_Clone_PreservesCodexProperties()
    {
        var options = new CodexAgentRunOptions
        {
            Model = "gpt-5.5",
            Cwd = "repo",
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.ReadOnly,
            Effort = CodexReasoningEffort.High,
            Summary = "auto",
            Tools = [AIFunctionFactory.Create((Func<string>)(() => "ok"), "read_value")]
        };

        var clone = (CodexAgentRunOptions)options.Clone();

        clone.Model.Should().Be(options.Model);
        clone.Cwd.Should().Be(options.Cwd);
        clone.ApprovalPolicy.Should().Be(options.ApprovalPolicy);
        clone.Sandbox.Should().Be(options.Sandbox);
        clone.Effort.Should().Be(options.Effort);
        clone.Summary.Should().Be(options.Summary);
        clone.Tools.Should().NotBeSameAs(options.Tools);
        clone.Tools.Should().BeEquivalentTo(options.Tools);
    }

    private sealed record SessionMemory(string Name);

    private sealed class TestContextProvider : AIContextProvider;
}
