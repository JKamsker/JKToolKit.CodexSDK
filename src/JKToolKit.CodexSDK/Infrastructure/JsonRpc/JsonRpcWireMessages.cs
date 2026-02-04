using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed record JsonRpcRequestWireMessage(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params,
    [property: JsonPropertyName("jsonrpc")] string? JsonRpc = null);

internal sealed record JsonRpcNotificationWireMessage(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params,
    [property: JsonPropertyName("jsonrpc")] string? JsonRpc = null);

internal sealed record JsonRpcResponseWireMessage(
    [property: JsonPropertyName("id")] JsonElement Id,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] JsonRpcError? Error,
    [property: JsonPropertyName("jsonrpc")] string? JsonRpc = null);

