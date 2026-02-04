using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class ByteRange
{
    [JsonPropertyName("start")]
    public uint Start { get; init; }

    [JsonPropertyName("end")]
    public uint End { get; init; }
}

