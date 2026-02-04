using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed record class JsonRpcRequestWireMessage
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object? Params { get; init; }

    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }
}

