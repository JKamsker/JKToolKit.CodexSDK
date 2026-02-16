using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Internal;

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
        var data = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
            ? ex.Error.Data.Value.GetRawText()
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(data))
        {
            data = CodexDiagnosticsSanitizer.Sanitize(data, maxChars: 2000);
        }

        var haystack = msg + "\n" + data;

        var mentionsReadOnlyAccess =
            haystack.Contains("readOnlyAccess", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("read_only_access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("readonly_access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("readonlyaccess", StringComparison.OrdinalIgnoreCase);

        var mentionsUnknownFieldIndicator =
            haystack.Contains("unknown field", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("unrecognized field", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("unknown property", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("unexpected property", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("additional properties", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("was unexpected", StringComparison.OrdinalIgnoreCase);

        var mentionsSandboxPolicyAccessField =
            haystack.Contains("sandboxPolicy.access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("sandbox_policy.access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("/sandboxPolicy/access", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("/sandbox_policy/access", StringComparison.OrdinalIgnoreCase);

        if (mentionsUnknownFieldIndicator && mentionsSandboxPolicyAccessField)
        {
            return true;
        }

        if (!mentionsReadOnlyAccess)
        {
            return false;
        }

        // Heuristic for servers that report unknown fields as JSON-pointer-like paths.
        var mentionsPointerLikePath =
            haystack.Contains("/sandboxPolicy", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("/sandbox_policy", StringComparison.OrdinalIgnoreCase);

        return mentionsUnknownFieldIndicator || mentionsPointerLikePath;
    }
}

