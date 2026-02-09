namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Factory for starting resilient app-server clients.
/// </summary>
public interface ICodexResilientAppServerClientFactory
{
    /// <summary>
    /// Starts a resilient app-server client (launches <c>codex app-server</c> and performs initialization).
    /// </summary>
    Task<ResilientCodexAppServerClient> StartAsync(CancellationToken ct = default);
}

