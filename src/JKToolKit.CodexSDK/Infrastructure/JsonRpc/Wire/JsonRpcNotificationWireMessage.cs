using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Wire;

internal sealed record class JsonRpcNotificationWireMessage
{
    [JsonPropertyName(JsonFieldNames.Method)]
    public required string Method { get; init; }

    [JsonPropertyName(JsonFieldNames.Params)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }

    [JsonPropertyName(JsonFieldNames.Jsonrpc)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JsonRpc { get; init; }
}
