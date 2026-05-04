using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Infrastructure.Stdio;

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
        CancellationToken ct) =>
        await CodexAppServerClient.StartAsync(options, _loggerFactory, _stdioFactory, ct).ConfigureAwait(false);
}
