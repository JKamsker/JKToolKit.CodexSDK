using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal static class CodexAgentChatOptionsMapper
{
    public static async ValueTask<ChatOptions?> GetEffectiveChatOptionsAsync(
        AgentRunOptions? runOptions,
        CancellationToken cancellationToken)
    {
        if (runOptions is not ChatClientAgentRunOptions chatRunOptions)
        {
            return null;
        }

        var chatOptions = chatRunOptions.ChatOptions?.Clone() ?? new ChatOptions();
        if (chatRunOptions.ChatClientFactory is null)
        {
            return chatOptions;
        }

        return await ApplyChatClientFactoryAsync(
            chatOptions,
            chatRunOptions.ChatClientFactory,
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
