using Microsoft.Agents.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Agent Framework session that stores the backing Codex thread id.
/// </summary>
public sealed class CodexAgentSession : AgentSession
{
    /// <summary>
    /// Creates an empty Codex agent session.
    /// </summary>
    public CodexAgentSession()
    {
    }

    internal CodexAgentSession(string? threadId, AgentSessionStateBag stateBag)
        : base(stateBag)
    {
        ThreadId = threadId;
    }

    /// <summary>
    /// Gets the Codex thread id once the first run has created or resumed a thread.
    /// </summary>
    public string? ThreadId { get; set; }
}
