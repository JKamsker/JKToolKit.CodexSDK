using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCodexSDK.Infrastructure.JsonRpc;
using NCodexSDK.Infrastructure.Stdio;

namespace NCodexSDK.AppServer;

internal sealed class CodexAppServerClientFactory : ICodexAppServerClientFactory
{
    private readonly IOptions<CodexAppServerClientOptions> _options;
    private readonly StdioProcessFactory _stdioFactory;
    private readonly ILoggerFactory _loggerFactory;

    public CodexAppServerClientFactory(
        IOptions<CodexAppServerClientOptions> options,
        StdioProcessFactory stdioFactory,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _stdioFactory = stdioFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task<CodexAppServerClient> StartAsync(CancellationToken ct = default)
    {
        var options = _options.Value;

        var process = await _stdioFactory.StartAsync(
            options.Launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            ct);

        var rpc = new JsonRpcConnection(
            reader: process.Stdout,
            writer: process.Stdin,
            includeJsonRpcHeader: false,
            notificationBufferCapacity: options.NotificationBufferCapacity,
            serializerOptions: options.SerializerOptionsOverride,
            logger: _loggerFactory.CreateLogger<JsonRpcConnection>());

        var client = new CodexAppServerClient(
            options,
            process,
            rpc,
            _loggerFactory.CreateLogger<CodexAppServerClient>());

        await client.InitializeAsync(options.DefaultClientInfo, ct);

        return client;
    }
}
