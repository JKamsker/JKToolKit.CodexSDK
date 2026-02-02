using JKToolKit.CodexSDK.Tests.TestHelpers;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Integration;

public class CodexSdkE2ETests
{
    [CodexE2EFact]
    public async Task CodexSdk_CanStart_AppServer_And_McpServer()
    {
        await using var sdk = CodexSdk.Create();

        await using var app = await sdk.AppServer.StartAsync();
        app.Should().NotBeNull();

        await using var mcp = await sdk.McpServer.StartAsync();
        mcp.Should().NotBeNull();
    }
}

