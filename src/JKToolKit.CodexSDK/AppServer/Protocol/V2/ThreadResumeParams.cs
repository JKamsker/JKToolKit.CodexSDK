using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/resume</c> request (v2 protocol).
/// </summary>
/// <remarks>
/// Codex supports three resume modes: by <c>threadId</c> (load from disk), by <c>history</c> (in-memory history),
/// or by <c>path</c> (load from a rollout path on disk). Precedence is <c>history</c> &gt; <c>path</c> &gt; <c>threadId</c>.
/// When using <c>history</c> or <c>path</c>, the <c>threadId</c> parameter is ignored.
/// </remarks>
public sealed record class ThreadResumeParams
{
    /// <summary>
    /// Gets the thread identifier to resume (when resuming by ID).
    /// </summary>
    [JsonPropertyName("threadId")]
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets an optional history override to resume from (raw JSON).
    /// </summary>
    /// <remarks>
    /// This field is unstable / intended for internal use (Codex Cloud).
    /// If specified, the server resumes the thread from the provided history instead of loading from disk.
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    [JsonPropertyName("history")]
    public JsonElement? History { get; init; }

    /// <summary>
    /// Gets an optional rollout path to resume from (raw filesystem path).
    /// </summary>
    /// <remarks>
    /// This field is unstable. If specified, the server loads the thread from the given path on disk.
    /// This field is gated behind app-server experimental API capabilities in newer upstream Codex builds.
    /// </remarks>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

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
    /// Gets an optional working directory for the resumed thread.
    /// </summary>
    [JsonPropertyName("cwd")]
    public string? Cwd { get; init; }

    /// <summary>
    /// Gets an optional service tier override for the resumed thread.
    /// </summary>
    [JsonPropertyName("serviceTier")]
    public string? ServiceTier { get; init; }

    /// <summary>
    /// Gets an optional approval policy override for the resumed thread.
    /// </summary>
    /// <remarks>
    /// This supports the upstream <c>AskForApproval</c> union:
    /// either a simple string policy (for example <c>untrusted</c>) or an object form (for example <c>{"reject":{...}}</c>).
    /// When serializing, System.Text.Json uses the runtime type of the assigned value, so consumers should only assign
    /// a string policy or an object that matches the union shape (for example <c>new { reject = new CodexAskForApprovalReject { ... } }</c>).
    /// When deserializing into <see cref="object"/>, System.Text.Json materializes this value as a <see cref="JsonElement"/>;
    /// do not rely on strong-typed reads after deserialization.
    /// </remarks>
    [JsonPropertyName("approvalPolicy")]
    public object? ApprovalPolicy { get; init; }

    /// <summary>
    /// Gets an optional sandbox mode override for the resumed thread (wire value).
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
