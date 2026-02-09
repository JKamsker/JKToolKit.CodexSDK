using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.McpServer;
using FluentAssertions;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class CodexServerFacadesTests
{
    [Fact]
    public async Task AppServerFacade_StartAsync_DelegatesToFactory()
    {
        var factory = new FakeAppServerFactory();
        var facade = new CodexAppServerFacade(factory);

        using var cts = new CancellationTokenSource();
        var result = await facade.StartAsync(cts.Token);

        result.Should().BeNull();
        factory.StartCalls.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    [Fact]
    public async Task McpServerFacade_StartAsync_DelegatesToFactory()
    {
        var factory = new FakeMcpServerFactory();
        var facade = new CodexMcpServerFacade(factory);

        using var cts = new CancellationTokenSource();
        var result = await facade.StartAsync(cts.Token);

        result.Should().BeNull();
        factory.StartCalls.Should().ContainSingle().Which.Should().Be(cts.Token);
    }

    private sealed class FakeAppServerFactory : ICodexAppServerClientFactory
    {
        public List<CancellationToken> StartCalls { get; } = new();

        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default)
        {
            StartCalls.Add(ct);
            return Task.FromResult<CodexAppServerClient>(null!);
        }
    }

    private sealed class FakeMcpServerFactory : ICodexMcpServerClientFactory
    {
        public List<CancellationToken> StartCalls { get; } = new();

        public Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default)
        {
            StartCalls.Add(ct);
            return Task.FromResult<CodexMcpServerClient>(null!);
        }
    }
}

