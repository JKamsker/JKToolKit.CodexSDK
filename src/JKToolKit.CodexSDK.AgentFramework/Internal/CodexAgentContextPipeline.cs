using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

#pragma warning disable MAAI001

internal sealed class CodexAgentContextPipeline
{
    private readonly AIAgent _agent;
    private readonly IReadOnlyList<AIContextProvider> _aiContextProviders;
    private readonly ChatHistoryProvider? _chatHistoryProvider;
    private readonly HashSet<string> _aiContextProviderStateKeys;

    public CodexAgentContextPipeline(
        AIAgent agent,
        ChatHistoryProvider? chatHistoryProvider,
        IEnumerable<AIContextProvider>? aiContextProviders)
    {
        _agent = agent;
        _chatHistoryProvider = chatHistoryProvider;
        _aiContextProviders = aiContextProviders?.ToArray() ?? [];
        _aiContextProviderStateKeys = ValidateStateKeys(_chatHistoryProvider, _aiContextProviders);
    }

    public ChatHistoryProvider? ChatHistoryProvider => _chatHistoryProvider;

    public IReadOnlyList<AIContextProvider>? AIContextProviders =>
        _aiContextProviders.Count == 0 ? null : _aiContextProviders;

    public async ValueTask<CodexAgentPreparedRun> PrepareAsync(
        CodexAgentSession session,
        IReadOnlyCollection<ChatMessage> messages,
        ChatOptions? chatOptions,
        CancellationToken cancellationToken)
    {
        var chatHistoryProvider = ResolveChatHistoryProvider(chatOptions);
        IEnumerable<ChatMessage> preparedMessages = messages;

        if (chatHistoryProvider is not null)
        {
            preparedMessages = await chatHistoryProvider.InvokingAsync(
                new ChatHistoryProvider.InvokingContext(_agent, session, preparedMessages),
                cancellationToken).ConfigureAwait(false);
        }

        if (_aiContextProviders.Count > 0)
        {
            var aiContext = new AIContext
            {
                Instructions = chatOptions?.Instructions,
                Messages = preparedMessages,
                Tools = chatOptions?.Tools
            };

            foreach (var provider in _aiContextProviders)
            {
                aiContext = await provider.InvokingAsync(
                    new AIContextProvider.InvokingContext(_agent, session, aiContext),
                    cancellationToken).ConfigureAwait(false);
            }

            preparedMessages = aiContext.Messages ?? [];
            chatOptions = ApplyAIContext(chatOptions, aiContext);
        }

        return new CodexAgentPreparedRun(
            preparedMessages as IReadOnlyCollection<ChatMessage> ?? preparedMessages.ToArray(),
            chatOptions,
            chatHistoryProvider);
    }

    public async ValueTask NotifySuccessAsync(
        CodexAgentPreparedRun run,
        CodexAgentSession session,
        IEnumerable<ChatMessage> responseMessages,
        CancellationToken cancellationToken)
    {
        if (run.ChatHistoryProvider is not null)
        {
            await run.ChatHistoryProvider.InvokedAsync(
                new ChatHistoryProvider.InvokedContext(_agent, session, run.Messages, responseMessages),
                cancellationToken).ConfigureAwait(false);
        }

        if (_aiContextProviders.Count == 0)
        {
            return;
        }

        var context = new AIContextProvider.InvokedContext(_agent, session, run.Messages, responseMessages);
        foreach (var provider in _aiContextProviders)
        {
            await provider.InvokedAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask NotifyFailureAsync(
        CodexAgentPreparedRun run,
        CodexAgentSession session,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (run.ChatHistoryProvider is not null)
        {
            await run.ChatHistoryProvider.InvokedAsync(
                new ChatHistoryProvider.InvokedContext(_agent, session, run.Messages, exception),
                cancellationToken).ConfigureAwait(false);
        }

        if (_aiContextProviders.Count == 0)
        {
            return;
        }

        var context = new AIContextProvider.InvokedContext(_agent, session, run.Messages, exception);
        foreach (var provider in _aiContextProviders)
        {
            await provider.InvokedAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey)
    {
        return _aiContextProviders
                   .Select(provider => provider.GetService(serviceType, serviceKey))
                   .FirstOrDefault(service => service is not null)
               ?? _chatHistoryProvider?.GetService(serviceType, serviceKey);
    }

    private ChatHistoryProvider? ResolveChatHistoryProvider(ChatOptions? chatOptions)
    {
        if (chatOptions?.AdditionalProperties?.TryGetValue<ChatHistoryProvider>(out var provider) == true)
        {
            ValidateChatHistoryProviderStateKeys(provider);
            return provider;
        }

        return _chatHistoryProvider;
    }

    private void ValidateChatHistoryProviderStateKeys(ChatHistoryProvider? provider)
    {
        if (provider is null)
        {
            return;
        }

        foreach (var stateKey in provider.StateKeys)
        {
            if (_aiContextProviderStateKeys.Contains(stateKey))
            {
                throw new InvalidOperationException(
                    $"The ChatHistoryProvider '{provider.GetType().Name}' uses state key '{stateKey}' which is already used by an AIContextProvider.");
            }
        }
    }

    private static ChatOptions ApplyAIContext(ChatOptions? chatOptions, AIContext aiContext)
    {
        var effectiveOptions = chatOptions?.Clone() ?? new ChatOptions();
        effectiveOptions.Instructions = aiContext.Instructions;
        effectiveOptions.Tools = aiContext.Tools?.ToArray();
        return effectiveOptions;
    }

    private static HashSet<string> ValidateStateKeys(
        ChatHistoryProvider? chatHistoryProvider,
        IReadOnlyList<AIContextProvider> aiContextProviders)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in aiContextProviders)
        {
            foreach (var stateKey in provider.StateKeys)
            {
                if (!keys.Add(stateKey))
                {
                    throw new InvalidOperationException(
                        $"The AIContextProvider '{provider.GetType().Name}' uses duplicate state key '{stateKey}'.");
                }
            }
        }

        if (chatHistoryProvider is not null)
        {
            foreach (var stateKey in chatHistoryProvider.StateKeys)
            {
                if (keys.Contains(stateKey))
                {
                    throw new InvalidOperationException(
                        $"The ChatHistoryProvider '{chatHistoryProvider.GetType().Name}' uses state key '{stateKey}' which is already used by an AIContextProvider.");
                }
            }
        }

        return keys;
    }
}

#pragma warning restore MAAI001
