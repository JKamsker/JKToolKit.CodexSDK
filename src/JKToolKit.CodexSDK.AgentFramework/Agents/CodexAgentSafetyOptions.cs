using System.Text.Json;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Configures safety behavior for Agent Framework tools exposed to Codex.
/// </summary>
public sealed class CodexAgentSafetyOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether every Agent Framework tool call requires host approval.
    /// </summary>
    public bool RequireApprovalForAllAgentFrameworkTools { get; set; }

    /// <summary>
    /// Gets or sets a predicate that can require approval for individual tool calls.
    /// </summary>
    public Func<AIFunction, JsonElement, bool>? RequireApproval { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether tool exception details are redacted from Codex responses.
    /// </summary>
    public bool RedactToolExceptionDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets the exact Agent Framework tool names allowed to run.
    /// </summary>
    public IReadOnlySet<string>? AllowedToolNames { get; set; }

    /// <summary>
    /// Gets or sets the exact Agent Framework tool names denied from running.
    /// </summary>
    public IReadOnlySet<string>? DeniedToolNames { get; set; }

    /// <summary>
    /// Gets or sets the sandbox mode used when neither run options nor agent options specify one.
    /// </summary>
    public CodexSandboxMode? DefaultSandbox { get; set; }
}
