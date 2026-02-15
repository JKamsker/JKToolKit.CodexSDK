using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.V2;

/// <summary>
/// Wire parameters for the <c>skills/config/write</c> request (v2 protocol).
/// </summary>
public sealed record class SkillsConfigWriteParams
{
    /// <summary>
    /// Gets a value indicating whether skills are enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public required bool Enabled { get; init; }

    /// <summary>
    /// Gets the skills configuration path.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
