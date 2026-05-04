using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal sealed class RemoteAppServerStarter
{
    private static readonly Regex DockerPortRegex = new(@"(?<host>[^:\s]+):(?<port>\d+)", RegexOptions.Compiled);
    private readonly RemoteAppServerManagerContext _context;

    public RemoteAppServerStarter(RemoteAppServerManagerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CodexRemoteAppServerEntry> StartSshWebSocketAsync(
        CodexSshWebSocketAppServerOptions options,
        CancellationToken ct)
    {
        RemoteAppServerValidation.ValidateSshStart(options);
        var id = NormalizeId(options.Id);
        var command = BuildSshStartCommand(options, id, _context.Options.StartTimeout);
        var result = await _context.RunRequiredAsync(
                RemoteLaunchFactory.SshCommand(options, command),
                _context.Options.StartTimeout,
                ct)
            .ConfigureAwait(false);
        var metadata = RemoteStartMetadata.Parse(result.StandardOutput);
        var now = DateTimeOffset.UtcNow;
        var entry = new CodexRemoteAppServerEntry
        {
            Id = id,
            Name = options.Name,
            Kind = CodexRemoteAppServerKind.SshWebSocket,
            Status = CodexRemoteAppServerStatus.Running,
            BearerToken = options.BearerToken,
            CreatedAt = now,
            UpdatedAt = now,
            Ssh = CreateSshInfo(options, metadata)
        };

        _context.RememberSecrets(id, options.Password, options.BearerToken);
        await _context.Registry.UpsertAsync(entry, ct).ConfigureAwait(false);
        return entry;
    }

    public async Task<CodexRemoteAppServerEntry> StartDockerContainerWebSocketAsync(
        CodexDockerContainerWebSocketAppServerOptions options,
        CancellationToken ct)
    {
        RemoteAppServerValidation.ValidateDockerContainerStart(options);
        var id = NormalizeId(options.Id);
        var containerName = string.IsNullOrWhiteSpace(options.ContainerName) ? id : options.ContainerName;
        var containerStarted = false;
        try
        {
            var run = await _context.RunRequiredAsync(
                    RemoteLaunchFactory.DockerRun(options, id, containerName),
                    _context.Options.StartTimeout,
                    ct)
                .ConfigureAwait(false);
            containerStarted = true;
            var portResult = await _context.RunRequiredAsync(
                    RemoteLaunchFactory.DockerPort(options.DockerExecutable, containerName!, options.ContainerPort),
                    _context.Options.StartTimeout,
                    ct)
                .ConfigureAwait(false);
            var hostPort = ParseDockerHostPort(portResult.StandardOutput);
            var uri = new Uri($"ws://127.0.0.1:{hostPort}");
            await _context.WaitReadyAsync(uri, ct).ConfigureAwait(false);

            var entry = CreateDockerContainerEntry(options, id, containerName!, run.StandardOutput.Trim(), hostPort, uri);
            _context.RememberSecrets(id, sshPassword: null, options.BearerToken);
            await _context.Registry.UpsertAsync(entry, ct).ConfigureAwait(false);
            return entry;
        }
        catch
        {
            if (containerStarted)
            {
                await TryRemoveContainerAsync(options.DockerExecutable, containerName!, ct).ConfigureAwait(false);
            }

            throw;
        }
    }

    public async Task<CodexRemoteAppServerEntry> StartDockerExecWebSocketAsync(
        CodexDockerExecWebSocketAppServerOptions options,
        CancellationToken ct)
    {
        RemoteAppServerValidation.ValidateDockerExecStart(options);
        var id = NormalizeId(options.Id);
        await _context.RunRequiredAsync(
                RemoteLaunchFactory.DockerExecStart(options, id, _context.Options.StartTimeout),
                _context.Options.StartTimeout,
                ct)
            .ConfigureAwait(false);
        var metadata = await PollDockerExecMetadataAsync(options, id, ct).ConfigureAwait(false);
        await _context.WaitReadyAsync(options.PublicUri, ct).ConfigureAwait(false);

        var entry = CreateDockerExecEntry(options, id, metadata);
        _context.RememberSecrets(id, sshPassword: null, options.BearerToken);
        await _context.Registry.UpsertAsync(entry, ct).ConfigureAwait(false);
        return entry;
    }

    private async Task<RemoteStartMetadata> PollDockerExecMetadataAsync(
        CodexDockerExecWebSocketAppServerOptions options,
        string id,
        CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + _context.Options.StartTimeout;
        RemoteProcessResult? last = null;
        while (true)
        {
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var perCallTimeout = remaining < TimeSpan.FromSeconds(5)
                ? remaining
                : TimeSpan.FromSeconds(5);
            last = await _context.ProcessRunner.RunAsync(
                    RemoteLaunchFactory.DockerExecReadMetadata(options, id),
                    perCallTimeout,
                    ct)
                .ConfigureAwait(false);
            if (last.ExitCode == 0 && last.StandardOutput.Contains("CODEXSDK_PID=", StringComparison.Ordinal))
            {
                return RemoteStartMetadata.Parse(last.StandardOutput);
            }

            remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var delay = remaining < TimeSpan.FromMilliseconds(200)
                ? remaining
                : TimeSpan.FromMilliseconds(200);
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }

        throw new TimeoutException($"Timed out waiting for Docker exec app-server metadata. Last stderr: {last?.StandardError}");
    }

    private static string BuildSshStartCommand(
        CodexSshWebSocketAppServerOptions options,
        string id,
        TimeSpan timeout)
    {
        var stateDir = string.IsNullOrWhiteSpace(options.RemoteStateDirectory)
            ? "\"${CODEX_HOME:-$HOME/.codex}/codexsdk-appservers\""
            : RemoteShell.Quote(options.RemoteStateDirectory);
        var appServer = RemoteShell.BuildAppServerCommand(
            options.CodexExecutable,
            "ws://127.0.0.1:0",
            options.AdditionalAppServerArguments);
        return RemoteShell.BuildDetachedStartScript(
            id,
            stateDir,
            "\"$state_dir/\"$id\".log\"",
            "\"$state_dir/\"$id\".pid\"",
            "ws://127\\.0\\.0\\.1:[0-9][0-9]*",
            appServer,
            timeout,
            options.RemoteWorkingDirectory);
    }

    private static CodexRemoteSshAppServerInfo CreateSshInfo(
        CodexSshWebSocketAppServerOptions options,
        RemoteStartMetadata metadata) =>
        new()
        {
            Host = options.Host,
            ConfigFile = options.ConfigFile,
            IdentityFile = options.IdentityFile,
            Port = options.Port,
            Username = options.Username,
            SshExecutable = options.SshExecutable,
            SshpassExecutable = options.SshpassExecutable,
            AdditionalSshArguments = options.AdditionalSshArguments,
            RemoteWorkingDirectory = options.RemoteWorkingDirectory,
            RemoteStateDirectory = metadata.StateDirectory,
            RemotePidFile = metadata.PidFile,
            RemoteLogFile = metadata.LogFile,
            RemoteProcessId = metadata.ProcessId,
            RemotePort = metadata.Uri.Port
        };

    private static CodexRemoteAppServerEntry CreateDockerContainerEntry(
        CodexDockerContainerWebSocketAppServerOptions options,
        string id,
        string containerName,
        string containerId,
        int hostPort,
        Uri uri)
    {
        var now = DateTimeOffset.UtcNow;
        return new CodexRemoteAppServerEntry
        {
            Id = id,
            Name = options.Name,
            Kind = CodexRemoteAppServerKind.DockerContainerWebSocket,
            Status = CodexRemoteAppServerStatus.Running,
            WebSocketUri = uri,
            BearerToken = options.BearerToken,
            CreatedAt = now,
            UpdatedAt = now,
            Docker = new CodexRemoteDockerAppServerInfo
            {
                DockerExecutable = options.DockerExecutable,
                Image = options.Image,
                ContainerName = containerName,
                ContainerId = containerId,
                WorkingDirectory = options.WorkingDirectory,
                CodexHome = options.CodexHome,
                ContainerPort = options.ContainerPort,
                HostPort = hostPort,
                RemoveContainerOnStop = options.RemoveContainerOnStop
            }
        };
    }

    private static CodexRemoteAppServerEntry CreateDockerExecEntry(
        CodexDockerExecWebSocketAppServerOptions options,
        string id,
        RemoteStartMetadata metadata)
    {
        var now = DateTimeOffset.UtcNow;
        return new CodexRemoteAppServerEntry
        {
            Id = id,
            Name = options.Name,
            Kind = CodexRemoteAppServerKind.DockerExecWebSocket,
            Status = CodexRemoteAppServerStatus.Running,
            WebSocketUri = options.PublicUri,
            BearerToken = options.BearerToken,
            CreatedAt = now,
            UpdatedAt = now,
            Docker = new CodexRemoteDockerAppServerInfo
            {
                DockerExecutable = options.DockerExecutable,
                ContainerName = options.Container,
                WorkingDirectory = options.WorkingDirectory,
                CodexHome = options.CodexHome,
                ContainerPort = options.ContainerPort,
                StateDirectory = metadata.StateDirectory,
                PidFile = metadata.PidFile,
                LogFile = metadata.LogFile
            }
        };
    }

    private static int ParseDockerHostPort(string output)
    {
        var match = DockerPortRegex.Match(output);
        return match.Success && int.TryParse(match.Groups["port"].Value, out var port)
            ? port
            : throw new InvalidOperationException($"Unable to parse Docker host port from: {output}");
    }

    private static string NormalizeId(string? id) =>
        string.IsNullOrWhiteSpace(id) ? $"codexsdk-{Guid.NewGuid():N}" : id;

    private async Task TryRemoveContainerAsync(string dockerExecutable, string containerName, CancellationToken ct)
    {
        try
        {
            await _context.ProcessRunner.RunAsync(
                    RemoteLaunchFactory.DockerRemove(new CodexRemoteDockerAppServerInfo
                    {
                        DockerExecutable = dockerExecutable,
                        ContainerName = containerName
                    }),
                    _context.Options.StopTimeout,
                    ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // Best effort cleanup after a failed managed-container start.
        }
    }
}
