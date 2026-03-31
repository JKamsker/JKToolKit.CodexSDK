using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/fork</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadForkParams
{
    /// <summary>
    /// Gets the thread identifier to fork from (stable).
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets an optional rollout path to fork from (experimental-gated in newer upstream Codex builds).
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>
    /// Gets an optional service tier override for the forked thread.
    /// </summary>
    [JsonPropertyName("serviceTier")]
    public JsonElement? ServiceTier { get; init; }

    /// <summary>
    /// Gets an optional model identifier.
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Gets an optional model provider identifier.
    /// </summary>
    [JsonPropertyName("modelProvider")]
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets an optional working directory override.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional approval policy override.
    /// </summary>
    [JsonPropertyName("approvalPolicy")]
    public object? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional approval reviewer routing override (raw JSON object).
    /// </summary>
    [JsonPropertyName("approvalsReviewer")]
    public JsonElement? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets an optional sandbox mode override for the forked thread (wire value).
    /// </summary>
    /// <remarks>
    /// Known values include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
    /// </remarks>
    [JsonPropertyName("sandbox")]
    public string? Sandbox { get; init; }

    /// <summary>
    /// Gets optional config overrides (raw JSON object).
    /// </summary>
    [JsonPropertyName("config")]
    public JsonElement? Config { get; init; }

    /// <summary>
    /// Gets optional base instructions.
    /// </summary>
    [JsonPropertyName("baseInstructions")]
    public string? BaseInstructions { get; init; }

    /// <summary>
    /// Gets optional developer instructions.
    /// </summary>
    [JsonPropertyName("developerInstructions")]
    public string? DeveloperInstructions { get; init; }

    /// <summary>
    /// Gets an optional value indicating whether the forked thread should be ephemeral.
    /// </summary>
    [JsonPropertyName("ephemeral")]
    public bool? Ephemeral { get; init; }

    /// <summary>
    /// Gets a value indicating whether to persist additional rollout event variants required to reconstruct a richer
    /// thread history on subsequent resume/fork/read (experimental).
    /// </summary>
    /// <remarks>
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    [JsonPropertyName("persistExtendedHistory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PersistExtendedHistory { get; init; }
}
