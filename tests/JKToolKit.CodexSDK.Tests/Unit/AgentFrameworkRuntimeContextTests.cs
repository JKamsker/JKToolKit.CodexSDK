using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkRuntimeContextTests
{
    [Fact]
    public async Task ApprovalHandler_PreservesAmbientAgentRunContext()
    {
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions { ModelId = "gpt-5.5" });
        var session = new CodexAgentSession();
        var agent = new AmbientRunContextAgent();
        agent.SetRunContext(
            session,
            [new ChatMessage(ChatRole.User, "hello")],
            runOptions);
        var function = AIFunctionFactory.Create(
            (Func<string>)(() =>
            {
                var functionContext = FunctionInvokingChatClient.CurrentContext;
                var runContext = AIAgent.CurrentRunContext;
                var sessionState = ReferenceEquals(runContext?.Session, session) ? "session" : "missing";
                return $"{functionContext?.Messages.Count}:{sessionState}:{functionContext?.Options?.ModelId}";
            }),
            "read_ambient_context");
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("read_ambient_context", new { });

        try
        {
            var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

            response.GetProperty("success").GetBoolean().Should().BeTrue();
            response.GetProperty("contentItems")[0].GetProperty("text").GetString()
                .Should()
                .Be("1:session:gpt-5.5");
        }
        finally
        {
            agent.ClearRunContext();
        }
    }

    [Fact]
    public async Task ApprovalHandler_UsesEffectiveChatOptionsForFunctionInvocationContext()
    {
        var runOptions = new ChatClientAgentRunOptions(new ChatOptions { ModelId = "original" });
        var session = new CodexAgentSession();
        var agent = new AmbientRunContextAgent();
        agent.SetRunContext(session, [new ChatMessage(ChatRole.User, "hello")], runOptions);
        var function = AIFunctionFactory.Create(
            (Func<string>)(() => FunctionInvokingChatClient.CurrentContext?.Options?.ModelId ?? "missing"),
            "read_effective_options");
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("read_effective_options", new { });

        try
        {
            using var scope = AgentFrameworkFunctionInvoker.PushEffectiveChatOptions(new ChatOptions { ModelId = "effective" });

            var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

            response.GetProperty("success").GetBoolean().Should().BeTrue();
            response.GetProperty("contentItems")[0].GetProperty("text").GetString()
                .Should()
                .Be("effective");
        }
        finally
        {
            agent.ClearRunContext();
        }
    }

    private static JsonElement CreateToolCall(string toolName, object arguments)
    {
        return JsonSerializer.SerializeToElement(new DynamicToolCallParams
        {
            ThreadId = "thread-1",
            TurnId = "turn-1",
            CallId = "call-1",
            Tool = toolName,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        });
    }

    private sealed class AmbientRunContextAgent : AIAgent
    {
        protected override string IdCore => "ambient";

        public void SetRunContext(
            AgentSession session,
            IReadOnlyCollection<ChatMessage> messages,
            AgentRunOptions options)
        {
            CurrentRunContext = new AgentRunContext(this, session, messages, options);
        }

        public void ClearRunContext()
        {
            CurrentRunContext = null;
        }

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
