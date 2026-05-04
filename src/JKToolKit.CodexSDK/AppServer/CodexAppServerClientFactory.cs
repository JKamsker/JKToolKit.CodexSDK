using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer;

internal sealed class CodexAppServerClientFactory : ICodexAppServerClientFactory, ICodexAppServerClientOptionsFactory
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

    public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default)
    {
        return StartAsync(_options.Value, ct);
    }

    public Task<CodexAppServerClient> StartAsync(
        Action<CodexAppServerClientOptions> configure,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = _options.Value.Clone();
        configure(options);
        return StartAsync(options, ct);
    }

    private async Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        CancellationToken ct)
    {
        var launch = ApplyCodexHome(options.Launch, options.CodexHomeDirectory);
        var serializerOptions = options.SerializerOptionsOverride ?? CodexAppServerClient.CreateDefaultSerializerOptions();
        var logger = _loggerFactory.CreateLogger<CodexAppServerClient>();

        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            _stdioFactory,
            _loggerFactory,
            launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            serializerOptions,
            includeJsonRpcHeader: false,
            ct);

        return await CodexAppServerClient.CreateInitializedAsync(
            options,
            process,
            rpc,
            logger,
            serializerOptions,
            ct).ConfigureAwait(false);
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
