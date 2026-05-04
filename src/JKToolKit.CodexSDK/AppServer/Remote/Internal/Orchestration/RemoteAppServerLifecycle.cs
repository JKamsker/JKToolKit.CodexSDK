using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal sealed class RemoteAppServerLifecycle
{
    private readonly RemoteAppServerManagerContext _context;
    private readonly RemoteAppServerConnector _connector;

    public RemoteAppServerLifecycle(
        RemoteAppServerManagerContext context,
        RemoteAppServerConnector connector)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    public async Task<IReadOnlyList<CodexRemoteAppServerEntry>> ListAsync(bool refresh, CancellationToken ct)
    {
        var entries = await _context.Registry.ListAsync(ct).ConfigureAwait(false);
        if (!refresh)
        {
            return entries;
        }

        var refreshed = new List<CodexRemoteAppServerEntry>(entries.Count);
        foreach (var entry in entries)
        {
            refreshed.Add(await RefreshEntryAsync(entry, ct).ConfigureAwait(false));
        }

        return refreshed;
    }

    public async Task<CodexRemoteAppServerEntry> StopAsync(
        string id,
        CodexRemoteStopOptions? options,
        CancellationToken ct)
    {
        var entry = await _context.GetRequiredEntryAsync(id, ct).ConfigureAwait(false);
        switch (entry.Kind)
        {
            case CodexRemoteAppServerKind.SshWebSocket:
                await StopSshAsync(entry, options, ct).ConfigureAwait(false);
                break;
            case CodexRemoteAppServerKind.DockerContainerWebSocket:
                await StopDockerContainerAsync(entry, ct).ConfigureAwait(false);
                break;
            case CodexRemoteAppServerKind.DockerExecWebSocket:
                await StopDockerExecAsync(entry, ct).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, $"Unsupported remote app-server kind '{entry.Kind}'.");
        }

        var stopped = entry with { Status = CodexRemoteAppServerStatus.Stopped, UpdatedAt = DateTimeOffset.UtcNow };
        if (options?.RemoveFromRegistry == true)
        {
            await _context.Registry.RemoveAsync(id, ct).ConfigureAwait(false);
        }
        else
        {
            await _context.Registry.UpsertAsync(stopped, ct).ConfigureAwait(false);
        }

        return stopped;
    }

    private async Task<CodexRemoteAppServerEntry> RefreshEntryAsync(
        CodexRemoteAppServerEntry entry,
        CancellationToken ct)
    {
        var isReady = await _connector.ProbeAsync(entry, ct).ConfigureAwait(false);
        var refreshed = entry with
        {
            Status = isReady ? CodexRemoteAppServerStatus.Running : CodexRemoteAppServerStatus.Stale,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _context.Registry.UpsertAsync(refreshed, ct).ConfigureAwait(false);
        return refreshed;
    }

    private async Task StopSshAsync(
        CodexRemoteAppServerEntry entry,
        CodexRemoteStopOptions? options,
        CancellationToken ct)
    {
        var info = entry.Ssh ?? throw new InvalidOperationException($"SSH entry '{entry.Id}' is missing SSH details.");
        var password = options?.SshPassword ?? _context.GetSecrets(entry.Id).SshPassword;
        var script = $"if [ -f {RemoteShell.Quote(info.RemotePidFile)} ]; then " +
            $"kill \"$(cat {RemoteShell.Quote(info.RemotePidFile)})\" 2>/dev/null || true; " +
            $"rm -f {RemoteShell.Quote(info.RemotePidFile)}; fi";
        await _context.RunRequiredAsync(
                RemoteLaunchFactory.SshCommand(info, password, script),
                _context.Options.StopTimeout,
                ct)
            .ConfigureAwait(false);
    }

    private async Task StopDockerContainerAsync(CodexRemoteAppServerEntry entry, CancellationToken ct)
    {
        var docker = entry.Docker ?? throw new InvalidOperationException($"Docker entry '{entry.Id}' is missing Docker details.");
        var launch = docker.RemoveContainerOnStop
            ? RemoteLaunchFactory.DockerRemove(docker)
            : RemoteLaunchFactory.DockerStop(docker);
        await _context.RunRequiredAsync(launch, _context.Options.StopTimeout, ct)
            .ConfigureAwait(false);
    }

    private async Task StopDockerExecAsync(CodexRemoteAppServerEntry entry, CancellationToken ct)
    {
        var docker = entry.Docker ?? throw new InvalidOperationException($"Docker entry '{entry.Id}' is missing Docker details.");
        await _context.RunRequiredAsync(RemoteLaunchFactory.DockerExecStop(docker), _context.Options.StopTimeout, ct)
            .ConfigureAwait(false);
    }
}
