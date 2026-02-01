namespace JKToolKit.CodexSDK.AppServer;

public interface ICodexAppServerClientFactory
{
    Task<CodexAppServerClient> StartAsync(CancellationToken ct = default);
}

