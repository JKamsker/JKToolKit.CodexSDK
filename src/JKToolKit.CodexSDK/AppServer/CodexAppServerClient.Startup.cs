using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Opens the configured Codex app-server endpoint and returns an initialized client.
    /// </summary>
    public static async Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerFactory = NullLoggerFactory.Instance;
        var stdioFactory = CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory);
        return await StartAsync(options, loggerFactory, stdioFactory, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Attaches to an already-running Codex app-server WebSocket listener.
    /// </summary>
    public static Task<CodexAppServerClient> ConnectWebSocketAsync(
        CodexAppServerWebSocketOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Uri is null)
        {
            throw new ArgumentException("WebSocket URI is required.", nameof(options));
        }

        var clientOptions = options.ClientOptions.Clone();
        clientOptions.Endpoint = new CodexAppServerWebSocketEndpoint(options.Uri, options.BearerToken);
        return StartAsync(clientOptions, ct);
    }

    internal static async Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        ILoggerFactory loggerFactory,
        StdioProcessFactory stdioFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(stdioFactory);

        var endpoint = options.Endpoint ?? new CodexAppServerStdioEndpoint(options.Launch);
        return endpoint switch
        {
            CodexAppServerStdioEndpoint stdio => await StartStdioAsync(options, stdio.Launch, loggerFactory, stdioFactory, ct)
                .ConfigureAwait(false),
            CodexAppServerWebSocketEndpoint ws => await StartWebSocketAsync(options, ws, loggerFactory, ct)
                .ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported app-server endpoint type '{endpoint.GetType().FullName}'.")
        };
    }

    internal static async Task<CodexAppServerClient> CreateInitializedAsync(
        CodexAppServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger logger,
        JsonSerializerOptions serializerOptions,
        CancellationToken ct) =>
        await CreateInitializedAsync(
            options,
            new StdioAppServerLifetime(process),
            rpc,
            logger,
            serializerOptions,
            ct).ConfigureAwait(false);

    internal static async Task<CodexAppServerClient> CreateInitializedAsync(
        CodexAppServerClientOptions options,
        IAppServerLifetime lifetime,
        IJsonRpcConnection rpc,
        ILogger logger,
        JsonSerializerOptions serializerOptions,
        CancellationToken ct)
    {
        var client = new CodexAppServerClient(options, lifetime, rpc, logger, serializerOptions);

        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        handshakeCts.CancelAfter(options.StartupTimeout);

        try
        {
            await client.InitializeAsync(options.DefaultClientInfo, handshakeCts.Token).ConfigureAwait(false);
            return client;
        }
        catch
        {
            try { await client.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
            throw;
        }
    }

    internal static JsonSerializerOptions CreateDefaultSerializerOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    private static async Task<CodexAppServerClient> StartStdioAsync(
        CodexAppServerClientOptions options,
        CodexLaunch launch,
        ILoggerFactory loggerFactory,
        StdioProcessFactory stdioFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<CodexAppServerClient>();
        var serializerOptions = options.SerializerOptionsOverride ?? CreateDefaultSerializerOptions();
        var launchWithCodexHome = ApplyCodexHome(launch, options.CodexHomeDirectory);
        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            stdioFactory,
            loggerFactory,
            launchWithCodexHome,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            serializerOptions,
            includeJsonRpcHeader: false,
            ct);

        return await CreateInitializedAsync(options, process, rpc, logger, serializerOptions, ct).ConfigureAwait(false);
    }

    private static async Task<CodexAppServerClient> StartWebSocketAsync(
        CodexAppServerClientOptions options,
        CodexAppServerWebSocketEndpoint endpoint,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<CodexAppServerClient>();
        var serializerOptions = options.SerializerOptionsOverride ?? CreateDefaultSerializerOptions();
        var transport = await WebSocketJsonRpcMessageTransport.ConnectAsync(
            endpoint.Uri,
            endpoint.BearerToken,
            options.StartupTimeout,
            loggerFactory.CreateLogger<WebSocketJsonRpcMessageTransport>(),
            ct).ConfigureAwait(false);

        var rpc = new JsonRpcConnection(
            transport,
            includeJsonRpcHeader: false,
            notificationBufferCapacity: options.NotificationBufferCapacity,
            serializerOptions: serializerOptions,
            logger: loggerFactory.CreateLogger<JsonRpcConnection>());
        var lifetime = new WebSocketAppServerLifetime(transport);

        return await CreateInitializedAsync(options, lifetime, rpc, logger, serializerOptions, ct).ConfigureAwait(false);
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
