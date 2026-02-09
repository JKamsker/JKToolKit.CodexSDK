namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Factory for creating <see cref="CodexMcpServerClient"/> instances.
/// </summary>
public interface ICodexMcpServerClientFactory
{
    /// <summary>
    /// Starts a new MCP server client.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A started <see cref="CodexMcpServerClient"/>.</returns>
    Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default);
}

