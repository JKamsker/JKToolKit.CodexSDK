using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

internal sealed record class JsonRpcNotification
{
    public string Method { get; }
    public JsonElement? Params { get; }

    public JsonRpcNotification(string Method, JsonElement? Params)
    {
        this.Method = Method ?? throw new ArgumentNullException(nameof(Method));
        this.Params = Params;
    }
}

