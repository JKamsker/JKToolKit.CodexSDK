namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal interface IJsonRpcMessageTransport : IAsyncDisposable
{
    Task Completion { get; }

    Task SendAsync(string message, CancellationToken ct);

    IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct);
}
