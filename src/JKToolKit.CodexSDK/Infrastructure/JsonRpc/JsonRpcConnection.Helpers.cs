using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed partial class JsonRpcConnection
{
    private void LogBogus(string message, Exception? ex = null)
    {
#if DEBUG
        _logger.LogWarning(ex, message);
#else
        _logger.LogTrace(ex, message);
#endif
    }

    private static JsonElement? TryCloneParams(JsonElement root)
    {
        if (!root.TryGetProperty(JsonFieldNames.Params, out var paramsProp) ||
            paramsProp.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        return paramsProp.Clone();
    }
}
