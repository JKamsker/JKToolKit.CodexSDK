using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for an <c>app/list</c> response.
/// </summary>
public sealed record class AppListResponse
{
    /// <summary>
    /// Gets the apps array when present (raw).
    /// </summary>
    [JsonPropertyName("apps")]
    public JsonElement? Apps { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

