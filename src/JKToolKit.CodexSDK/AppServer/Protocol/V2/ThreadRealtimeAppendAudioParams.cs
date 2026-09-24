using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/appendAudio</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeAppendAudioParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the input audio chunk.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Audio)]
    public required ThreadRealtimeAudioChunk Audio { get; init; }
}
