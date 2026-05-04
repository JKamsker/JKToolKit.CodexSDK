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
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("SSH host cannot be empty or whitespace.", nameof(host));
        }

        var command = string.IsNullOrWhiteSpace(remoteWorkingDirectory)
            ? "exec codex app-server"
            : $"cd {ShellQuote(remoteWorkingDirectory)} && exec codex app-server";

        return CodexLaunch
            .FromFileName("ssh")
            .WithArgs("-T", host, "bash", "-lc", command);
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
}
