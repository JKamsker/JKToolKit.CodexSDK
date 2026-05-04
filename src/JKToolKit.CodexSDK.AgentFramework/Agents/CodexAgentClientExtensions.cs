using JKToolKit.CodexSDK.AgentFramework.Internal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Extension methods for creating Agent Framework agents backed by Codex.
/// </summary>
public static class CodexAgentClientExtensions
{
    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by Codex app-server.
    /// </summary>
    public static AIAgent AsAIAgent(this CodexAgentClient client, CodexAIAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        return new CodexAIAgent(client, options);
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by Codex app-server.
    /// </summary>
    public static AIAgent AsAIAgent(
        this CodexAgentClient client,
        string? model = null,
        string? instructions = null,
        string? name = null,
        string? description = null,
        IEnumerable<AITool>? tools = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IEnumerable<AIContextProvider>? aiContextProviders = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.AsAIAgent(new CodexAIAgentOptions
        {
            Model = model,
            Instructions = instructions,
            Name = name,
            Description = description,
            Tools = tools?.ToArray(),
            ChatHistoryProvider = chatHistoryProvider,
            AIContextProviders = aiContextProviders?.ToArray()
        });
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by Codex app-server.
    /// </summary>
    public static AIAgent AsAIAgent(this CodexAgentClient client, ChatClientAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        return client.AsAIAgent(CodexAgentOptionsMapper.FromChatClientAgentOptions(options, model: null));
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by Codex app-server.
    /// </summary>
    public static AIAgent AsAIAgent(
        this CodexAgentClient client,
        string? model,
        ChatClientAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        return client.AsAIAgent(CodexAgentOptionsMapper.FromChatClientAgentOptions(options, model));
    }
}
