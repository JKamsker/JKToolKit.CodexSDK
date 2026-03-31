using System.Text.Json;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting a new turn on an existing thread.
/// </summary>
public sealed class TurnStartOptions
{
    /// <summary>
    /// Gets or sets the input items for the turn.
    /// </summary>
    public IReadOnlyList<TurnInputItem> Input { get; set; } = Array.Empty<TurnInputItem>();

    /// <summary>
    /// Gets or sets an optional working directory for the turn.
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// </remarks>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional approval policy.
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// Known values include <c>untrusted</c>, <c>on-failure</c>, <c>on-request</c>, and <c>never</c>.
    /// </remarks>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets advanced approval policy configuration (object form).
    /// </summary>
    /// <remarks>
    /// When set, this takes precedence over <see cref="ApprovalPolicy"/> and enables upstream features such as
    /// selectively rejecting specific approval prompt types.
    /// </remarks>
    public CodexAskForApproval? AskForApproval { get; set; }

    /// <summary>
    /// Optional approval reviewer routing override (raw JSON object).
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// This can be used to route approval requests to a specific review destination.
    /// </remarks>
    public JsonElement? ApprovalsReviewer { get; set; }

    /// <summary>
    /// Optional sandbox policy override for this turn and subsequent turns.
    /// </summary>
    public SandboxPolicy? SandboxPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional model identifier.
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// </remarks>
    public CodexModel? Model { get; set; }

    /// <summary>
    /// Gets or sets an optional service tier override.
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// Set <see cref="ClearServiceTier"/> to <see langword="true"/> to explicitly clear any inherited service tier
    /// override (serialize <c>"serviceTier": null</c>) instead of inheriting.
    /// </remarks>
    public CodexServiceTier? ServiceTier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to explicitly clear the service tier override for this turn and
    /// subsequent turns.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the SDK serializes <c>"serviceTier": null</c>.
    /// This is distinct from leaving both <see cref="ServiceTier"/> and <see cref="ClearServiceTier"/> unset,
    /// which omits the field and inherits existing behavior.
    /// </remarks>
    public bool ClearServiceTier { get; set; }

    /// <summary>
    /// Gets or sets an optional reasoning effort.
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// </remarks>
    public CodexReasoningEffort? Effort { get; set; }

    /// <summary>
    /// Optional reasoning summary setting (e.g. "auto", "concise", "detailed", "none").
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// </remarks>
    public string? Summary { get; set; }

    /// <summary>
    /// Optional personality identifier (e.g. "friendly", "pragmatic").
    /// </summary>
    /// <remarks>
    /// In the v2 app-server protocol, this override applies to this turn and subsequent turns.
    /// </remarks>
    public string? Personality { get; set; }

    /// <summary>
    /// Optional JSON Schema used to constrain the final assistant message for this turn.
    /// </summary>
    public JsonElement? OutputSchema { get; set; }

    /// <summary>
    /// Optional collaboration mode object (experimental).
    /// </summary>
    /// <remarks>
    /// When set, Codex treats this as taking precedence over some other overrides (such as model, reasoning effort,
    /// and developer instructions).
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public JsonElement? CollaborationMode { get; set; }

    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    /// <returns>A new <see cref="TurnStartOptions"/> instance with the same values.</returns>
    public TurnStartOptions Clone()
    {
        return new TurnStartOptions
        {
            Input = Input,
            Cwd = Cwd,
            ApprovalPolicy = ApprovalPolicy,
            AskForApproval = AskForApproval,
            ApprovalsReviewer = ApprovalsReviewer,
            SandboxPolicy = SandboxPolicy,
            Model = Model,
            ServiceTier = ServiceTier,
            ClearServiceTier = ClearServiceTier,
            Effort = Effort,
            Summary = Summary,
            Personality = Personality,
            OutputSchema = OutputSchema,
            CollaborationMode = CollaborationMode
        };
    }
}
