using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>thread/loaded/list</c> response.
/// </summary>
public sealed record class ThreadLoadedListResponse
{
    /// <summary>
    /// Gets the loaded thread identifier array when present (raw).
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }

    /// <summary>
    /// Gets the next cursor token when present.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

