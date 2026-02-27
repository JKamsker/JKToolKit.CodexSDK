using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/appendText</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeAppendTextParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the input text to append.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
