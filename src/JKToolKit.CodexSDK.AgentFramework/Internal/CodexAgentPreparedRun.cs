using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class CodexAgentPreparedRun
{
    public CodexAgentPreparedRun(
        IReadOnlyCollection<ChatMessage> messages,
        ChatOptions? chatOptions,
        ChatHistoryProvider? chatHistoryProvider)
    {
        Messages = messages;
        ChatOptions = chatOptions;
        ChatHistoryProvider = chatHistoryProvider;
    }

    public IReadOnlyCollection<ChatMessage> Messages { get; }

    public ChatOptions? ChatOptions { get; }

    public ChatHistoryProvider? ChatHistoryProvider { get; }
}
