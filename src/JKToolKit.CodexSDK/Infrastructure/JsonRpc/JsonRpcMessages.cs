using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal readonly record struct JsonRpcId(JsonElement Value)
{
    public JsonValueKind Kind => Value.ValueKind;

    public override string ToString() => Value.ToString();

    public static JsonRpcId FromNumber(long value)
    {
        using var doc = JsonDocument.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new JsonRpcId(doc.RootElement.Clone());
    }

    public static string ToKey(JsonElement idElement) => idElement.ValueKind switch
    {
        JsonValueKind.String => idElement.GetString() ?? string.Empty,
        JsonValueKind.Number => idElement.GetRawText(),
        _ => idElement.GetRawText()
    };
}

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

