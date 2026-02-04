using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer.Protocol;

public sealed record class InitializeResponse
{
    [JsonPropertyName("userAgent")]
    public required string UserAgent { get; init; }
}

