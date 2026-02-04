using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed record class JsonRpcResponseWireMessage
{
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("result")]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }

    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }
}

