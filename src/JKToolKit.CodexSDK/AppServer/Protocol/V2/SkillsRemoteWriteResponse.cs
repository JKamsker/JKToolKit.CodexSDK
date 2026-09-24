using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Minimal envelope for a <c>skills/remote/write</c> response.
/// </summary>
public sealed record class SkillsRemoteWriteResponse
{
    /// <summary>
    /// Gets the skill identifier, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Id)]
    public string? Id { get; init; }

    /// <summary>
    /// Gets the skill name, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Name)]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the skill path, when present.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    public string? Path { get; init; }

    /// <summary>
    /// Gets additional unmodeled properties for forward compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

