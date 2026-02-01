using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Public;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_Starts_AndInitializes_WhenEnabled()
    {
        await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
        {
            Launch = CodexLaunch.CodexOnPath().WithArgs("app-server")
        });
    }
}
