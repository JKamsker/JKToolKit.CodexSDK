namespace JKToolKit.CodexSDK.AppServer.Internal;

internal interface ICodexAppServerClientOptionsFactory
{
    Task<CodexAppServerClient> StartAsync(
        Action<CodexAppServerClientOptions> configure,
        CancellationToken ct = default);
}
