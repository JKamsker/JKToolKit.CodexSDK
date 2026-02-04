using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class ImageUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "image";

    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

