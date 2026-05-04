using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class CodexAgentNoOpChatClient : IChatClient
{
    public static CodexAgentNoOpChatClient Instance { get; } = new();

    private CodexAgentNoOpChatClient()
    {
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatResponse());
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
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
