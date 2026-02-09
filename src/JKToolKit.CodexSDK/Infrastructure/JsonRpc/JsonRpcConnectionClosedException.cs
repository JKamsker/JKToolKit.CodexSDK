namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed class JsonRpcConnectionClosedException : IOException
{
    public JsonRpcConnectionClosedException() { }

    public JsonRpcConnectionClosedException(string message) : base(message) { }

    public JsonRpcConnectionClosedException(string message, Exception innerException) : base(message, innerException) { }
}
