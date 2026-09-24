using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>skills/config/write</c> request (v2 protocol).
/// </summary>
public sealed record class SkillsConfigWriteParams
{
    /// <summary>
    /// Gets a value indicating whether skills are enabled.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Enabled)]
    public required bool Enabled { get; init; }

    /// <summary>
    /// Gets the optional path-based selector.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; init; }

    /// <summary>
    /// Gets the optional name-based selector.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Name)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }
}
