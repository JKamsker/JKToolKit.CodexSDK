using System.Text.Json;

namespace NCodexSDK.Infrastructure.JsonRpc;

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

internal sealed record JsonRpcRequest(JsonRpcId Id, string Method, JsonElement? Params);
internal sealed record JsonRpcResponse(JsonRpcId Id, JsonElement? Result, JsonRpcError? Error);
internal sealed record JsonRpcNotification(string Method, JsonElement? Params);

internal sealed record JsonRpcError(int Code, string Message, JsonElement? Data = null);

