using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

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

    /// <summary>
    /// Starts a new <see cref="CodexAppServerClient"/> with per-start app-server option overrides.
    /// </summary>
    /// <param name="configure">Configuration applied to a cloned options snapshot before startup.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A started <see cref="CodexAppServerClient"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// The underlying app-server factory does not support per-start option overrides.
    /// </exception>
    public Task<CodexAppServerClient> StartAsync(
        Action<CodexAppServerClientOptions> configure,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configure);

        return _factory is ICodexAppServerClientOptionsFactory configurableFactory
            ? configurableFactory.StartAsync(configure, ct)
            : throw new NotSupportedException(
                "The configured app-server factory does not support per-start app-server options.");
    }
}

