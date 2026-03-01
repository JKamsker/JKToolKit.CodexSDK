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
    public string? ThreadId { get; init; }

    /// <summary>
    /// Gets an optional rollout path to fork from (experimental-gated in newer upstream Codex builds).
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

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
