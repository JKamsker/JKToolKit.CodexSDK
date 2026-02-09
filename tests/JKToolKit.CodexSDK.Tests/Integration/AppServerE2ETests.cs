using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_Starts_AndInitializes_WhenEnabled()
    {
        await using var sdk = CodexSdk.Create(builder =>
            builder.ConfigureAppServer(o => o.Launch = CodexLaunch.CodexOnPath().WithArgs("app-server")));

        await using var client = await sdk.AppServer.StartAsync();
    }
}
