using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

internal sealed record class JsonRpcRequest
{
    public JsonRpcId Id { get; }
    public string Method { get; }
    public JsonElement? Params { get; }

    public JsonRpcRequest(JsonRpcId Id, string Method, JsonElement? Params)
    {
        this.Id = Id;
        this.Method = Method ?? throw new ArgumentNullException(nameof(Method));
        this.Params = Params;
    }
}

