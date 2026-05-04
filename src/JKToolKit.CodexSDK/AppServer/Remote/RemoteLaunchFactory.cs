using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer;

internal static class RemoteLaunchFactory
{
    public static CodexLaunch SshCommand(
        CodexSshWebSocketAppServerOptions options,
        string remoteCommand)
    {
        var args = BuildSshPrefix(
            options.ConfigFile,
            includeNoTty: true,
            options.IdentityFile,
            options.Port,
            options.Username,
            options.AdditionalSshArguments);
        args.Add(options.Host);
        args.AddRange(["bash", "-lc", remoteCommand]);

        return WithSshPassword(
            CodexLaunch.FromFileName(options.SshExecutable).WithArgs(args.ToArray()),
            options.SshpassExecutable,
            options.Password);
    }

    public static CodexLaunch SshCommand(
        CodexRemoteSshAppServerInfo info,
        string? password,
        string remoteCommand)
    {
        var args = BuildSshPrefix(
            info.ConfigFile,
            includeNoTty: true,
            info.IdentityFile,
            info.Port,
            info.Username,
            info.AdditionalSshArguments);
        args.Add(info.Host);
        args.AddRange(["bash", "-lc", remoteCommand]);

        return WithSshPassword(
            CodexLaunch.FromFileName(info.SshExecutable).WithArgs(args.ToArray()),
            info.SshpassExecutable,
            password);
    }

    public static CodexLaunch SshTunnel(
        CodexRemoteSshAppServerInfo info,
        int localPort,
        string? password)
    {
        var args = BuildSshPrefix(
            info.ConfigFile,
            includeNoTty: false,
            info.IdentityFile,
            info.Port,
            info.Username,
            info.AdditionalSshArguments);
        args.AddRange(["-N", "-L", $"127.0.0.1:{localPort}:127.0.0.1:{info.RemotePort}", info.Host]);

        return WithSshPassword(
            CodexLaunch.FromFileName(info.SshExecutable).WithArgs(args.ToArray()),
            info.SshpassExecutable,
            password);
    }

    public static CodexLaunch DockerRun(CodexDockerContainerWebSocketAppServerOptions options, string id, string containerName)
    {
        var args = new List<string>
        {
            "run",
            "-d",
            "--name",
            containerName,
            "--label",
            "jktoolkit.codexsdk.remote=true",
            "--label",
            $"jktoolkit.codexsdk.id={id}",
            "-p",
            $"127.0.0.1::{options.ContainerPort}"
        };

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            args.AddRange(["-w", options.WorkingDirectory]);
        }

        if (!string.IsNullOrWhiteSpace(options.CodexHome))
        {
            args.AddRange(["-e", $"CODEX_HOME={options.CodexHome}"]);
        }

        if (options.AdditionalDockerRunArguments is { Count: > 0 })
        {
            args.AddRange(options.AdditionalDockerRunArguments);
        }

        args.Add(options.Image);
        args.Add(options.CodexExecutable);
        args.AddRange(["app-server", "--listen", $"ws://0.0.0.0:{options.ContainerPort}"]);
        if (options.AdditionalAppServerArguments is { Count: > 0 })
        {
            args.AddRange(options.AdditionalAppServerArguments);
        }

