using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>skills/list</c> response.
/// </summary>
public sealed record class SkillsListResponse
{
    /// <summary>
    /// Gets the response data array when present (raw).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Data)]
    public JsonElement? Data { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

