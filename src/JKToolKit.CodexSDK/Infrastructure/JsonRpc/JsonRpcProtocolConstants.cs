namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal static class JsonRpcProtocolConstants
{
    public const string Version = "2.0";
    public const string RemoteErrorMessage = "Remote error";
}

// Keep error codes open to upstream extensions; public errors continue to carry an int.
internal static class JsonRpcErrorCodes
{
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int ServerError = -32000;

    // Codex app-server's overload response, used by the restart policy.
    public const int ServerOverloaded = -32001;
}
