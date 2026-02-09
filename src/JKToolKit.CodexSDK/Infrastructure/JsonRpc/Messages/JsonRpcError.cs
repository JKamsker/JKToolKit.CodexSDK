using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

internal sealed record class JsonRpcError
{
    public int Code { get; }
    public string Message { get; }
    public JsonElement? Data { get; }

    public JsonRpcError(int Code, string Message, JsonElement? Data = null)
    {
        this.Code = Code;
        this.Message = Message ?? throw new ArgumentNullException(nameof(Message));
        this.Data = Data;
    }
}

