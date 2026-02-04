using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class SkillUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "skill";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

