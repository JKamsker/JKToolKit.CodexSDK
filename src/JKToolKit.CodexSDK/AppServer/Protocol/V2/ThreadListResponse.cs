using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>thread/list</c> response.
/// </summary>
public sealed record class ThreadListResponse
{
    /// <summary>
    /// Gets the threads array when present (raw).
    /// </summary>
    [JsonPropertyName("threads")]
    public JsonElement? Threads { get; init; }

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
