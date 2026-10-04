using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkInvocationContentContractTests
{
    [Fact]
    public async Task StandaloneToolInvocation_ExposesAnEmptyConversationToMiddleware()
    {
        IReadOnlyList<ChatMessage>? observedMessages = null;
        var function = AIFunctionFactory.Create(() =>
        {
            observedMessages = FunctionInvokingChatClient.CurrentContext!.Messages.ToArray();
            return "completed";
        }, "inspect_messages");
        var tools = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = JsonSerializer.SerializeToElement(new
        {
            threadId = "thread-1", turnId = "turn-1", callId = "call-1", tool = "inspect_messages", arguments = new { }
        });

        var response = await tools.ApprovalHandler.HandleAsync("item/tool/call", request, default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        observedMessages.Should().NotBeNull().And.BeEmpty("standalone tools have no agent conversation to expose");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatClientFactory_ReceivesAnEmptyRequestWhenTransformingConfiguration(bool transformTools)
    {
        InspectingChatClient? middleware = null;
        var runOptions = new ChatClientAgentRunOptions
        {
            ChatClientFactory = inner => middleware = new InspectingChatClient(inner)
        };
        var function = AIFunctionFactory.Create(() => "result", "tool");

        if (transformTools)
        {
            var tools = await CodexAgentChatOptionsMapper.TransformToolsAsync([function], runOptions, default);
            tools.Should().Equal(function);
        }
        else
        {
            var options = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(new() { ModelId = "configured" }, runOptions, default);
            options!.ModelId.Should().Be("configured");
        }

        middleware.Should().NotBeNull();
        middleware!.ObservedMessages.Should().NotBeNull().And.BeEmpty(
            "applying a configuration factory must not invent conversation messages");
    }

    [Fact]
    public async Task MixedToolResults_RetainEmptyItemsBeforeFollowingContent()
    {
        var function = AIFunctionFactory.Create(() => new AIContent[]
        {
            new FunctionResultContent("nested-call", null),
            new TextContent("tail")
        }, "mixed_results");
        var tools = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = JsonSerializer.SerializeToElement(new
        {
            threadId = "thread-1", turnId = "turn-1", callId = "call-1", tool = "mixed_results", arguments = new { }
        });

        var response = await tools.ApprovalHandler.HandleAsync("item/tool/call", request, default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        var items = response.GetProperty("contentItems");
        items.GetArrayLength().Should().Be(2);
        items[0].GetProperty("type").GetString().Should().Be("inputText");
        items[0].GetProperty("text").GetString().Should().BeEmpty();
        items[1].GetProperty("text").GetString().Should().Be("tail");
    }

    private sealed class InspectingChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public IReadOnlyList<ChatMessage>? ObservedMessages { get; private set; }

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ObservedMessages = messages.ToArray();
            return base.GetResponseAsync(messages, options, cancellationToken);
        }
    }
}
