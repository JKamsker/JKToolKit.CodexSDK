using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Resiliency.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// A resilient wrapper around <see cref="CodexAppServerClient"/> that can auto-restart the underlying
/// <c>codex app-server</c> subprocess and (optionally) retry operations based on a user-provided policy.
/// </summary>
public sealed class ResilientCodexAppServerClient : IAsyncDisposable
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
    /// Returns a notification stream. When enabled, the stream continues across restarts.
    /// </summary>
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _executor.Notifications(ct);

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(CodexAppServerOperationKind.Call, (c, token) => c.CallAsync(method, @params, token), ct);

    /// <summary>
    /// Starts a new thread.
    /// </summary>
    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(CodexAppServerOperationKind.StartThread, (c, token) => c.StartThreadAsync(options, token), ct);

    /// <summary>
    /// Resumes an existing thread by ID.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(threadId, token), ct);

    /// <summary>
    /// Resumes an existing thread using the provided options.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(options, token), ct);

    /// <summary>
    /// Starts a new turn within the specified thread.
    /// </summary>
    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default) =>
        _executor.ExecuteWithPolicyAsync(CodexAppServerOperationKind.StartTurn, (c, token) => c.StartTurnAsync(threadId, options, token), ct);

    /// <summary>
    /// Forces a restart of the underlying app-server subprocess.
    /// </summary>
    public Task RestartAsync(CancellationToken ct = default) => _connection.RestartAsync(ct);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
