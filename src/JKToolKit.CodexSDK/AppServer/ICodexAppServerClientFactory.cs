namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Factory for creating <see cref="CodexAppServerClient"/> instances.
/// </summary>
public interface ICodexAppServerClientFactory
{
    /// <summary>
    /// Starts a new app-server client.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A started <see cref="CodexAppServerClient"/>.</returns>
    Task<CodexAppServerClient> StartAsync(CancellationToken ct = default);
}

