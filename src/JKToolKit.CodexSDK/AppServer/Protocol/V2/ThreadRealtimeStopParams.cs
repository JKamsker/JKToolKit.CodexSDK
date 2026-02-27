using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/stop</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeStopParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName("threadId")]
    public required string ThreadId { get; init; }
}
