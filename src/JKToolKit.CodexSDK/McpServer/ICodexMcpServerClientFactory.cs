namespace JKToolKit.CodexSDK.McpServer;

public interface ICodexMcpServerClientFactory
{
    Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default);
}

