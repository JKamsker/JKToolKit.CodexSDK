using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Resiliency.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// A resilient wrapper around <see cref="CodexAppServerClient"/> that can auto-restart the underlying
/// <c>codex app-server</c> subprocess and (optionally) retry operations based on a user-provided policy.
/// </summary>
public sealed partial class ResilientCodexAppServerClient : IAsyncDisposable
{
    private readonly ResilientAppServerConnection _connection;
    private readonly ResilientAppServerExecutor _executor;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public CodexAppServerConnectionState State => _connection.State;

    /// <summary>
    /// Gets the last restart event, when available.
    /// </summary>
    public CodexAppServerRestartEvent? LastRestart => _connection.LastRestart;

    /// <summary>
    /// Gets the number of restarts performed during this client's lifetime.
    /// </summary>
    public int RestartCount => _connection.RestartCount;

    internal ResilientCodexAppServerClient(
        Func<CancellationToken, Task<ICodexAppServerClientAdapter>> startInner,
        CodexAppServerResilienceOptions options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(startInner);
        _connection = new ResilientAppServerConnection(startInner, options ?? throw new ArgumentNullException(nameof(options)), logger ?? throw new ArgumentNullException(nameof(logger)));
        _executor = new ResilientAppServerExecutor(_connection, options);
    }

    /// <summary>
    /// Starts a new resilient client using an underlying <see cref="ICodexAppServerClientFactory"/>.
    /// </summary>
    public static async Task<ResilientCodexAppServerClient> StartAsync(
        ICodexAppServerClientFactory factory,
        CodexAppServerResilienceOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var effectiveOptions = options ?? new CodexAppServerResilienceOptions();
        var lf = loggerFactory ?? NullLoggerFactory.Instance;
        var logger = lf.CreateLogger<ResilientCodexAppServerClient>();

        var client = new ResilientCodexAppServerClient(
            startInner: async c =>
            {
                var inner = await factory.StartAsync(c).ConfigureAwait(false);
                return new CodexAppServerClientAdapter(inner);
            },
            options: effectiveOptions,
            logger: logger);

        await client._connection.EnsureConnectedAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// Gets the initialize result payload, if <see cref="CodexAppServerClient.InitializeAsync"/> has completed on the active inner client.
    /// </summary>
    public AppServerInitializeResult? InitializeResult => _connection.InitializeResult;

    /// <summary>
    /// A task that completes when the resilient client is disposed or faulted.
    /// </summary>
    public Task ExitTask => _connection.ExitTask;

    /// <summary>
    /// Gets drop counters for bounded notification buffers from the active inner client.
    /// </summary>
    public AppServerNotificationDropStats NotificationDropStats => _connection.NotificationDropStats;

    /// <summary>
    /// Returns a notification stream. When enabled, the stream continues across restarts.
    /// </summary>
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _executor.Notifications(ct);

    /// <summary>
    /// Returns the raw JSON-RPC notification stream. When enabled, the stream continues across restarts
    /// without injecting local synthetic marker notifications.
    /// </summary>
    public IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct = default) =>
        _executor.NotificationsRaw(ct);

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Call, (c, token) => c.CallAsync(method, @params, token), ct);

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server and deserializes the <c>result</c> payload.
    /// </summary>
    public Task<TResult?> CallAsync<TResult>(
        string method,
        object? @params,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Call, (c, token) => c.CallAsync<TResult>(method, @params, serializerOptions, token), ct);

    private Task ExecuteAsync(
        CodexAppServerOperationKind kind,
        Func<ICodexAppServerClientAdapter, CancellationToken, Task> operation,
        CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync<object?>(
            kind,
            async (client, token) =>
            {
                await operation(client, token).ConfigureAwait(false);
                return null;
            },
            ct);

    private Task<TResult> ExecuteAsync<TResult>(
        CodexAppServerOperationKind kind,
        Func<ICodexAppServerClientAdapter, CancellationToken, Task<TResult>> operation,
        CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(kind, operation, ct);

    /// <summary>
    /// Forces a restart of the underlying app-server subprocess.
    /// </summary>
    public Task RestartAsync(CancellationToken ct = default) => _connection.RestartAsync(ct);

    internal Task EnsureConnectedAsync(CancellationToken ct = default) => _connection.EnsureConnectedAsync(ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
