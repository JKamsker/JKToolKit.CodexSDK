using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;

namespace JKToolKit.CodexSDK.McpServer;

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

        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            _stdioFactory,
            _loggerFactory,
            options.Launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            options.SerializerOptionsOverride,
            includeJsonRpcHeader: true,
            ct);

        var client = new CodexMcpServerClient(options, process, rpc);
        await client.InitializeAsync(ct);

        return client;
    }
}

