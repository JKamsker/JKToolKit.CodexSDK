using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>skills/config/write</c> response.
/// </summary>
public sealed record class SkillsConfigWriteResponse
{
    /// <summary>
    /// Gets the effective enabled value after applying the config update.
    /// </summary>
    [JsonPropertyName("effectiveEnabled")]
    public bool? EffectiveEnabled { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

