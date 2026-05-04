using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Per-run options for a Codex-backed Agent Framework agent.
/// </summary>
public sealed class CodexAgentRunOptions : AgentRunOptions
{
    /// <summary>
    /// Gets or sets a per-run model override.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets a per-run working directory override.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets a per-run approval policy override.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets a per-run sandbox override.
    /// </summary>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Gets or sets per-run Agent Framework tools.
    /// </summary>
    public IReadOnlyList<AITool>? Tools { get; set; }

    /// <summary>
    /// Gets or sets additional turn configuration applied to this Codex turn.
    /// </summary>
    public Action<TurnStartOptions>? ConfigureTurn { get; set; }
}
