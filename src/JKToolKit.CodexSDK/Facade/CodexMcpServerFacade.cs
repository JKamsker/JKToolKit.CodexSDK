using JKToolKit.CodexSDK.McpServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>
/// Facade for the <c>codex mcp-server</c> mode.
/// </summary>
public sealed class CodexMcpServerFacade
{
    private readonly ICodexMcpServerClientFactory _factory;

    /// <summary>
    /// Creates a new facade over an existing <see cref="ICodexMcpServerClientFactory"/>.
    /// </summary>
    /// <param name="factory">The underlying mcp-server client factory.</param>
    public CodexMcpServerFacade(ICodexMcpServerClientFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <summary>
    /// Starts a new <see cref="CodexMcpServerClient"/>.
    /// </summary>
    public Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default) =>
        _factory.StartAsync(ct);
}

