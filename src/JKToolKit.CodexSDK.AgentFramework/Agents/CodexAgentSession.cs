using JKToolKit.CodexSDK.Models;
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

    /// <summary>
    /// Gets or sets the dynamic tool schema hash captured when the backing Codex thread was created.
    /// </summary>
    public string? ToolSchemaHash { get; set; }

    /// <summary>
    /// Gets or sets the model captured when the backing Codex thread was created.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the working directory captured when the backing Codex thread was created.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets the approval policy captured when the backing Codex thread was created.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets the sandbox mode captured when the backing Codex thread was created.
    /// </summary>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the backing Codex thread was created.
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }
}
