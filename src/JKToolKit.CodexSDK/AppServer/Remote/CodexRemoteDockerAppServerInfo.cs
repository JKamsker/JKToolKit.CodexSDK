namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Docker-specific details for a SDK-managed remote app-server.
/// </summary>
public sealed record class CodexRemoteDockerAppServerInfo
{
    /// <summary>
    /// Gets the Docker executable used by the SDK.
    /// </summary>
    public string DockerExecutable { get; init; } = "docker";

    /// <summary>
    /// Gets the Docker image used for managed-container mode.
    /// </summary>
    public string? Image { get; init; }

    /// <summary>
    /// Gets the Docker container name.
    /// </summary>
    public required string ContainerName { get; init; }

    /// <summary>
    /// Gets the Docker container identifier when known.
    /// </summary>
    public string? ContainerId { get; init; }

    /// <summary>
    /// Gets an optional working directory inside the container.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Gets an optional Codex home directory inside the container.
    /// </summary>
    public string? CodexHome { get; init; }

    /// <summary>
    /// Gets the app-server WebSocket port inside the container.
    /// </summary>
    public int ContainerPort { get; init; } = 4500;

    /// <summary>
    /// Gets the host port published to loopback for managed-container mode.
    /// </summary>
    public int? HostPort { get; init; }

    /// <summary>
    /// Gets the state directory inside the container for existing-container mode.
    /// </summary>
    public string? StateDirectory { get; init; }

    /// <summary>
    /// Gets the PID file inside the container for existing-container mode.
    /// </summary>
    public string? PidFile { get; init; }

    /// <summary>
    /// Gets the log file inside the container for existing-container mode.
    /// </summary>
    public string? LogFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether stop should remove the managed container.
    /// </summary>
    public bool RemoveContainerOnStop { get; init; }
}
