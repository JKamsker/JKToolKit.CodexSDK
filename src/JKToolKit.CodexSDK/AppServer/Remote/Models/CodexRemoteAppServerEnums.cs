namespace JKToolKit.CodexSDK.AppServer.Remote;

/// <summary>
/// Identifies the kind of SDK-managed remote Codex app-server process.
/// </summary>
public enum CodexRemoteAppServerKind
{
    /// <summary>
    /// A Codex app-server started over SSH and reached through a local SSH tunnel.
    /// </summary>
    SshWebSocket,

    /// <summary>
    /// A Codex app-server running as the main process in a SDK-managed Docker container.
    /// </summary>
    DockerContainerWebSocket,

    /// <summary>
    /// A Codex app-server started inside an existing Docker container.
    /// </summary>
    DockerExecWebSocket
}

/// <summary>
/// Describes the observed lifecycle state of a SDK-managed remote Codex app-server process.
/// </summary>
public enum CodexRemoteAppServerStatus
{
    /// <summary>
    /// The process has not been probed yet.
    /// </summary>
    Unknown,

    /// <summary>
    /// The process responded to a readiness probe or was just started successfully.
    /// </summary>
    Running,

    /// <summary>
    /// The registry entry exists, but the process did not respond to a readiness probe.
    /// </summary>
    Stale,

    /// <summary>
    /// The process was stopped by the SDK.
    /// </summary>
    Stopped
}
