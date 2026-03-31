using JKToolKit.CodexSDK.Models;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for forking an existing thread via the app-server.
/// </summary>
public sealed class ThreadForkOptions
{
    /// <summary>
    /// Gets or sets the thread identifier to fork.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// [UNSTABLE] Gets or sets a rollout path to fork from (experimental-gated in newer upstream Codex builds).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets an optional service tier override for the forked thread.
    /// </summary>
    /// <remarks>
    /// Set <see cref="ClearServiceTier"/> to <see langword="true"/> to explicitly clear any inherited service tier
    /// override instead of inheriting the current value.
    /// </remarks>
    public CodexServiceTier? ServiceTier { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to explicitly clear the service tier override.
    /// </summary>
    public bool ClearServiceTier { get; set; }

    /// <summary>
    /// Gets or sets an optional model identifier.
    /// </summary>
    public CodexModel? Model { get; set; }

    /// <summary>
    /// Gets or sets an optional model provider identifier.
    /// </summary>
    public string? ModelProvider { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory override.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional approval policy.
    /// </summary>
    public CodexApprovalPolicy? ApprovalPolicy { get; set; }

    /// <summary>
    /// Gets or sets advanced approval policy configuration (object form).
    /// </summary>
    public CodexAskForApproval? AskForApproval { get; set; }

    /// <summary>
    /// Gets or sets an optional approval reviewer routing override (raw JSON object).
    /// </summary>
    public JsonElement? ApprovalsReviewer { get; set; }

    /// <summary>
    /// Gets or sets an optional sandbox mode override for the forked thread.
    /// </summary>
    /// <remarks>
    /// Known values include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
    /// </remarks>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Gets or sets optional config overrides (arbitrary JSON object).
    /// </summary>
    public JsonElement? Config { get; set; }

    /// <summary>
    /// Gets or sets optional base instructions.
    /// </summary>
    public string? BaseInstructions { get; set; }

    /// <summary>
    /// Gets or sets optional developer instructions.
    /// </summary>
    public string? DeveloperInstructions { get; set; }

    /// <summary>
    /// Gets or sets an optional value indicating whether the forked thread should be ephemeral.
    /// </summary>
    public bool? Ephemeral { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to persist additional rollout event variants required to reconstruct
    /// a richer thread history on subsequent resume/fork/read (experimental).
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public bool PersistExtendedHistory { get; set; }
}