        return CodexLaunch.FromFileName(options.DockerExecutable).WithArgs(args.ToArray());
    }

    public static CodexLaunch DockerPort(string dockerExecutable, string containerName, int containerPort) =>
        CodexLaunch.FromFileName(dockerExecutable).WithArgs("port", containerName, $"{containerPort}/tcp");

    public static CodexLaunch DockerExecStart(CodexDockerExecWebSocketAppServerOptions options, string id, TimeSpan timeout)
    {
        var stateDir = string.IsNullOrWhiteSpace(options.StateDirectory)
            ? "\"${CODEX_HOME:-/tmp}/codexsdk-appservers\""
            : RemoteShell.Quote(options.StateDirectory);
        var command = RemoteShell.BuildAppServerCommand(
            options.CodexExecutable,
            $"ws://0.0.0.0:{options.ContainerPort}",
            options.AdditionalAppServerArguments);
        var scriptLines = new List<string>
        {
            "set -eu",
            $"id={RemoteShell.Quote(id)}",
            $"state_dir={stateDir}",
            "mkdir -p \"$state_dir\"",
            "pid_file=\"$state_dir/$id.pid\"",
            "log_file=\"$state_dir/$id.log\"",
            "printf '%s\\n' \"$$\" > \"$pid_file\""
        };
        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            scriptLines.Insert(4, $"cd {RemoteShell.Quote(options.WorkingDirectory)}");
        }

        scriptLines.Add($"exec {command} >/dev/null 2>\"$log_file\"");
        var script = string.Join("\n", scriptLines);

        var args = BuildDockerExecPrefix(options);
        args.AddRange(["-d", options.Container, "sh", "-lc", script]);
        return CodexLaunch.FromFileName(options.DockerExecutable).WithArgs(args.ToArray());
    }

    public static CodexLaunch DockerExecReadMetadata(CodexDockerExecWebSocketAppServerOptions options, string id)
    {
        var stateAssignment = string.IsNullOrWhiteSpace(options.StateDirectory)
            ? "state_dir=\"${CODEX_HOME:-/tmp}/codexsdk-appservers\""
            : $"state_dir={RemoteShell.Quote(options.StateDirectory)}";
        var metadataScript = string.Join(
            "; ",
            stateAssignment,
            $"pid_file=\"$state_dir/{id}.pid\"",
            $"log_file=\"$state_dir/{id}.log\"",
            "cat \"$log_file\" >/dev/null 2>&1 || true",
            "cat \"$pid_file\" >/dev/null 2>&1",
            $"printf 'CODEXSDK_ID=%s\\nCODEXSDK_PID=%s\\nCODEXSDK_URI=ws://0.0.0.0:{options.ContainerPort}\\nCODEXSDK_STATE_DIR=%s\\nCODEXSDK_PID_FILE=%s\\nCODEXSDK_LOG_FILE=%s\\n' " +
            $"{RemoteShell.Quote(id)} \"$(cat \"$pid_file\")\" \"$state_dir\" \"$pid_file\" \"$log_file\"");

        var args = BuildDockerExecPrefix(options);
        args.AddRange([options.Container, "sh", "-lc", metadataScript]);
        return CodexLaunch.FromFileName(options.DockerExecutable).WithArgs(args.ToArray());
    }

    public static CodexLaunch DockerExecStop(CodexRemoteDockerAppServerInfo info)
    {
        var pidFile = info.PidFile ?? throw new InvalidOperationException("Docker exec entry has no PID file.");
        var script = $"if [ -f {RemoteShell.Quote(pidFile)} ]; then kill \"$(cat {RemoteShell.Quote(pidFile)})\" 2>/dev/null || true; rm -f {RemoteShell.Quote(pidFile)}; fi";
        return CodexLaunch.FromFileName(info.DockerExecutable)
            .WithArgs("exec", info.ContainerName, "sh", "-lc", script);
    }

    public static CodexLaunch DockerRemove(CodexRemoteDockerAppServerInfo info) =>
        CodexLaunch.FromFileName(info.DockerExecutable).WithArgs("rm", "-f", info.ContainerName);

    public static CodexLaunch DockerStop(CodexRemoteDockerAppServerInfo info) =>
        CodexLaunch.FromFileName(info.DockerExecutable).WithArgs("stop", info.ContainerName);

    private static List<string> BuildSshPrefix(
        string? configFile,
        bool includeNoTty,
        string? identityFile,
        int? port,
        string? username,
        IReadOnlyList<string>? additionalArgs)
    {
        var args = new List<string>();
        if (!string.IsNullOrWhiteSpace(configFile))
            args.AddRange(["-F", configFile]);
        if (includeNoTty)
            args.Add("-T");
        if (!string.IsNullOrWhiteSpace(identityFile))
            args.AddRange(["-i", identityFile]);
        if (port is not null)
            args.AddRange(["-p", port.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (!string.IsNullOrWhiteSpace(username))
            args.AddRange(["-l", username]);
        if (additionalArgs is { Count: > 0 })
            args.AddRange(additionalArgs);
        return args;
    }

    private static CodexLaunch WithSshPassword(CodexLaunch sshLaunch, string sshpassExecutable, string? password)
    {
        if (password is null)
        {
            return sshLaunch;
        }

        return CodexLaunch.FromFileName(sshpassExecutable)
            .WithArgs(["-e", sshLaunch.FileName!, .. sshLaunch.Arguments])
            .WithEnvironment("SSHPASS", password);
    }

    private static List<string> BuildDockerExecPrefix(CodexDockerExecWebSocketAppServerOptions options)
    {
        var args = new List<string> { "exec" };
        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            args.AddRange(["-w", options.WorkingDirectory]);
        }

        if (!string.IsNullOrWhiteSpace(options.CodexHome))
        {
            args.AddRange(["-e", $"CODEX_HOME={options.CodexHome}"]);
        }

        if (options.AdditionalDockerExecArguments is { Count: > 0 })
        {
            args.AddRange(options.AdditionalDockerExecArguments);
        }

        return args;
    }
}
