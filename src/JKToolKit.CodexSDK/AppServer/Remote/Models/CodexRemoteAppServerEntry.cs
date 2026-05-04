namespace JKToolKit.CodexSDK.AppServer.Remote;

/// <summary>
/// Registry entry for a SDK-managed remote Codex app-server process.
/// </summary>
public sealed record class CodexRemoteAppServerEntry
{
    /// <summary>
    /// Gets the stable SDK registry identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets an optional human-readable name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the remote process kind.
    /// </summary>
    public required CodexRemoteAppServerKind Kind { get; init; }

    /// <summary>
    /// Gets the last observed process status.
    /// </summary>
    public CodexRemoteAppServerStatus Status { get; init; } = CodexRemoteAppServerStatus.Unknown;

    /// <summary>
    /// Gets a directly reachable WebSocket URI when one exists.
    /// </summary>
    public Uri? WebSocketUri { get; init; }

    /// <summary>
    /// Gets an optional bearer token used for the WebSocket handshake.
    /// </summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// Gets SSH-specific registry details.
    /// </summary>
    public CodexRemoteSshAppServerInfo? Ssh { get; init; }

    /// <summary>
    /// Gets Docker-specific registry details.
    /// </summary>
    public CodexRemoteDockerAppServerInfo? Docker { get; init; }

    /// <summary>
    /// Gets when the entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets when the entry was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// SSH-specific details for a SDK-managed remote app-server.
/// </summary>
public sealed record class CodexRemoteSshAppServerInfo
{
    /// <summary>
    /// Gets the SSH host, host alias, or host name.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Gets an optional SSH config file path.
    /// </summary>
    public string? ConfigFile { get; init; }

    /// <summary>
    /// Gets an optional SSH identity file path.
    /// </summary>
    public string? IdentityFile { get; init; }

    /// <summary>
    /// Gets an optional SSH port.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Gets an optional SSH username.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the SSH executable used by the SDK.
    /// </summary>
    public string SshExecutable { get; init; } = "ssh";

    /// <summary>
    /// Gets the sshpass executable used when a password is supplied at runtime.
    /// </summary>
    public string SshpassExecutable { get; init; } = "sshpass";

    /// <summary>
    /// Gets additional SSH arguments inserted before the host.
    /// </summary>
    public IReadOnlyList<string>? AdditionalSshArguments { get; init; }

    /// <summary>
    /// Gets the remote working directory used when the app-server was started.
    /// </summary>
    public string? RemoteWorkingDirectory { get; init; }

    /// <summary>
    /// Gets the remote state directory containing PID and log files.
    /// </summary>
    public required string RemoteStateDirectory { get; init; }

    /// <summary>
    /// Gets the remote PID file path.
    /// </summary>
    public required string RemotePidFile { get; init; }

    /// <summary>
    /// Gets the remote log file path.
    /// </summary>
    public required string RemoteLogFile { get; init; }

    /// <summary>
    /// Gets the remote process identifier when startup reported one.
    /// </summary>
    public int? RemoteProcessId { get; init; }

    /// <summary>
    /// Gets the remote loopback WebSocket port.
    /// </summary>
    public required int RemotePort { get; init; }
}
