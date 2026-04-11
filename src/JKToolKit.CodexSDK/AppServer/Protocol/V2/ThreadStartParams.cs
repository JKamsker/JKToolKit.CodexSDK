using System.Text.Json.Serialization;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/start</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadStartParams
{
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
    /// Gets an optional working directory for the thread.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional service tier override for the thread.
    /// </summary>
    [JsonPropertyName("serviceTier")]
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
    /// Gets an optional approval policy override for the thread.
    /// </summary>
    /// <remarks>
    /// This supports the upstream <c>AskForApproval</c> union:
    /// either a simple string policy (for example <c>untrusted</c>) or an object form (for example <c>{"reject":{...}}</c>).
    /// </remarks>
    [JsonPropertyName("approvalPolicy")]
    public object? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional approval reviewer routing override.
    /// </summary>
    [JsonPropertyName("approvalsReviewer")]
    public CodexApprovalsReviewer? ApprovalsReviewer { get; init; }

    /// <summary>
    /// Gets an optional sandbox mode override for the thread (wire value).
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
    /// Gets an optional personality identifier.
    /// </summary>
    [JsonPropertyName("personality")]
    public string? Personality { get; init; }

    /// <summary>
    /// Gets an optional value indicating whether the thread should be ephemeral (not persisted on disk).
    /// </summary>
    [JsonPropertyName("ephemeral")]
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
