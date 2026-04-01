using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>item/commandExecution/requestApproval</c> server request (v2 protocol).
/// </summary>
public sealed record class CommandExecutionRequestApprovalParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    [JsonPropertyName("turnId")]
    public required string TurnId { get; init; }

    /// <summary>
    /// Gets the item identifier that requested approval.
    /// </summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>
    /// Gets the optional per-callback approval identifier.
    /// </summary>
    [JsonPropertyName("approvalId")]
    public string? ApprovalId { get; init; }

    /// <summary>
    /// Gets the optional explanatory reason for the approval request.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the optional managed-network approval context.
    /// </summary>
    [JsonPropertyName("networkApprovalContext")]
    public NetworkApprovalContext? NetworkApprovalContext { get; init; }

    /// <summary>
    /// Gets the command to be executed, when present.
    /// </summary>
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    /// <summary>
    /// Gets the working directory for the command, when present.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets the best-effort parsed command actions as raw JSON.
    /// </summary>
    [JsonPropertyName("commandActions")]
    public List<JsonElement>? CommandActions { get; init; }

    /// <summary>
    /// Gets any additional permissions requested for the command as raw JSON.
    /// </summary>
    [JsonPropertyName("additionalPermissions")]
    public JsonElement? AdditionalPermissions { get; init; }

    /// <summary>
    /// Gets the proposed execpolicy amendment, when present.
    /// </summary>
    [JsonPropertyName("proposedExecpolicyAmendment")]
    public JsonElement? ProposedExecpolicyAmendment { get; init; }

    /// <summary>
    /// Gets the proposed network policy amendments, when present.
    /// </summary>
    [JsonPropertyName("proposedNetworkPolicyAmendments")]
    public List<JsonElement>? ProposedNetworkPolicyAmendments { get; init; }

    /// <summary>
    /// Gets the ordered list of available decisions as raw JSON union values.
    /// </summary>
    [JsonPropertyName("availableDecisions")]
    public List<JsonElement>? AvailableDecisions { get; init; }
}

/// <summary>
/// Managed-network approval context surfaced on command-execution approval prompts.
/// </summary>
public sealed record class NetworkApprovalContext
{
    /// <summary>
    /// Gets the network host tied to the approval prompt.
    /// </summary>
    [JsonPropertyName("host")]
    public required string Host { get; init; }

    /// <summary>
    /// Gets the transport protocol name.
    /// </summary>
    [JsonPropertyName("protocol")]
    public required string Protocol { get; init; }
}
