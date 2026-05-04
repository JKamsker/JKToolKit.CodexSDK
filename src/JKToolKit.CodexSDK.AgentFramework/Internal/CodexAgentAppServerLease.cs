using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class CodexAgentAppServerLease : IAsyncDisposable
{
    private readonly CodexSdk? _sdk;
    private readonly IAsyncDisposable? _clientOwner;

    public CodexAgentAppServerLease(
        CodexAppServerClient client,
        CodexSdk? sdk,
        IAsyncDisposable? clientOwner = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        Client = client;
        _sdk = sdk;
        _clientOwner = clientOwner;
    }

    public CodexAppServerClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_clientOwner is null)
            {
                await Client.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await _clientOwner.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            if (_sdk is not null)
            {
                await _sdk.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
