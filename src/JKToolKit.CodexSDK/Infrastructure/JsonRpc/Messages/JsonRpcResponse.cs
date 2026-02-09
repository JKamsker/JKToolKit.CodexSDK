using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

internal sealed record class JsonRpcResponse
{
    public JsonRpcId Id { get; }
    public JsonElement? Result { get; }
    public JsonRpcError? Error { get; }

    public JsonRpcResponse(JsonRpcId Id, JsonElement? Result, JsonRpcError? Error)
    {
        this.Id = Id;
        this.Result = Result;
        this.Error = Error;
    }
}

