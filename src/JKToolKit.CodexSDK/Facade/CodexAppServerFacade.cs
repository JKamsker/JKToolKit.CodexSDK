using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>
/// Facade for the <c>codex app-server</c> mode.
/// </summary>
public sealed class CodexAppServerFacade
{
    private readonly ICodexAppServerClientFactory _factory;

    /// <summary>
    /// Creates a new facade over an existing <see cref="ICodexAppServerClientFactory"/>.
    /// </summary>
    /// <param name="factory">The underlying app-server client factory.</param>
    public CodexAppServerFacade(ICodexAppServerClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <summary>
    /// Starts a new <see cref="CodexAppServerClient"/>.
    /// </summary>
    public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default) =>
        _factory.StartAsync(ct);
}

