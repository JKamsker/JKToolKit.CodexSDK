using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
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
    public async Task AppServerFacade_StartAsync_WithOptions_DelegatesToConfigurableFactory()
    {
        var factory = new ConfigurableFakeAppServerFactory();
        var facade = new CodexAppServerFacade(factory);

        using var cts = new CancellationTokenSource();
        var result = await facade.StartAsync(
            options =>
            {
                options.ExperimentalApi = true;
                options.CodexHomeDirectory = "codex-home";
            },
            cts.Token);

        result.Should().BeNull();
        factory.StartCalls.Should().ContainSingle().Which.Should().Be(cts.Token);
        factory.StartOptions.Should().ContainSingle().Which.Should().Match<CodexAppServerClientOptions>(
            options => options.ExperimentalApi && options.CodexHomeDirectory == "codex-home");
    }

    [Fact]
    public async Task AppServerFacade_StartAsync_WithOptions_ThrowsForNonConfigurableFactory()
    {
        var facade = new CodexAppServerFacade(new FakeAppServerFactory());

        var act = async () => await facade.StartAsync(_ => { });

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*per-start app-server options*");
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

    private sealed class ConfigurableFakeAppServerFactory :
        ICodexAppServerClientFactory,
        ICodexAppServerClientOptionsFactory
    {
        public List<CancellationToken> StartCalls { get; } = new();

        public List<CodexAppServerClientOptions> StartOptions { get; } = new();

        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default)
        {
            StartCalls.Add(ct);
            return Task.FromResult<CodexAppServerClient>(null!);
        }

        public Task<CodexAppServerClient> StartAsync(
            Action<CodexAppServerClientOptions> configure,
            CancellationToken ct = default)
        {
            var options = new CodexAppServerClientOptions();
            configure(options);
            StartOptions.Add(options);
            return StartAsync(ct);
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

