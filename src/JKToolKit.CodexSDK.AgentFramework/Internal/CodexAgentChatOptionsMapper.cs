using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentChatOptionsMapper
{
    public static async ValueTask<ChatOptions?> GetEffectiveChatOptionsAsync(
        ChatOptions? defaultChatOptions,
        AgentRunOptions? runOptions,
        CancellationToken cancellationToken)
    {
        var runChatOptions = runOptions is ChatClientAgentRunOptions chatRunOptions
            ? chatRunOptions.ChatOptions
            : null;
        var chatOptions = Merge(defaultChatOptions, runChatOptions);

        if (runOptions is not ChatClientAgentRunOptions { ChatClientFactory: { } chatClientFactory })
        {
            return chatOptions;
        }

        return await ApplyChatClientFactoryAsync(
            chatOptions ?? new ChatOptions(),
            chatClientFactory,
            cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<IReadOnlyList<AITool>?> TransformToolsAsync(
        IReadOnlyList<AITool>? tools,
        AgentRunOptions? runOptions,
        CancellationToken cancellationToken)
    {
        if (tools is null ||
            runOptions is not ChatClientAgentRunOptions { ChatClientFactory: { } chatClientFactory })
        {
            return tools;
        }

        var chatOptions = new ChatOptions { Tools = tools.ToArray() };
        var transformedOptions = await ApplyChatClientFactoryAsync(
            chatOptions,
            chatClientFactory,
            cancellationToken).ConfigureAwait(false);
        return transformedOptions.Tools?.ToArray() ?? [];
    }

    private static async ValueTask<ChatOptions> ApplyChatClientFactoryAsync(
        ChatOptions chatOptions,
        Func<IChatClient, IChatClient> chatClientFactory,
        CancellationToken cancellationToken)
    {
        using var captureClient = new CapturingChatClient();
        using var transformedClient = chatClientFactory(captureClient)
            ?? throw new InvalidOperationException("ChatClientAgentRunOptions.ChatClientFactory returned null.");

        await transformedClient.GetResponseAsync([], chatOptions, cancellationToken).ConfigureAwait(false);
        return captureClient.CapturedOptions ?? chatOptions;
    }

    private static ChatOptions? Merge(ChatOptions? defaults, ChatOptions? runOptions)
    {
        if (defaults is null)
        {
            return runOptions?.Clone();
        }

        var merged = defaults.Clone();
        if (runOptions is null)
        {
            return merged;
        }

        ApplyOverrides(merged, runOptions);
        return merged;
    }

    private static void ApplyOverrides(ChatOptions target, ChatOptions source)
    {
        target.AllowMultipleToolCalls = source.AllowMultipleToolCalls ?? target.AllowMultipleToolCalls;
        target.ConversationId = source.ConversationId ?? target.ConversationId;
        target.FrequencyPenalty = source.FrequencyPenalty ?? target.FrequencyPenalty;
        target.Instructions = source.Instructions ?? target.Instructions;
        target.MaxOutputTokens = source.MaxOutputTokens ?? target.MaxOutputTokens;
        target.ModelId = source.ModelId ?? target.ModelId;
        target.PresencePenalty = source.PresencePenalty ?? target.PresencePenalty;
        target.RawRepresentationFactory = source.RawRepresentationFactory ?? target.RawRepresentationFactory;
        target.Reasoning = source.Reasoning ?? target.Reasoning;
        target.ResponseFormat = source.ResponseFormat ?? target.ResponseFormat;
        target.Seed = source.Seed ?? target.Seed;
        target.StopSequences = source.StopSequences ?? target.StopSequences;
        target.Temperature = source.Temperature ?? target.Temperature;
        target.ToolMode = source.ToolMode ?? target.ToolMode;
        target.TopK = source.TopK ?? target.TopK;
        target.TopP = source.TopP ?? target.TopP;
        target.Tools = Combine(target.Tools, source.Tools);
        target.AdditionalProperties = MergeAdditionalProperties(target.AdditionalProperties, source.AdditionalProperties);
    }

    private static IList<AITool>? Combine(IList<AITool>? defaults, IList<AITool>? runTools)
    {
        if (runTools is null)
        {
            return defaults;
        }

        return defaults is null
            ? runTools.ToArray()
            : defaults.Concat(runTools).ToArray();
    }

    private static AdditionalPropertiesDictionary? MergeAdditionalProperties(
        AdditionalPropertiesDictionary? defaults,
        AdditionalPropertiesDictionary? overrides)
    {
        if (overrides is null)
        {
            return defaults;
        }

        var merged = defaults is null
            ? new AdditionalPropertiesDictionary()
            : new AdditionalPropertiesDictionary(defaults);
        foreach (var pair in overrides)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public ChatOptions? CapturedOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CapturedOptions = options;
            return Task.FromResult(new ChatResponse());
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CapturedOptions = options;
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
        }

        public void Dispose()
        {
        }
    }
}
