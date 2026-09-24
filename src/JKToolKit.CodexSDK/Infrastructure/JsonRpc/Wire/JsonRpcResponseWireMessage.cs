using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Wire;

internal sealed record class JsonRpcResponseWireMessage
{
    [JsonPropertyName(JsonFieldNames.Id)]
    public JsonElement Id { get; init; }

    [JsonPropertyName(JsonFieldNames.Result)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName(JsonFieldNames.Error)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; init; }

    [JsonPropertyName(JsonFieldNames.Jsonrpc)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? JsonRpc { get; init; }
}
