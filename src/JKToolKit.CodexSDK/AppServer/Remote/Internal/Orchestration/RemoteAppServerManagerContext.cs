using System.Collections.Concurrent;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;
using JKToolKit.CodexSDK.Exec;

namespace JKToolKit.CodexSDK.AppServer.Remote.Internal;

internal sealed class RemoteAppServerManagerContext
{
    private readonly ConcurrentDictionary<string, RemoteAppServerSecrets> _secrets = new(StringComparer.Ordinal);

    public RemoteAppServerManagerContext(
        CodexRemoteAppServerManagerOptions options,
        IRemoteProcessRunner processRunner,
        IRemoteAppServerHealthProbe healthProbe,
        Func<CodexAppServerWebSocketOptions, CancellationToken, Task<CodexAppServerClient>> clientFactory)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Registry = options.Registry ?? new InMemoryCodexRemoteAppServerRegistry();
        ProcessRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        HealthProbe = healthProbe ?? throw new ArgumentNullException(nameof(healthProbe));
        ClientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public CodexRemoteAppServerManagerOptions Options { get; }

    public ICodexRemoteAppServerRegistry Registry { get; }

    public IRemoteProcessRunner ProcessRunner { get; }

    public IRemoteAppServerHealthProbe HealthProbe { get; }

    public Func<CodexAppServerWebSocketOptions, CancellationToken, Task<CodexAppServerClient>> ClientFactory { get; }

    public void RememberSecrets(string id, string? sshPassword, string? bearerToken)
    {
        if (sshPassword is null && bearerToken is null)
        {
            return;
        }

        _secrets[id] = new RemoteAppServerSecrets(sshPassword, bearerToken);
    }

    public RemoteAppServerSecrets GetSecrets(string id)
    {
        return _secrets.TryGetValue(id, out var secrets)
            ? secrets
            : new RemoteAppServerSecrets(null, null);
    }

    public async Task<CodexRemoteAppServerEntry> GetRequiredEntryAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await Registry.GetAsync(id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Remote app-server '{id}' is not registered.");
    }

    public async Task<RemoteProcessResult> RunRequiredAsync(
        CodexLaunch launch,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync(launch, timeout, ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Remote command failed with exit code {result.ExitCode}: {result.StandardError}");
        }

        return result;
    }

    public async Task WaitReadyAsync(Uri uri, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + Options.StartTimeout;
        while (DateTimeOffset.UtcNow <= deadline)
        {
            if (await HealthProbe.IsReadyAsync(uri, Options.HealthCheckTimeout, ct).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), ct).ConfigureAwait(false);
        }

        if (!await HealthProbe.IsReadyAsync(uri, Options.HealthCheckTimeout, ct).ConfigureAwait(false))
        {
            throw new TimeoutException($"Remote app-server did not become ready at {uri}.");
        }
    }
}

internal sealed record RemoteAppServerSecrets(string? SshPassword, string? BearerToken);
