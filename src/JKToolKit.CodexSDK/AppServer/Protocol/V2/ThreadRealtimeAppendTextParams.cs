using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/appendText</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeAppendTextParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the input text to append.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Text)]
    public required string Text { get; init; }
}
