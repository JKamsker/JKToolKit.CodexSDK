using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerMcpManagementE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_CanReloadMcpServers_AndListMcpServerStatus()
    {
        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions());

        await client.ReloadMcpServersAsync();

        var page = await client.ListMcpServerStatusAsync(new McpServerStatusListOptions());

        page.Raw.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
        page.Servers.Should().NotBeNull();
    }
}

