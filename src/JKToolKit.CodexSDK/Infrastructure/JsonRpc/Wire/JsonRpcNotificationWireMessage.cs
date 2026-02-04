using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed record class JsonRpcNotificationWireMessage
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object? Params { get; init; }

    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }
}

