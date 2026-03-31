using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting a new thread via the Codex app server.
/// </summary>
public sealed class ThreadStartOptions
{
    /// <summary>
    /// Gets or sets an optional model identifier.
    /// </summary>
    public CodexModel? Model { get; set; }

    /// <summary>
    /// Gets or sets an optional model provider identifier.
    /// </summary>
    public string? ModelProvider { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory for the thread.
    /// </summary>
    public string? Cwd { get; set; }

    /// <summary>
    /// Gets or sets an optional service tier override for the thread.
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
    /// Gets or sets an optional service name identifier for the thread.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets an optional approval policy.
    /// </summary>
    /// <remarks>
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
    /// Gets or sets an optional approval reviewer routing override (raw JSON object).
    /// </summary>
    public JsonElement? ApprovalsReviewer { get; set; }

    /// <summary>
    /// Gets or sets an optional sandbox mode.
    /// </summary>
    /// <remarks>
    /// Known values include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
    /// </remarks>
    public CodexSandboxMode? Sandbox { get; set; }

    /// <summary>
    /// Optional config overrides (arbitrary JSON object).
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
    /// Optional personality identifier (e.g. "friendly", "pragmatic").
    /// </summary>
    public string? Personality { get; set; }

    /// <summary>
    /// Gets or sets an optional value indicating whether the thread should be ephemeral (not persisted on disk).
    /// </summary>
    public bool? Ephemeral { get; set; }

    /// <summary>
    /// If true, opt into emitting raw response items on the event stream.
    /// </summary>
    /// <remarks>
    /// This is intended for internal use (e.g. Codex Cloud).
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public bool ExperimentalRawEvents { get; set; }

    /// <summary>
    /// Gets or sets optional dynamic tool specifications for the thread (experimental).
    /// </summary>
    /// <remarks>
    /// When set, Codex may emit server requests such as <c>item/tool/call</c> that the client must handle via
    /// <see cref="CodexAppServerClientOptions.ApprovalHandler"/>.
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public IReadOnlyList<DynamicToolSpec>? DynamicTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to persist additional rollout event variants required to reconstruct
    /// a richer thread history on subsequent resume/fork/read (experimental).
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    public bool PersistExtendedHistory { get; set; }
}

