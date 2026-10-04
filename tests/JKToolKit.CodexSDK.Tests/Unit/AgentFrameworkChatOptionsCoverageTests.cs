using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkChatOptionsCoverageTests
{
    [Fact]
    public async Task Merge_OverridesSpecifiedSettings_CombinesTools_AndDoesNotMutateDefaults()
    {
        var first = AIFunctionFactory.Create(() => "first", "first");
        var second = AIFunctionFactory.Create(() => "second", "second");
        var defaults = new ChatOptions
        {
            AllowMultipleToolCalls = false, ConversationId = "default", FrequencyPenalty = 1,
            Instructions = "default", MaxOutputTokens = 100, ModelId = "default", PresencePenalty = 1,
            RawRepresentationFactory = _ => "default", Reasoning = new() { Effort = ReasoningEffort.Low },
            ResponseFormat = ChatResponseFormat.Text, Seed = 1, StopSequences = ["stop"], Temperature = 1,
            ToolMode = ChatToolMode.Auto, TopK = 1, TopP = 1, Tools = [first],
            AdditionalProperties = new() { ["retained"] = 1, ["overridden"] = "old" }
        };
        var overrides = new ChatOptions
        {
            AllowMultipleToolCalls = true, ConversationId = "run", FrequencyPenalty = 2,
            Instructions = "run", MaxOutputTokens = 200, ModelId = "run", PresencePenalty = 2,
            RawRepresentationFactory = _ => "run", Reasoning = new() { Effort = ReasoningEffort.High },
            ResponseFormat = ChatResponseFormat.Json, Seed = 2, StopSequences = ["end"], Temperature = 2,
            ToolMode = ChatToolMode.None, TopK = 2, TopP = .5f, Tools = [second],
            AdditionalProperties = new() { ["added"] = 3, ["overridden"] = "new" }
        };
        var merged = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(defaults, new ChatClientAgentRunOptions { ChatOptions = overrides }, default);
        merged.Should().BeEquivalentTo(overrides, c => c.Excluding(x => x.Tools).Excluding(x => x.AdditionalProperties));
        merged!.Tools.Should().Equal(first, second);
        merged.AdditionalProperties.Should().Contain("retained", 1).And.Contain("added", 3).And.Contain("overridden", "new");
        defaults.ModelId.Should().Be("default");
        defaults.Tools.Should().Equal(first);
        defaults.AdditionalProperties.Should().Contain("overridden", "old").And.NotContainKey("added");
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(defaults, new ChatClientAgentRunOptions { ChatOptions = new() }, default))
            .Should().BeEquivalentTo(defaults);
    }

    [Fact]
    public async Task AbsentDefaults_CloneRunOptions_AndAbsentOptionsRemainNull()
    {
        var run = new ChatOptions { ModelId = "run" };
        var merged = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(null, new ChatClientAgentRunOptions { ChatOptions = run }, default);
        merged.Should().NotBeSameAs(run).And.BeEquivalentTo(run);
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(null, null, default)).Should().BeNull();
        var defaults = new ChatOptions { ModelId = "default" };
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(defaults, null, default)).Should().NotBeSameAs(defaults).And.BeEquivalentTo(defaults);
    }

    [Fact]
    public async Task Merge_WithNoDefaultCollections_CopiesRunCollections()
    {
        var function = AIFunctionFactory.Create(() => "ok", "test");
        var run = new ChatOptions { Tools = [function], AdditionalProperties = new() { ["extra"] = true } };
        var merged = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(new(), new ChatClientAgentRunOptions { ChatOptions = run }, default);
        merged!.Tools.Should().Equal(function).And.NotBeSameAs(run.Tools);
        merged.AdditionalProperties.Should().Contain("extra", true).And.NotBeSameAs(run.AdditionalProperties);
    }

    [Fact]
    public async Task ChatClientFactory_TransformsEffectiveOptions_AndReceivesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var client = default(TransformingClient);
        var options = new ChatClientAgentRunOptions { ChatClientFactory = inner => client = new TransformingClient(inner, (chat, ct) =>
        {
            ct.Should().Be(cancellation.Token);
            chat!.ModelId = "transformed";
        }) };
        var result = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(null, options, cancellation.Token);
        result!.ModelId.Should().Be("transformed");
        client!.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task ChatClientFactory_MayReplaceOptionsOrReturnWithoutCallingInnerClient()
    {
        var replacing = new ChatClientAgentRunOptions { ChatClientFactory = inner => new ReplacingClient(inner, useInner: true) };
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(new() { ModelId = "original" }, replacing, default))!.ModelId.Should().Be("replacement");
        var shortCircuit = new ChatClientAgentRunOptions { ChatClientFactory = inner => new ReplacingClient(inner, useInner: false) };
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(new() { ModelId = "original" }, shortCircuit, default))!.ModelId.Should().Be("original");
    }

    [Fact]
    public async Task ChatClientFactory_NullReturn_IsRejected()
    {
        var options = new ChatClientAgentRunOptions { ChatClientFactory = _ => null! };
        await Assert.ThrowsAsync<InvalidOperationException>(() => CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(null, options, default).AsTask());
    }

    [Fact]
    public async Task ToolTransformation_HandlesNullCollectionsAndRemoval()
    {
        var function = AIFunctionFactory.Create(() => "ok", "test");
        AITool[] tools = [function];
        (await CodexAgentChatOptionsMapper.TransformToolsAsync(null, null, default)).Should().BeNull();
        (await CodexAgentChatOptionsMapper.TransformToolsAsync(tools, null, default)).Should().BeSameAs(tools);
        var options = new ChatClientAgentRunOptions { ChatClientFactory = inner => new TransformingClient(inner, (chat, _) => chat!.Tools = null) };
        (await CodexAgentChatOptionsMapper.TransformToolsAsync(tools, options, default)).Should().BeEmpty();
        options.ChatClientFactory = inner => inner;
        (await CodexAgentChatOptionsMapper.TransformToolsAsync(tools, options, default)).Should().Equal(function);
    }

    [Fact]
    public async Task FactoryMayInspectServices_AndCaptureViaStreaming()
    {
        var options = new ChatClientAgentRunOptions { ChatClientFactory = inner =>
        {
            inner.GetService(typeof(IChatClient)).Should().BeSameAs(inner);
            inner.GetService(typeof(string)).Should().BeNull();
            inner.GetService(typeof(IChatClient), "key").Should().BeNull();
            Assert.Throws<ArgumentNullException>(() => inner.GetService(null!));
            return new StreamCapturingClient(inner);
        } };
        (await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(new() { ModelId = "original" }, options, default))!.ModelId.Should().Be("streamed");
    }

    private sealed class TransformingClient(IChatClient inner, Action<ChatOptions?, CancellationToken> transform) : DelegatingChatClient(inner)
    {
        public bool Disposed { get; private set; }
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        { transform(options, cancellationToken); return base.GetResponseAsync(messages, options, cancellationToken); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class ReplacingClient(IChatClient inner, bool useInner) : DelegatingChatClient(inner)
    {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            useInner ? base.GetResponseAsync(messages, new() { ModelId = "replacement" }, cancellationToken) : Task.FromResult(new ChatResponse());
    }
    private sealed class StreamCapturingClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await foreach (var _ in InnerClient.GetStreamingResponseAsync(messages, new() { ModelId = "streamed" }, cancellationToken)) { }
            return new ChatResponse();
        }
    }
}
