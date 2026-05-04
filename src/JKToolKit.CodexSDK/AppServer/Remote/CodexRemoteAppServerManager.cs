using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Manages SDK-started detached remote Codex app-server processes.
/// </summary>
public sealed class CodexRemoteAppServerManager
{
    private readonly RemoteAppServerManagerContext _context;
    private readonly RemoteAppServerStarter _starter;
    private readonly RemoteAppServerConnector _connector;
    private readonly RemoteAppServerLifecycle _lifecycle;

    /// <summary>
    /// Initializes a manager with an in-memory registry.
    /// </summary>
    public CodexRemoteAppServerManager()
        : this(new CodexRemoteAppServerManagerOptions())
    {
    }

    /// <summary>
    /// Initializes a manager with the provided registry.
    /// </summary>
    /// <param name="registry">The registry to use.</param>
    public CodexRemoteAppServerManager(ICodexRemoteAppServerRegistry registry)
        : this(new CodexRemoteAppServerManagerOptions { Registry = registry })
    {
    }

    /// <summary>
    /// Initializes a manager with explicit options.
    /// </summary>
    /// <param name="options">The manager options.</param>
    public CodexRemoteAppServerManager(CodexRemoteAppServerManagerOptions options)
        : this(
            options,
            new RemoteProcessRunner(NullLogger.Instance),
            new RemoteAppServerHealthProbe(),
            CodexAppServerClient.ConnectWebSocketAsync)
    {
    }

    internal CodexRemoteAppServerManager(
        CodexRemoteAppServerManagerOptions options,
        IRemoteProcessRunner processRunner,
        IRemoteAppServerHealthProbe healthProbe,
        Func<CodexAppServerWebSocketOptions, CancellationToken, Task<CodexAppServerClient>> clientFactory)
    {
        _context = new RemoteAppServerManagerContext(options, processRunner, healthProbe, clientFactory);
        _starter = new RemoteAppServerStarter(_context);
        _connector = new RemoteAppServerConnector(_context);
        _lifecycle = new RemoteAppServerLifecycle(_context, _connector);
    }

    /// <summary>
    /// Starts a detached SSH WebSocket app-server and registers it.
    /// </summary>
    public Task<CodexRemoteAppServerEntry> StartSshWebSocketAsync(
        CodexSshWebSocketAppServerOptions options,
        CancellationToken ct = default) =>
        _starter.StartSshWebSocketAsync(options, ct);

    /// <summary>
    /// Starts a detached WebSocket app-server in a new SDK-managed Docker container and registers it.
    /// </summary>
    public Task<CodexRemoteAppServerEntry> StartDockerContainerWebSocketAsync(
        CodexDockerContainerWebSocketAppServerOptions options,
        CancellationToken ct = default) =>
        _starter.StartDockerContainerWebSocketAsync(options, ct);

    /// <summary>
    /// Starts a detached WebSocket app-server inside an existing Docker container and registers it.
    /// </summary>
    public Task<CodexRemoteAppServerEntry> StartDockerExecWebSocketAsync(
        CodexDockerExecWebSocketAppServerOptions options,
        CancellationToken ct = default) =>
        _starter.StartDockerExecWebSocketAsync(options, ct);

    /// <summary>
    /// Attaches to a registered remote app-server.
    /// </summary>
    public Task<CodexRemoteAppServerAttachment> AttachAsync(
        string id,
        CodexRemoteAttachOptions? options = null,
        CancellationToken ct = default) =>
        _connector.AttachAsync(id, options, ct);

    /// <summary>
    /// Lists registered remote app-servers.
    /// </summary>
    public Task<IReadOnlyList<CodexRemoteAppServerEntry>> ListAsync(
        bool refresh = false,
        CancellationToken ct = default) =>
        _lifecycle.ListAsync(refresh, ct);

    /// <summary>
    /// Stops a registered remote app-server.
    /// </summary>
    public Task<CodexRemoteAppServerEntry> StopAsync(
        string id,
        CodexRemoteStopOptions? options = null,
        CancellationToken ct = default) =>
        _lifecycle.StopAsync(id, options, ct);

    /// <summary>
    /// Removes a registered remote app-server entry without stopping the process.
    /// </summary>
    public Task<bool> RemoveAsync(string id, CancellationToken ct = default) =>
        _context.Registry.RemoveAsync(id, ct);
}
