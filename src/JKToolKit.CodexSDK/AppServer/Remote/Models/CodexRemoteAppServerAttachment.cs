using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Remote;

/// <summary>
/// Represents an active attachment to a registered remote Codex app-server.
/// </summary>
public sealed class CodexRemoteAppServerAttachment : IAsyncDisposable
{
    private readonly IAsyncDisposable? _ownedTransport;
    private int _disposed;

    internal CodexRemoteAppServerAttachment(
        CodexRemoteAppServerEntry entry,
        Uri endpointUri,
        CodexAppServerClient client,
        IAsyncDisposable? ownedTransport)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        EndpointUri = endpointUri ?? throw new ArgumentNullException(nameof(endpointUri));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        _ownedTransport = ownedTransport;
    }

    /// <summary>
    /// Gets the registry entry used for this attachment.
    /// </summary>
    public CodexRemoteAppServerEntry Entry { get; }

    /// <summary>
    /// Gets the concrete WebSocket endpoint used by this attachment.
    /// </summary>
    public Uri EndpointUri { get; }

    /// <summary>
    /// Gets the initialized app-server client.
    /// </summary>
    public CodexAppServerClient Client { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await Client.DisposeAsync().ConfigureAwait(false);
        if (_ownedTransport is not null)
        {
            await _ownedTransport.DisposeAsync().ConfigureAwait(false);
        }
    }
}
