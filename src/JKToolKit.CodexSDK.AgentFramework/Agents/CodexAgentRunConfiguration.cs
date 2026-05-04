using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Codex-specific settings that can be attached to Agent Framework run options.
/// </summary>
public sealed class CodexAgentRunConfiguration
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
    /// Gets or sets a per-run reasoning effort override.
    /// </summary>
    public CodexReasoningEffort? Effort { get; set; }

    /// <summary>
    /// Gets or sets a per-run reasoning summary override.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets per-run Agent Framework tools.
    /// </summary>
    public IReadOnlyList<AITool>? Tools { get; set; }

    /// <summary>
    /// Gets or sets services made available to Agent Framework function invocations.
    /// </summary>
    public IServiceProvider? FunctionInvocationServices { get; set; }

    /// <summary>
    /// Gets or sets a host approval callback for <see cref="ApprovalRequiredAIFunction"/> calls.
    /// </summary>
    public Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? ToolApprovalHandler { get; set; }

    /// <summary>
    /// Gets or sets additional turn configuration applied to this Codex turn.
    /// </summary>
    public Action<TurnStartOptions>? ConfigureTurn { get; set; }
}
