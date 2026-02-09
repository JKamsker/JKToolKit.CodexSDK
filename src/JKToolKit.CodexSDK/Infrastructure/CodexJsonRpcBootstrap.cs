using System.Text.Json;
using Microsoft.Extensions.Logging;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.Infrastructure;

internal static class CodexJsonRpcBootstrap
{
    public static StdioProcessFactory CreateDefaultStdioFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var fileSystem = new RealFileSystem();
        var pathProviderLogger = loggerFactory.CreateLogger<DefaultCodexPathProvider>();
        ICodexPathProvider pathProvider = new DefaultCodexPathProvider(fileSystem, pathProviderLogger);

        return new StdioProcessFactory(pathProvider, loggerFactory.CreateLogger<StdioProcessFactory>());
    }

    public static async Task<(StdioProcess Process, JsonRpcConnection Rpc)> StartAsync(
        StdioProcessFactory stdioFactory,
        ILoggerFactory loggerFactory,
        CodexLaunch launch,
        string? codexExecutablePath,
        TimeSpan startupTimeout,
        TimeSpan shutdownTimeout,
        int notificationBufferCapacity,
        JsonSerializerOptions? serializerOptionsOverride,
        bool includeJsonRpcHeader,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stdioFactory);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var process = await stdioFactory.StartAsync(
            launch,
            codexExecutablePath,
            startupTimeout,
            shutdownTimeout,
            ct);

        var rpc = new JsonRpcConnection(
            reader: process.Stdout,
            writer: process.Stdin,
            includeJsonRpcHeader: includeJsonRpcHeader,
            notificationBufferCapacity: notificationBufferCapacity,
            serializerOptions: serializerOptionsOverride,
            logger: loggerFactory.CreateLogger<JsonRpcConnection>());

        return (process, rpc);
    }
}

