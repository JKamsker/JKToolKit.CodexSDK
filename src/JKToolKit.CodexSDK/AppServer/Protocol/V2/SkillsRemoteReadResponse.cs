using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>skills/remote/read</c> response.
/// </summary>
public sealed record class SkillsRemoteReadResponse
{
    /// <summary>
    /// Gets the remote skills array when present (raw).
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

