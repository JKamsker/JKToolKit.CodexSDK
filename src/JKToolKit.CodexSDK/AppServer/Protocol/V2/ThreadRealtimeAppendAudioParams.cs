using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/appendAudio</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeAppendAudioParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the input audio chunk.
    /// </summary>
    [JsonPropertyName("audio")]
    public required ThreadRealtimeAudioChunk Audio { get; init; }
}
