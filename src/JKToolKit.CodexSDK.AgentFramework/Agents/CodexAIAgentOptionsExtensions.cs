using JKToolKit.CodexSDK.AgentFramework.Internal;
using Microsoft.Agents.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Extension methods for creating Codex-backed agent options from Agent Framework options.
/// </summary>
public static class CodexAIAgentOptionsExtensions
{
    /// <summary>
    /// Converts Agent Framework chat-client agent options into Codex-backed agent options.
    /// </summary>
    public static CodexAIAgentOptions ToCodexAIAgentOptions(
        this ChatClientAgentOptions options,
        string? model = null)
    {
        return CodexAgentOptionsMapper.FromChatClientAgentOptions(options, model);
    }
}
