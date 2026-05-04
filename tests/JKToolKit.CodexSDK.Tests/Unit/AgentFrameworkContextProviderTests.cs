using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

#pragma warning disable MAAI001

public sealed class AgentFrameworkContextProviderTests
{
    [Fact]
    public async Task PrepareAsync_AppliesChatHistoryAndAIContextProviders()
    {
        var session = new CodexAgentSession();
        var history = new RecordingChatHistoryProvider([new ChatMessage(ChatRole.Assistant, "remembered")]);
        var contextTool = AIFunctionFactory.Create((Func<string>)(() => "context"), "context_tool");
        var baseTool = AIFunctionFactory.Create((Func<string>)(() => "base"), "base_tool");
        var provider = new RecordingAIContextProvider(
            instructions: "context instructions",
            messages: [new ChatMessage(ChatRole.System, "context message")],
            tools: [contextTool]);
        var pipeline = new CodexAgentContextPipeline(new TestAgent(), history, [provider]);
        var chatOptions = new ChatOptions
        {
            Instructions = "base instructions",
            Tools = [baseTool]
        };

        var prepared = await pipeline.PrepareAsync(
            session,
            [new ChatMessage(ChatRole.User, "hello")],
            chatOptions,
            CancellationToken.None);

        prepared.Messages.Select(message => message.Text)
            .Should()
            .Equal("remembered", "hello", "context message");
        prepared.ChatOptions.Should().NotBeNull();
        prepared.ChatOptions!.Instructions.Should().Be("base instructions\ncontext instructions");
        prepared.ChatOptions.Tools!.Select(tool => tool.Name).Should().Equal("base_tool", "context_tool");
        history.InvokingSession.Should().BeSameAs(session);
        provider.InvokingSession.Should().BeSameAs(session);
    }

    [Fact]
    public async Task NotifySuccessAsync_NotifiesChatHistoryAndAIContextProviders()
    {
        var session = new CodexAgentSession();
        var history = new RecordingChatHistoryProvider([]);
        var provider = new RecordingAIContextProvider();
        var pipeline = new CodexAgentContextPipeline(new TestAgent(), history, [provider]);
        var prepared = await pipeline.PrepareAsync(
            session,
            [new ChatMessage(ChatRole.User, "hello")],
            chatOptions: null,
            CancellationToken.None);

        await pipeline.NotifySuccessAsync(
            prepared,
            session,
            [new ChatMessage(ChatRole.Assistant, "done")],
            CancellationToken.None);

        history.StoredSuccessfully.Should().BeTrue();
        provider.StoredSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Constructor_RejectsProviderStateKeyCollisions()
    {
        var history = new RecordingChatHistoryProvider([], stateKey: "shared");
        var provider = new RecordingAIContextProvider(stateKey: "shared");

        var act = () => new CodexAgentContextPipeline(new TestAgent(), history, [provider]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*state key 'shared'*");
    }

    private sealed class RecordingChatHistoryProvider : ChatHistoryProvider
    {
        private readonly IReadOnlyList<ChatMessage> _history;
        private readonly string _stateKey;

        public RecordingChatHistoryProvider(IReadOnlyList<ChatMessage> history, string stateKey = "history")
        {
            _history = history;
            _stateKey = stateKey;
        }

        public override IReadOnlyList<string> StateKeys => [_stateKey];

        public AgentSession? InvokingSession { get; private set; }

        public bool StoredSuccessfully { get; private set; }

        protected override ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
            ChatHistoryProvider.InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            InvokingSession = context.Session;
            return ValueTask.FromResult<IEnumerable<ChatMessage>>(_history);
        }

        protected override ValueTask StoreChatHistoryAsync(
            ChatHistoryProvider.InvokedContext context,
            CancellationToken cancellationToken = default)
        {
            StoredSuccessfully = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingAIContextProvider : AIContextProvider
    {
        private readonly string? _instructions;
        private readonly IReadOnlyList<ChatMessage>? _messages;
        private readonly string _stateKey;
        private readonly IReadOnlyList<AITool>? _tools;

        public RecordingAIContextProvider(
            string? instructions = null,
            IReadOnlyList<ChatMessage>? messages = null,
            IReadOnlyList<AITool>? tools = null,
            string stateKey = "context")
        {
            _instructions = instructions;
            _messages = messages;
            _tools = tools;
            _stateKey = stateKey;
        }

        public override IReadOnlyList<string> StateKeys => [_stateKey];

        public AgentSession? InvokingSession { get; private set; }

        public bool StoredSuccessfully { get; private set; }

        protected override ValueTask<AIContext> ProvideAIContextAsync(
            AIContextProvider.InvokingContext context,
            CancellationToken cancellationToken = default)
        {
            InvokingSession = context.Session;
            return ValueTask.FromResult(new AIContext
            {
                Instructions = _instructions,
                Messages = _messages,
                Tools = _tools
            });
        }

        protected override ValueTask StoreAIContextAsync(
            AIContextProvider.InvokedContext context,
            CancellationToken cancellationToken = default)
        {
            StoredSuccessfully = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestAgent : AIAgent
    {
        protected override string IdCore => "test";

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<AgentSession>(new CodexAgentSession());
        }

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { }, jsonSerializerOptions));
        }

        protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
            JsonElement serializedSession,
            JsonSerializerOptions? jsonSerializerOptions = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<AgentSession>(new CodexAgentSession());
        }

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session = null,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}

#pragma warning restore MAAI001
