using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/start</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeStartParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the initial prompt.
    /// </summary>
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the optional realtime session identifier.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}
