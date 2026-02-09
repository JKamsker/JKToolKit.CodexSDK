using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed class JsonRpcProtocolException : Exception
{
    public JsonRpcProtocolException(string message) : base(message) { }
    public JsonRpcProtocolException(string message, Exception inner) : base(message, inner) { }
}

internal sealed class JsonRpcRemoteException : Exception
{
    public JsonRpcError Error { get; }

    public JsonRpcRemoteException(JsonRpcError error)
        : base($"{error.Code}: {error.Message}")
    {
        Error = error;
    }
}

