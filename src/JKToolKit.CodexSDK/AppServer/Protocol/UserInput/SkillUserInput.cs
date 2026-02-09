using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a skill user input item in the app-server wire format.
/// </summary>
public sealed record class SkillUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>skill</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type => "skill";

    /// <summary>
    /// Gets the skill display name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file system path associated with the skill.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }
}
