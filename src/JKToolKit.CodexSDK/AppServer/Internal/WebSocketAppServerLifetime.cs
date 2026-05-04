using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class WebSocketAppServerLifetime : IAppServerLifetime
{
    private readonly IJsonRpcMessageTransport _transport;

    public WebSocketAppServerLifetime(IJsonRpcMessageTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public Task Completion => _transport.Completion;

    public int? ProcessId => null;

    public int? ExitCode => null;

    public IReadOnlyList<string> DiagnosticTail => Array.Empty<string>();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
