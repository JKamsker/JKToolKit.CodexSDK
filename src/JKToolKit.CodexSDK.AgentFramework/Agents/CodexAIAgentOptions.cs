using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Configures a Codex-backed Microsoft Agent Framework agent.
/// </summary>
public sealed class CodexAIAgentOptions
{
    /// <summary>
    /// Gets or sets the stable Agent Framework agent id.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the agent description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets instructions passed to Codex as developer instructions.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Gets or sets the Codex model id.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets default Agent Framework chat options used for each Codex run.
    /// </summary>
    public ChatOptions? ChatOptions { get; set; }

    /// <summary>
    /// Gets or sets the working directory used for Codex threads.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets the default Codex approval policy.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets the default Codex sandbox mode.
    /// </summary>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Gets or sets the default Codex reasoning effort.
    /// </summary>
    public CodexReasoningEffort? Effort { get; set; }

    /// <summary>
    /// Gets or sets the default Codex reasoning summary setting.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the Agent Framework tools exposed to Codex.
    /// </summary>
    public IReadOnlyList<AITool>? Tools { get; set; }

    /// <summary>
    /// Gets or sets the Agent Framework chat history provider used to enrich Codex runs.
    /// </summary>
    public ChatHistoryProvider? ChatHistoryProvider { get; set; }

    /// <summary>
    /// Gets or sets Agent Framework context providers that add run-specific instructions, messages, or tools.
    /// </summary>
    public IEnumerable<AIContextProvider>? AIContextProviders { get; set; }

    /// <summary>
    /// Gets or sets services made available to Agent Framework function invocations.
    /// </summary>
    public IServiceProvider? FunctionInvocationServices { get; set; }

    /// <summary>
    /// Gets or sets a host approval callback for <see cref="ApprovalRequiredAIFunction"/> calls.
    /// </summary>
    public Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? ToolApprovalHandler { get; set; }

    /// <summary>
    /// Gets or sets additional thread configuration applied when a Codex thread is created.
    /// </summary>
    public Action<ThreadStartOptions>? ConfigureThread { get; set; }

    /// <summary>
    /// Gets or sets additional turn configuration applied to each Codex turn.
    /// </summary>
    public Action<TurnStartOptions>? ConfigureTurn { get; set; }
}
