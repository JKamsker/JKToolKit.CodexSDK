using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Agents;

namespace JKToolKit.CodexSDK.AgentFramework.Tools;

/// <summary>
/// Configures Agent Framework function invocation when creating Codex dynamic tools.
/// </summary>
public sealed class AgentFrameworkCodexToolAdapterOptions
{
    /// <summary>
    /// Gets or sets an optional handler for app-server requests not handled by the adapter.
    /// </summary>
    public IAppServerApprovalHandler? FallbackHandler { get; set; }

    /// <summary>
    /// Gets or sets services made available to Agent Framework function invocations.
    /// </summary>
    public IServiceProvider? FunctionInvocationServices { get; set; }

    /// <summary>
    /// Gets or sets a host approval callback for <see cref="Microsoft.Extensions.AI.ApprovalRequiredAIFunction"/> calls.
    /// </summary>
    public Func<AgentFrameworkToolApprovalRequest, CancellationToken, ValueTask<AgentFrameworkToolApprovalResponse>>? ToolApprovalHandler { get; set; }

    /// <summary>
    /// Gets or sets safety policy for Agent Framework tools exposed to Codex.
    /// </summary>
    public CodexAgentSafetyOptions? SafetyOptions { get; set; }
}
