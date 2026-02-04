using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class LocalImageUserInput : IUserInput
{
    [JsonPropertyName("type")]
    public string Type => "localImage";

    [JsonPropertyName("path")]
    public required string Path { get; init; }
}

