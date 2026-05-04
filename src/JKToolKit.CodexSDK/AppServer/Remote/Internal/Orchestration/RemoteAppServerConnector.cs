using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal sealed class RemoteAppServerConnector
{
    private readonly RemoteAppServerManagerContext _context;

    public RemoteAppServerConnector(RemoteAppServerManagerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<CodexRemoteAppServerAttachment> AttachAsync(
        string id,
        CodexRemoteAttachOptions? options,
        CancellationToken ct)
    {
        var entry = await _context.GetRequiredEntryAsync(id, ct).ConfigureAwait(false);
        var secrets = _context.GetSecrets(id);
        var token = options?.BearerToken ?? secrets.BearerToken ?? entry.BearerToken;
        var clientOptions = (options?.ClientOptions ?? _context.Options.ClientOptions).Clone();
        clientOptions.StartupTimeout = _context.Options.AttachTimeout;

        return entry.Kind == CodexRemoteAppServerKind.SshWebSocket
            ? await AttachSshAsync(entry, options?.SshPassword ?? secrets.SshPassword, token, clientOptions, ct)
                .ConfigureAwait(false)
            : await AttachDirectAsync(entry, token, clientOptions, ct).ConfigureAwait(false);
    }

    public async Task<bool> ProbeAsync(CodexRemoteAppServerEntry entry, CancellationToken ct)
    {
        if (entry.Kind != CodexRemoteAppServerKind.SshWebSocket)
        {
            return entry.WebSocketUri is not null &&
                await _context.HealthProbe.IsReadyAsync(entry.WebSocketUri, _context.Options.HealthCheckTimeout, ct)
                    .ConfigureAwait(false);
        }

        var info = entry.Ssh;
        if (info is null)
        {
            return false;
        }

        try
        {
            var localPort = LocalPortAllocator.GetFreeLoopbackPort();
            await using var tunnel = await _context.ProcessRunner.StartAsync(
                    RemoteLaunchFactory.SshTunnel(info, localPort, _context.GetSecrets(entry.Id).SshPassword),
                    ct)
                .ConfigureAwait(false);
            return await _context.HealthProbe.IsReadyAsync(
                    new Uri($"ws://127.0.0.1:{localPort}"),
                    _context.Options.HealthCheckTimeout,
                    ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private async Task<CodexRemoteAppServerAttachment> AttachDirectAsync(
        CodexRemoteAppServerEntry entry,
        string? bearerToken,
        CodexAppServerClientOptions clientOptions,
        CancellationToken ct)
    {
        var uri = entry.WebSocketUri ??
            throw new InvalidOperationException($"Remote app-server '{entry.Id}' has no WebSocket URI.");
        var client = await _context.ClientFactory(new CodexAppServerWebSocketOptions
        {
            Uri = uri,
            BearerToken = bearerToken,
            ClientOptions = clientOptions
        }, ct).ConfigureAwait(false);
        return new CodexRemoteAppServerAttachment(entry, uri, client, ownedTransport: null);
    }

    private async Task<CodexRemoteAppServerAttachment> AttachSshAsync(
        CodexRemoteAppServerEntry entry,
        string? sshPassword,
        string? bearerToken,
        CodexAppServerClientOptions clientOptions,
        CancellationToken ct)
    {
        var info = entry.Ssh ?? throw new InvalidOperationException($"SSH entry '{entry.Id}' is missing SSH details.");
        Exception? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var localPort = LocalPortAllocator.GetFreeLoopbackPort();
            var tunnel = await _context.ProcessRunner.StartAsync(
                    RemoteLaunchFactory.SshTunnel(info, localPort, sshPassword),
                    ct)
                .ConfigureAwait(false);
            var uri = new Uri($"ws://127.0.0.1:{localPort}");
            if (await _context.HealthProbe.IsReadyAsync(uri, _context.Options.HealthCheckTimeout, ct).ConfigureAwait(false))
            {
                var client = await _context.ClientFactory(new CodexAppServerWebSocketOptions
                {
                    Uri = uri,
                    BearerToken = bearerToken,
                    ClientOptions = clientOptions
                }, ct).ConfigureAwait(false);
                return new CodexRemoteAppServerAttachment(entry, uri, client, tunnel);
            }

            lastError = new TimeoutException($"SSH tunnel for '{entry.Id}' did not become ready on local port {localPort}.");
            await tunnel.DisposeAsync().ConfigureAwait(false);
        }

        throw lastError ?? new InvalidOperationException($"Unable to attach SSH remote app-server '{entry.Id}'.");
    }
}
