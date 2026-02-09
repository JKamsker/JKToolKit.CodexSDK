using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class McpServerE2ETests
{
    [CodexE2EFact]
    public async Task McpServer_Starts_AndListsTools_WhenEnabled()
    {
        await using var sdk = CodexSdk.Create(builder =>
            builder.ConfigureMcpServer(o => o.Launch = CodexLaunch.CodexOnPath().WithArgs("mcp-server")));

        await using var client = await sdk.McpServer.StartAsync();

        _ = await client.ListToolsAsync();
    }
}
