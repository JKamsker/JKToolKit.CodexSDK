using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class MentionUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "mention";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

