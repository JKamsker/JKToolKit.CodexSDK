using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>thread/realtime/start</c> request (v2 protocol).
/// </summary>
public sealed record class ThreadRealtimeStartParams
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.ThreadId)]
    public required string ThreadId { get; init; }

    /// <summary>
    /// Gets the optional realtime session identifier.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.SessionId)]
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the optional realtime transport payload.
    /// </summary>
    [JsonPropertyName("transport")]
    public JsonElement? Transport { get; init; }

    /// <summary>
    /// Gets the optional realtime voice identifier.
    /// </summary>
    [JsonPropertyName("voice")]
    public string? Voice { get; init; }

    /// <summary>
    /// Holds tri-state fields that need precise wire serialization.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
