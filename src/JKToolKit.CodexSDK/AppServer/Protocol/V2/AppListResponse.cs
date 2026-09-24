using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for an <c>app/list</c> response.
/// </summary>
public sealed record class AppListResponse
{
    /// <summary>
    /// Gets the response data array when present (raw).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Data)]
    public JsonElement? Data { get; init; }

    /// <summary>
    /// Gets the next cursor token when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.NextCursor)]
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

