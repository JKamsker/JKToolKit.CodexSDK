using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class TextElement
{
    [JsonPropertyName("byteRange")]
    public required ByteRange ByteRange { get; init; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; init; }
}

