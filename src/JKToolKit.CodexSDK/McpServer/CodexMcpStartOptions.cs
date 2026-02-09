using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Options for starting a Codex session via the MCP server tool interface.
/// </summary>
public sealed class CodexMcpStartOptions
{
    /// <summary>
    /// Gets or sets the prompt to send to Codex.
    /// </summary>
    public required string Prompt { get; set; }

    /// <summary>
    /// Gets or sets an optional approval policy.
    /// </summary>
    /// <remarks>
    /// Known values include <c>untrusted</c>, <c>on-failure</c>, <c>on-request</c>, and <c>never</c>.
    /// </remarks>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional sandbox mode.
    /// </summary>
    /// <remarks>
    /// Known values include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
    /// </remarks>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional model identifier.
    /// </summary>
    public CodexModel? Model { get; set; }

    /// <summary>
    /// Gets or sets an optional value indicating whether to include the plan tool.
    /// </summary>
    public bool? IncludePlanTool { get; set; }
}

