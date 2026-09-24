using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/fork</c> request (v2 protocol).
/// </summary>
/// <remarks>
/// When <c>path</c> is set, the server forks from that rollout path and ignores <c>threadId</c>.
/// </remarks>
public sealed record class ThreadForkParams
{
    /// <summary>
    /// Gets the thread identifier to fork from (stable).
    /// </summary>
    /// <remarks>
    /// Ignored when <see cref="Path"/> is set.
    /// </remarks>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets an optional last turn id to fork through, inclusive.
    /// </summary>
    [JsonPropertyName("lastTurnId")]
    public string? LastTurnId { get; init; }

    /// <summary>
    /// Gets an optional rollout path to fork from (experimental-gated in newer upstream Codex builds).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    public string? Path { get; init; }

    /// <summary>
    /// Gets an optional service tier override for the forked thread.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ServiceTier)]
    public JsonElement? ServiceTier { get; init; }

    /// <summary>
    /// Gets an optional model identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Model)]
    public string? Model { get; init; }

    /// <summary>
    /// Gets an optional model provider identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ModelProvider)]
    public string? ModelProvider { get; init; }

    /// <summary>
    /// Gets an optional working directory override.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets optional thread-scoped runtime workspace roots.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.RuntimeWorkspaceRoots)]
    public IReadOnlyList<string>? RuntimeWorkspaceRoots { get; init; }

    /// <summary>
    /// Gets an optional approval policy override.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalPolicy)]
    public object? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional approval reviewer routing override.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalsReviewer)]
    public CodexApprovalsReviewer? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets an optional sandbox mode override for the forked thread (wire value).
    /// </summary>
    /// <remarks>
    /// Known values include <c>read-only</c>, <c>workspace-write</c>, and <c>danger-full-access</c>.
    /// </remarks>
    [JsonPropertyName(JsonFieldNames.Sandbox)]
    public string? Sandbox { get; init; }

    /// <summary>
    /// Gets an optional named permission profile id.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Permissions)]
    public string? Permissions { get; init; }

    /// <summary>
    /// Gets optional config overrides (raw JSON object).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Config)]
    public JsonElement? Config { get; init; }

    /// <summary>
    /// Gets optional base instructions.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.BaseInstructions)]
    public string? BaseInstructions { get; init; }

    /// <summary>
    /// Gets optional developer instructions.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.DeveloperInstructions)]
    public string? DeveloperInstructions { get; init; }

    /// <summary>
    /// Gets a value indicating whether the response should omit hydrated turns.
    /// </summary>
    /// <remarks>
    /// Full-history hydration is deprecated for paginated threads; use this with <c>thread/turns/list</c>
    /// and <c>thread/items/list</c> where available.
    /// </remarks>
    [JsonPropertyName(JsonFieldNames.ExcludeTurns)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExcludeTurns { get; init; }

    /// <summary>
    /// Gets an optional value indicating whether the forked thread should be ephemeral.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Ephemeral)]
    public bool? Ephemeral { get; init; }

}
