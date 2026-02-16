using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerReadOnlyAccessOverridesSupport
{
    // -1 = rejected, 0 = unknown, 1 = supported
    public int Value;

    internal static bool ShouldMarkRejected(JsonRpcRemoteException ex)
    {
        if (ex.Error is null)
        {
            return false;
        }

        var msg = ex.Error.Message ?? string.Empty;
        var data = ex.Error.Data is { ValueKind: not System.Text.Json.JsonValueKind.Null and not System.Text.Json.JsonValueKind.Undefined }
            ? ex.Error.Data.Value.GetRawText()
            : string.Empty;

        var haystack = msg + "\n" + data;

        var mentionsReadOnlyAccess =
            haystack.Contains("readOnlyAccess", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("read_only_access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("readonly_access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("readonlyaccess", StringComparison.OrdinalIgnoreCase);

        if (mentionsReadOnlyAccess)
        {
            return true;
        }

        // Heuristic for servers that report unknown fields as JSON-pointer-like paths.
        var mentionsSandboxPolicy =
            haystack.Contains("sandboxPolicy", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("sandbox_policy", StringComparison.OrdinalIgnoreCase);

        return mentionsSandboxPolicy && haystack.Contains("access", StringComparison.OrdinalIgnoreCase);
    }
}

