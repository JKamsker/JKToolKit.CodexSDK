namespace NCodexSDK.AppServer;

public interface ICodexAppServerClientFactory
{
    Task<CodexAppServerClient> StartAsync(CancellationToken ct = default);
}

