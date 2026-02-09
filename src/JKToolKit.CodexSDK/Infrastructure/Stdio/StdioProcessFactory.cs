using Microsoft.Extensions.Logging;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal sealed class StdioProcessFactory
{
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<StdioProcessFactory> _logger;

    public StdioProcessFactory(
        ICodexPathProvider pathProvider,
        ILogger<StdioProcessFactory> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<StdioProcess> StartAsync(
        CodexLaunch launch,
        string? codexExecutablePathOverride,
        TimeSpan? startupTimeout,
        TimeSpan? shutdownTimeout,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(launch);

        var resolvedFileName = string.IsNullOrWhiteSpace(launch.FileName)
            ? _pathProvider.GetCodexExecutablePath(codexExecutablePathOverride)
            : launch.FileName!;

        var options = new ProcessLaunchOptions
        {
            ResolvedFileName = resolvedFileName,
            Arguments = launch.Arguments,
            WorkingDirectory = launch.WorkingDirectory,
            Environment = launch.Environment,
            StartupTimeout = startupTimeout ?? TimeSpan.FromSeconds(30),
            ShutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(5)
        };

        _logger.LogDebug(
            "Starting stdio process: {FileName} {Args}",
            options.ResolvedFileName,
            string.Join(" ", options.Arguments));

        return StdioProcess.StartAsync(options, _logger, ct);
    }
}

