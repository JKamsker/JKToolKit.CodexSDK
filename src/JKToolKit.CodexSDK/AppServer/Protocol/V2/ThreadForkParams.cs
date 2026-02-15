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
}
