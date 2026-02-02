using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Integration;

public class AddCodexSdkResolutionTests
{
    [Fact]
    public async Task AddCodexSdk_ResolvesCodexSdk()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddCodexSdk();

        await using var provider = services.BuildServiceProvider();
        var sdk = provider.GetRequiredService<CodexSdk>();

        sdk.Exec.Should().NotBeNull();
        sdk.AppServer.Should().NotBeNull();
        sdk.McpServer.Should().NotBeNull();
    }
}
