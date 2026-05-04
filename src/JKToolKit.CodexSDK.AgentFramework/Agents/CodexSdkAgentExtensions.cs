using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Extension methods for creating Agent Framework agents from a configured Codex SDK facade.
/// </summary>
public static class CodexSdkAgentExtensions
{
    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by an existing Codex SDK facade.
    /// </summary>
    public static AIAgent AsAIAgent(this CodexSdk sdk, CodexAIAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(sdk);
        ArgumentNullException.ThrowIfNull(options);

        return new CodexAgentClient(sdk).AsAIAgent(options);
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by an existing Codex SDK facade.
    /// </summary>
    public static AIAgent AsAIAgent(
        this CodexSdk sdk,
        string? model = null,
        string? instructions = null,
        string? name = null,
        string? description = null,
        IEnumerable<AITool>? tools = null,
        ChatHistoryProvider? chatHistoryProvider = null,
        IEnumerable<AIContextProvider>? aiContextProviders = null)
    {
        ArgumentNullException.ThrowIfNull(sdk);

        return new CodexAgentClient(sdk).AsAIAgent(
            model,
            instructions,
            name,
            description,
            tools,
            chatHistoryProvider,
            aiContextProviders);
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by an existing Codex SDK facade.
    /// </summary>
    public static AIAgent AsAIAgent(this CodexSdk sdk, ChatClientAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(sdk);
        ArgumentNullException.ThrowIfNull(options);

        return new CodexAgentClient(sdk).AsAIAgent(options);
    }

    /// <summary>
    /// Creates an Agent Framework <see cref="AIAgent"/> backed by an existing Codex SDK facade.
    /// </summary>
    public static AIAgent AsAIAgent(
        this CodexSdk sdk,
        string? model,
        ChatClientAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(sdk);
        ArgumentNullException.ThrowIfNull(options);

        return new CodexAgentClient(sdk).AsAIAgent(model, options);
    }
}
