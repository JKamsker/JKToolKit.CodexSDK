using System.Text.Json.Serialization;
using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/start</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadStartParams
{
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
    /// Gets an optional working directory for the thread.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Cwd)]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets optional thread-scoped runtime workspace roots.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.RuntimeWorkspaceRoots)]
    public IReadOnlyList<string>? RuntimeWorkspaceRoots { get; init; }

    /// <summary>
    /// Gets optional sticky execution environments for turns on this thread.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Environments)]
    public IReadOnlyList<TurnEnvironmentParams>? Environments { get; init; }

    /// <summary>
    /// Gets an optional service tier override for the thread.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ServiceTier)]
    public JsonElement? ServiceTier { get; init; }

    /// <summary>
    /// Gets an optional service name identifier.
    /// </summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>
    /// Gets the optional session-start source.
    /// </summary>
    [JsonPropertyName("sessionStartSource")]
    public string? SessionStartSource { get; init; }

    /// <summary>
    /// Gets an optional project assignment for the new thread.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ProjectId)]
    public string? ProjectId { get; init; }

    /// <summary>
    /// Gets the initial Daybreak choice for this persistent thread.
    /// </summary>
    [JsonPropertyName("daybreakEnabled")]
    public bool? DaybreakEnabled { get; init; }

    /// <summary>
    /// Gets an optional approval policy override for the thread.
    /// </summary>
    /// <remarks>
    /// This supports the upstream <c>AskForApproval</c> union:
    /// either a simple string policy (for example <c>untrusted</c>) or an object form (for example <c>{"reject":{...}}</c>).
    /// </remarks>
    [JsonPropertyName(JsonFieldNames.ApprovalPolicy)]
    public object? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional approval reviewer routing override.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ApprovalsReviewer)]
    public CodexApprovalsReviewer? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets an optional sandbox mode override for the thread (wire value).
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
    /// Gets an optional personality identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Personality)]
    public string? Personality { get; init; }

    /// <summary>
    /// Gets an optional value indicating whether the thread should be ephemeral (not persisted on disk).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Ephemeral)]
    public bool? Ephemeral { get; init; }

    /// <summary>
    /// Gets a value indicating whether to opt into emitting raw response items on the event stream.
    /// </summary>
    /// <remarks>
    /// This is intended for internal use (e.g. Codex Cloud).
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    [JsonPropertyName("experimentalRawEvents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExperimentalRawEvents { get; init; }

    /// <summary>
    /// Gets optional dynamic tool specifications for the thread (experimental).
    /// </summary>
    /// <remarks>
    /// When set, Codex may emit server requests such as <c>item/tool/call</c> that the client must handle via
    /// <c>CodexAppServerClientOptions.ApprovalHandler</c>.
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    [JsonPropertyName("dynamicTools")]
    public IReadOnlyList<DynamicToolSpec>? DynamicTools { get; init; }

}
