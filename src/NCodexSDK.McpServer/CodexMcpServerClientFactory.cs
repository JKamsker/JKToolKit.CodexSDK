using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCodexSDK.Infrastructure.JsonRpc;
using NCodexSDK.Infrastructure.Stdio;

namespace NCodexSDK.McpServer;

internal sealed class CodexMcpServerClientFactory : ICodexMcpServerClientFactory
{
    private readonly IOptions<CodexMcpServerClientOptions> _options;
    private readonly StdioProcessFactory _stdioFactory;
    private readonly ILoggerFactory _loggerFactory;

    public CodexMcpServerClientFactory(
        IOptions<CodexMcpServerClientOptions> options,
        StdioProcessFactory stdioFactory,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _stdioFactory = stdioFactory;
        _loggerFactory = loggerFactory;
    }

    public async Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default)
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
            includeJsonRpcHeader: true,
            notificationBufferCapacity: options.NotificationBufferCapacity,
            serializerOptions: options.SerializerOptionsOverride,
            logger: _loggerFactory.CreateLogger<JsonRpcConnection>());

        var client = new CodexMcpServerClient(options, process, rpc);
        await client.CallAsync(
            "initialize",
            new
            {
                protocolVersion = "2025-06-18",
                clientInfo = new { name = options.ClientInfo.Name, title = options.ClientInfo.Title, version = options.ClientInfo.Version },
                capabilities = new { }
            },
            ct);
        await rpc.SendNotificationAsync("notifications/initialized", @params: null, ct);

        return client;
    }
}

