using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer;

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
        var launch = ApplyCodexHome(options.Launch, options.CodexHomeDirectory);

        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            _stdioFactory,
            _loggerFactory,
            launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            options.SerializerOptionsOverride,
            includeJsonRpcHeader: false,
            ct);

        var client = new CodexAppServerClient(
            options,
            process,
            rpc,
            _loggerFactory.CreateLogger<CodexAppServerClient>());

        await client.InitializeAsync(options.DefaultClientInfo, ct);

        return client;
    }

    private static CodexLaunch ApplyCodexHome(CodexLaunch launch, string? codexHomeDirectory)
    {
        if (string.IsNullOrWhiteSpace(codexHomeDirectory))
        {
            return launch;
        }

        return launch.WithEnvironment("CODEX_HOME", codexHomeDirectory);
    }
}
