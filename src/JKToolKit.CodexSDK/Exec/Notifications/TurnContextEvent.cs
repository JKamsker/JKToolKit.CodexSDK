namespace JKToolKit.CodexSDK.Exec.Notifications;

using JKToolKit.CodexSDK.Models;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Represents parsed network permissions emitted in a turn-context payload.
/// </summary>
public sealed record TurnContextNetwork(
    IReadOnlyList<string>? AllowedDomains,
    IReadOnlyList<string>? DeniedDomains);

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

    /// <summary>
    /// Gets the normalized network-access mode when provided (for example <c>enabled</c> or <c>restricted</c>).
    /// </summary>
    public string? NetworkAccessMode { get; init; }

    /// <summary>
    /// Gets the raw sandbox policy payload when provided.
    /// </summary>
    public JsonElement? SandboxPolicyJson { get; init; }

    /// <summary>
    /// Gets the turn identifier associated with this context, if provided.
    /// </summary>
    public string? TurnId { get; init; }

    /// <summary>
    /// Gets the trace identifier associated with this context, if provided.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the canonical working directory captured for this turn.
    /// </summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the current date value emitted for the turn, if available.
    /// </summary>
    public string? CurrentDate { get; init; }

    /// <summary>
    /// Gets the timezone reported for the turn, if available.
    /// </summary>
    public string? Timezone { get; init; }

    /// <summary>
    /// Gets the model identifier captured for this turn, if available.
    /// </summary>
    public CodexModel? Model { get; init; }

    /// <summary>
    /// Gets the personality override, if provided.
    /// </summary>
    public string? Personality { get; init; }

    /// <summary>
    /// Gets any collaboration-mode data emitted in the turn context.
    /// </summary>
    public JsonElement? CollaborationMode { get; init; }

    /// <summary>
    /// Gets whether realtime mode was active when this turn context was emitted.
    /// </summary>
    public bool? RealtimeActive { get; init; }

    /// <summary>
    /// Gets the reasoning effort configured for the turn, if provided.
    /// </summary>
    public CodexReasoningEffort? ReasoningEffort { get; init; }

    /// <summary>
    /// Gets the optional reasoning summary configuration payload.
    /// </summary>
    public JsonElement? ReasoningSummary { get; init; }

    /// <summary>
    /// Gets optional user instructions captured for the turn.
    /// </summary>
    public string? UserInstructions { get; init; }

    /// <summary>
    /// Gets optional developer instructions captured for the turn.
    /// </summary>
    public string? DeveloperInstructions { get; init; }

    /// <summary>
    /// Gets the final-output JSON schema that the turn context requested.
    /// </summary>
    public JsonElement? FinalOutputJsonSchema { get; init; }

    /// <summary>
    /// Gets the truncation policy emitted with the turn context, if any.
    /// </summary>
    public JsonElement? TruncationPolicy { get; init; }

    /// <summary>
    /// Gets the parsed network permissions emitted with the turn context, if any.
    /// </summary>
    public TurnContextNetwork? Network { get; init; }
}
