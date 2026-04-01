using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

/// <summary>
/// Represents the details of a failed turn when <c>turn/status</c> is <c>failed</c>.
/// </summary>
public sealed record class CodexTurnError(string Message, string? AdditionalDetails, JsonElement? CodexErrorInfo, JsonElement Raw)
{
    internal static CodexTurnError? Parse(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } obj)
        {
            return null;
        }

        var message = CodexAppServerClientJson.GetStringOrNull(obj, "message") ?? string.Empty;
        var additionalDetails = CodexAppServerClientJson.GetStringOrNull(obj, "additionalDetails");
        var codexErrorInfo = obj.TryGetProperty("codexErrorInfo", out var info) ? info.Clone() : (JsonElement?)null;

        return new CodexTurnError(message, additionalDetails, codexErrorInfo, obj.Clone());
    }
}
