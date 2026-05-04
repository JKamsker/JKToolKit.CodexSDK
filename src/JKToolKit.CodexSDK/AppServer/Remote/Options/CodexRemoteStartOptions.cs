namespace JKToolKit.CodexSDK.AppServer.Remote;

/// <summary>
/// Options for starting a detached Codex app-server over SSH WebSocket transport.
/// </summary>
public sealed class CodexSshWebSocketAppServerOptions
{
    /// <summary>
    /// Gets or sets an optional registry identifier. A stable identifier is generated when omitted.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the SSH host, host alias, or host name.
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Gets or sets an optional explicit SSH config file passed with <c>-F</c>.
    /// </summary>
    public string? ConfigFile { get; set; }

    /// <summary>
    /// Gets or sets an optional identity file passed with <c>-i</c>.
    /// </summary>
    public string? IdentityFile { get; set; }

    /// <summary>
    /// Gets or sets an optional SSH port passed with <c>-p</c>.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets an optional SSH username passed with <c>-l</c>.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets an optional SSH password. The registry does not persist this value.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets an optional remote working directory before starting Codex.
    /// </summary>
    public string? RemoteWorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets an optional remote state directory for PID and log files.
    /// </summary>
    public string? RemoteStateDirectory { get; set; }

    /// <summary>
    /// Gets or sets the remote Codex executable.
    /// </summary>
    public string CodexExecutable { get; set; } = "codex";

    /// <summary>
    /// Gets or sets the SSH executable name.
    /// </summary>
    public string SshExecutable { get; set; } = "ssh";

    /// <summary>
    /// Gets or sets the sshpass executable name used when <see cref="Password"/> is set.
    /// </summary>
    public string SshpassExecutable { get; set; } = "sshpass";

    /// <summary>
    /// Gets or sets additional SSH arguments inserted before the host.
    /// </summary>
    public IReadOnlyList<string>? AdditionalSshArguments { get; set; }

    /// <summary>
    /// Gets or sets additional app-server arguments appended after the listen URL.
    /// </summary>
    public IReadOnlyList<string>? AdditionalAppServerArguments { get; set; }

    /// <summary>
    /// Gets or sets an optional bearer token used by the WebSocket client.
    /// </summary>
    public string? BearerToken { get; set; }
}

/// <summary>
/// Options for starting a detached Codex app-server in a new Docker container.
/// </summary>
public sealed class CodexDockerContainerWebSocketAppServerOptions
{
    /// <summary>
    /// Gets or sets an optional registry identifier. A stable identifier is generated when omitted.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the Docker image that contains the Codex CLI.
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Gets or sets an optional Docker container name.
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory inside the container.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets an optional Codex home directory inside the container.
    /// </summary>
    public string? CodexHome { get; set; }

    /// <summary>
    /// Gets or sets the app-server WebSocket port inside the container.
    /// </summary>
    public int ContainerPort { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the Codex executable inside the container.
    /// </summary>
    public string CodexExecutable { get; set; } = "codex";

    /// <summary>
    /// Gets or sets the Docker executable name.
    /// </summary>
    public string DockerExecutable { get; set; } = "docker";

    /// <summary>
    /// Gets or sets additional Docker run arguments inserted before the image.
    /// </summary>
    public IReadOnlyList<string>? AdditionalDockerRunArguments { get; set; }

    /// <summary>
    /// Gets or sets additional app-server arguments appended after the listen URL.
    /// </summary>
    public IReadOnlyList<string>? AdditionalAppServerArguments { get; set; }

    /// <summary>
    /// Gets or sets an optional bearer token used by the WebSocket client.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stop should remove the managed container.
    /// </summary>
    public bool RemoveContainerOnStop { get; set; } = true;
}

/// <summary>
/// Options for starting a detached Codex app-server inside an existing Docker container.
/// </summary>
public sealed class CodexDockerExecWebSocketAppServerOptions
{
    /// <summary>
    /// Gets or sets an optional registry identifier. A stable identifier is generated when omitted.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the existing Docker container name or ID.
    /// </summary>
    public required string Container { get; set; }

    /// <summary>
    /// Gets or sets the public WebSocket URI that can reach the container listener.
    /// </summary>
    public required Uri PublicUri { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory inside the container.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets an optional Codex home directory inside the container.
    /// </summary>
    public string? CodexHome { get; set; }

    /// <summary>
    /// Gets or sets an optional state directory inside the container.
    /// </summary>
    public string? StateDirectory { get; set; }

    /// <summary>
    /// Gets or sets the container port the app-server should listen on.
    /// </summary>
    public int ContainerPort { get; set; } = 4500;

    /// <summary>
    /// Gets or sets the Codex executable inside the container.
    /// </summary>
    public string CodexExecutable { get; set; } = "codex";

    /// <summary>
    /// Gets or sets the Docker executable name.
    /// </summary>
    public string DockerExecutable { get; set; } = "docker";

    /// <summary>
    /// Gets or sets additional Docker exec arguments inserted before the container.
    /// </summary>
    public IReadOnlyList<string>? AdditionalDockerExecArguments { get; set; }

    /// <summary>
    /// Gets or sets additional app-server arguments appended after the listen URL.
    /// </summary>
    public IReadOnlyList<string>? AdditionalAppServerArguments { get; set; }

    /// <summary>
    /// Gets or sets an optional bearer token used by the WebSocket client.
    /// </summary>
    public string? BearerToken { get; set; }
}
