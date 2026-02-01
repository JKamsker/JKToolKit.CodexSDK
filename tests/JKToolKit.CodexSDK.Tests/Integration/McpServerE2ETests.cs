using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Public;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class McpServerE2ETests
{
    [CodexE2EFact]
    public async Task McpServer_Starts_AndListsTools_WhenEnabled()
    {
        await using var client = await CodexMcpServerClient.StartAsync(new CodexMcpServerClientOptions
        {
            Launch = CodexLaunch.CodexOnPath().WithArgs("mcp-server")
        });

        _ = await client.ListToolsAsync();
    }
}
