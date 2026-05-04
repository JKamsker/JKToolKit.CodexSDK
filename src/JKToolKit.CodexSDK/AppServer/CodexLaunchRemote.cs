using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Convenience launch helpers for running Codex app-server through common remote stdio transports.
/// </summary>
public static class CodexLaunchRemote
{
    /// <summary>
    /// Creates an SSH launch that runs <c>codex app-server</c> on the remote host over clean stdio.
    /// </summary>
    /// <param name="host">The SSH host, alias, or user@host target.</param>
    /// <param name="remoteWorkingDirectory">Optional remote working directory.</param>
    /// <returns>A launch configuration for <see cref="CodexAppServerClientOptions.Launch"/>.</returns>
    public static CodexLaunch SshAppServer(string host, string? remoteWorkingDirectory = null)
    {
        return SshAppServer(new CodexSshAppServerOptions
        {
            Host = host,
            RemoteWorkingDirectory = remoteWorkingDirectory
        });
    }

    /// <summary>
    /// Creates an SSH launch that runs <c>codex app-server</c> on the remote host over clean stdio.
    /// </summary>
    /// <param name="options">SSH launch options.</param>
    /// <returns>A launch configuration for <see cref="CodexAppServerClientOptions.Launch"/>.</returns>
    public static CodexLaunch SshAppServer(CodexSshAppServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var sshArgs = BuildSshArguments(options);
        if (options.Password is null)
        {
            return CodexLaunch.FromFileName(options.SshExecutable).WithArgs(sshArgs.ToArray());
        }

        return CodexLaunch
            .FromFileName(options.SshpassExecutable)
            .WithArgs(["-e", options.SshExecutable, .. sshArgs])
            .WithEnvironment("SSHPASS", options.Password);
    }

    /// <summary>
    /// Creates a Docker launch that runs <c>codex app-server</c> inside an existing container over clean stdio.
    /// </summary>
    /// <param name="container">The Docker container name or ID.</param>
    /// <param name="workingDirectory">Optional container working directory.</param>
    /// <param name="codexHome">Optional Codex home directory inside the container.</param>
    /// <returns>A launch configuration for <see cref="CodexAppServerClientOptions.Launch"/>.</returns>
    public static CodexLaunch DockerAppServer(
        string container,
        string? workingDirectory = null,
        string? codexHome = null)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new ArgumentException("Container cannot be empty or whitespace.", nameof(container));
        }

        var args = new List<string> { "exec", "-i" };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            args.AddRange(["-w", workingDirectory]);
        }

        if (!string.IsNullOrWhiteSpace(codexHome))
        {
            args.AddRange(["-e", $"CODEX_HOME={codexHome}"]);
        }

        args.AddRange([container, "codex", "app-server"]);
        return CodexLaunch.FromFileName("docker").WithArgs(args.ToArray());
    }

    private static string ShellQuote(string value)
    {
        if (value.Length == 0)
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

    private static List<string> BuildSshArguments(CodexSshAppServerOptions options)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.ConfigFile))
        {
            args.AddRange(["-F", options.ConfigFile]);
        }

        args.Add("-T");

        if (!string.IsNullOrWhiteSpace(options.IdentityFile))
        {
            args.AddRange(["-i", options.IdentityFile]);
        }

        if (options.Port is not null)
        {
            args.AddRange(["-p", options.Port.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        }

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            args.AddRange(["-l", options.Username]);
        }

        if (options.AdditionalSshArguments is { Count: > 0 })
        {
            args.AddRange(options.AdditionalSshArguments);
        }

        args.Add(options.Host);
        args.AddRange(["bash", "-lc", BuildRemoteCommand(options.RemoteWorkingDirectory)]);
        return args;
    }

    private static string BuildRemoteCommand(string? remoteWorkingDirectory)
    {
        return string.IsNullOrWhiteSpace(remoteWorkingDirectory)
            ? "exec codex app-server"
            : $"cd {ShellQuote(remoteWorkingDirectory)} && exec codex app-server";
    }
}

/// <summary>
/// Options for launching <c>codex app-server</c> over SSH stdio.
/// </summary>
public sealed class CodexSshAppServerOptions
{
    /// <summary>
    /// Gets or sets the SSH host, host alias, or host name. OpenSSH config aliases are supported.
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
    /// Gets or sets an optional SSH password. Requires <c>sshpass</c>; passed through <c>SSHPASS</c>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets an optional remote working directory before starting Codex.
    /// </summary>
    public string? RemoteWorkingDirectory { get; set; }

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

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("SSH host cannot be empty or whitespace.", nameof(Host));
        if (string.IsNullOrWhiteSpace(SshExecutable))
            throw new ArgumentException("SSH executable cannot be empty or whitespace.", nameof(SshExecutable));
        if (Password is not null && string.IsNullOrWhiteSpace(SshpassExecutable))
            throw new ArgumentException("sshpass executable cannot be empty or whitespace when Password is set.", nameof(SshpassExecutable));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "SSH port must be between 1 and 65535.");
    }
}
