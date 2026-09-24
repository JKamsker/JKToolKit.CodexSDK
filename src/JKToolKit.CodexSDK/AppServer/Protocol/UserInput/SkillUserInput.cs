using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.AppServer.Protocol.UserInput;

/// <summary>
/// Represents a skill user input item in the app-server wire format.
/// </summary>
public sealed record class SkillUserInput : IUserInput
{
    /// <summary>
    /// Gets the wire discriminator value (<c>skill</c>).
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Type)]
    public string Type => "skill";

    /// <summary>
    /// Gets the skill display name.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Name)]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the file system path associated with the skill.
    /// </summary>
    [JsonPropertyName(JsonFieldNames.Path)]
    public required string Path { get; init; }
}
