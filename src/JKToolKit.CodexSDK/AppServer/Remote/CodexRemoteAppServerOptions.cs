namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <see cref="CodexRemoteAppServerManager"/>.
/// </summary>
public sealed class CodexRemoteAppServerManagerOptions
{
    /// <summary>
    /// Gets or sets the registry used by the manager.
    /// </summary>
    public ICodexRemoteAppServerRegistry? Registry { get; set; }

    /// <summary>
    /// Gets or sets base client options used when attaching to WebSocket app-servers.
    /// </summary>
    public CodexAppServerClientOptions ClientOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the timeout for starting detached remote app-servers.
    /// </summary>
    public TimeSpan StartTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for attaching through direct WebSocket or SSH tunnel transports.
    /// </summary>
    public TimeSpan AttachTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for readiness probes.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the timeout for stop commands.
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Options for attaching to a registered remote app-server.
/// </summary>
public sealed class CodexRemoteAttachOptions
{
    /// <summary>
    /// Gets or sets base client options used for this attachment.
    /// </summary>
    public CodexAppServerClientOptions? ClientOptions { get; set; }

    /// <summary>
    /// Gets or sets an SSH password used at attach time. The registry does not persist this value.
    /// </summary>
    public string? SshPassword { get; set; }

    /// <summary>
    /// Gets or sets a bearer token used for the WebSocket handshake.
    /// </summary>
    public string? BearerToken { get; set; }
}

/// <summary>
/// Options for stopping a registered remote app-server.
/// </summary>
public sealed class CodexRemoteStopOptions
{
    /// <summary>
    /// Gets or sets an SSH password used for SSH stop commands.
    /// </summary>
    public string? SshPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stopped entry should also be removed from the registry.
    /// </summary>
    public bool RemoveFromRegistry { get; set; }
}
