namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a turn context event containing execution context information.
/// </summary>
/// <remarks>
/// This event provides information about the current turn's execution context,
/// including approval policies and sandbox settings.
/// </remarks>
public record TurnContextEvent : CodexEvent
{
    /// <summary>
    /// Gets the approval policy for the current turn.
    /// </summary>
    /// <remarks>
    /// May be null if approval policy information is not available.
    /// Common values include "auto", "manual", or custom policy identifiers.
    /// </remarks>
    public string? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets the parsed approval policy, if <see cref="ApprovalPolicy"/> is present and well-formed.
    /// </summary>
    public CodexApprovalPolicy? ParsedApprovalPolicy =>
        CodexApprovalPolicy.TryParse(ApprovalPolicy, out var policy) ? policy : (CodexApprovalPolicy?)null;

    /// <summary>
    /// Gets the sandbox policy type for the current turn.
    /// </summary>
    /// <remarks>
    /// May be null if sandbox policy information is not available.
    /// Common values include "none", "strict", or other sandbox configuration identifiers.
    /// </remarks>
    public string? SandboxPolicyType { get; init; }

    /// <summary>
    /// Gets the parsed sandbox mode, if <see cref="SandboxPolicyType"/> is present and well-formed.
    /// </summary>
    public CodexSandboxMode? ParsedSandboxMode =>
        CodexSandboxMode.TryParse(SandboxPolicyType, out var mode) ? mode : (CodexSandboxMode?)null;

    /// <summary>
    /// Gets whether network access is enabled for the current turn's sandbox policy (when provided by Codex).
    /// </summary>
    public bool? NetworkAccess { get; init; }
}
