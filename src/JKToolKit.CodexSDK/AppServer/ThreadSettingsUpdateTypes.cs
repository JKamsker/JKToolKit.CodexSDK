using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for experimental <c>thread/settings/update</c>.
/// </summary>
public sealed class ThreadSettingsUpdateOptions
{
    /// <summary>
    /// Gets or sets the thread identifier.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory override.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional approval policy override.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional structured approval policy override.
    /// </summary>
    public CodexAskForApproval? AskForApproval { get; set; }

    /// <summary>
    /// Gets or sets optional approval reviewer routing.
    /// </summary>
    public CodexApprovalsReviewer? ApprovalsReviewer { get; set; }

    /// <summary>
    /// Gets or sets an optional sandbox policy override.
    /// </summary>
    public SandboxPolicy? SandboxPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional named permission profile id.
    /// </summary>
    public string? PermissionProfileId { get; set; }

    /// <summary>
    /// Gets or sets an optional model override.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets an optional service tier override.
    /// </summary>
    public CodexServiceTier? ServiceTier { get; set; }

    /// <summary>
    /// Gets or sets whether the service tier should be cleared.
    /// </summary>
    public bool ClearServiceTier { get; set; }

    /// <summary>
    /// Gets or sets an optional reasoning effort override.
    /// </summary>
    public CodexReasoningEffort? Effort { get; set; }

    /// <summary>
    /// Gets or sets an optional reasoning summary override.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets an optional collaboration mode payload.
    /// </summary>
    public JsonElement? CollaborationMode { get; set; }

    /// <summary>
    /// Gets or sets an optional personality override.
    /// </summary>
    public string? Personality { get; set; }
}

/// <summary>
/// Result returned by <c>thread/settings/update</c>.
/// </summary>
public sealed record class ThreadSettingsUpdateResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
