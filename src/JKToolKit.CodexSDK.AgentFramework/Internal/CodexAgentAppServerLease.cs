using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AgentFramework.Internal;

internal sealed class CodexAgentAppServerLease : IAsyncDisposable
{
    private readonly CodexSdk? _sdk;

    public CodexAgentAppServerLease(CodexAppServerClient client, CodexSdk? sdk)
    {
        ArgumentNullException.ThrowIfNull(client);

        Client = client;
        _sdk = sdk;
    }

    public CodexAppServerClient Client { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Client.DisposeAsync().ConfigureAwait(false);
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
