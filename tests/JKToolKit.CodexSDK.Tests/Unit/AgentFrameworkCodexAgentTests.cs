using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
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
