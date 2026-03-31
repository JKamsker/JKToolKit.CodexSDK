using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire representation of a thread realtime audio chunk (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeAudioChunk
{
    /// <summary>
    /// Gets the optional realtime item identifier associated with this chunk.
    /// </summary>
    [JsonPropertyName("itemId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ItemId { get; init; }

    /// <summary>
    /// Gets the audio data chunk (typically base64-encoded).
    /// </summary>
    [JsonPropertyName("data")]
    public required string Data { get; init; }

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    [JsonPropertyName("numChannels")]
    public required int NumChannels { get; init; }

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    [JsonPropertyName("sampleRate")]
    public required int SampleRate { get; init; }

    /// <summary>
    /// Gets the samples per channel, if provided.
    /// </summary>
    [JsonPropertyName("samplesPerChannel")]
    public int? SamplesPerChannel { get; init; }
}
