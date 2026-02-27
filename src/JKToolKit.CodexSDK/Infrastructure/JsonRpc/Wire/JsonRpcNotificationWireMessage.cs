using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Wire;

internal sealed record class JsonRpcNotificationWireMessage
{
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }

    [JsonPropertyName("jsonrpc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JsonRpc { get; init; }
}
