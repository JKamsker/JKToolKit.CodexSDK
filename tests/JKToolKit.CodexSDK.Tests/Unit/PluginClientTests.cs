using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class PluginClientTests
{
    [Fact]
    public async Task ListPluginsAsync_ParsesMarketplacesAndFeaturedIds()
    {
        using var doc = JsonDocument.Parse(
            """{"featuredPluginIds":["plug-1"],"marketplaceLoadErrors":[{"marketplacePath":"C:\\market","message":"failed"}],"marketplaces":[{"name":"official","path":"C:\\market","plugins":[{"id":"plug-1","name":"Plugin One","installed":true,"enabled":true,"authPolicy":"ON_INSTALL","installPolicy":"TRUSTED_ONLY","source":{"type":"curated"}}]}]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions { ForceRemoteSync = true });

        result.FeaturedPluginIds.Should().Equal("plug-1");
        result.MarketplaceLoadErrors.Should().ContainSingle();
        result.Marketplaces.Should().ContainSingle();
        result.Marketplaces[0].Plugins.Should().ContainSingle();
        result.Marketplaces[0].Plugins[0].Id.Should().Be("plug-1");
        rpc.LastMethod.Should().Be("plugin/list");
    }

    [Fact]
    public async Task ReadPluginAsync_ParsesDetailSurface()
    {
        using var doc = JsonDocument.Parse(
            """{"plugin":{"description":"desc","marketplaceName":"official","marketplacePath":"C:\\market","mcpServers":["server-a"],"skills":[{"name":"skill-a","path":"skills\\a","enabled":true,"description":"desc"}],"apps":[{"id":"app-a","name":"App A","needsAuth":true}],"summary":{"id":"plug-1","name":"Plugin One","installed":true,"enabled":true,"authPolicy":"ON_USE","installPolicy":"TRUSTED_ONLY"}}}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = "C:\\market",
            PluginName = "plugin-one"
        });

        result.Plugin.Summary.Id.Should().Be("plug-1");
        result.Plugin.McpServers.Should().Equal("server-a");
        result.Plugin.Skills.Should().ContainSingle();
        result.Plugin.Apps.Should().ContainSingle();
        rpc.LastMethod.Should().Be("plugin/read");
    }

    [Fact]
    public async Task InstallPluginAsync_ParsesAuthPolicyAndAppsNeedingAuth()
    {
        using var doc = JsonDocument.Parse(
            """{"authPolicy":"ON_INSTALL","appsNeedingAuth":[{"id":"app-a","name":"App A","needsAuth":true}]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.InstallPluginAsync(new PluginInstallOptions
        {
            MarketplacePath = "C:\\market",
            PluginName = "plugin-one"
        });

        result.AuthPolicy.Should().Be("ON_INSTALL");
        result.AppsNeedingAuth.Should().ContainSingle();
        rpc.LastMethod.Should().Be("plugin/install");
    }

    [Fact]
    public async Task UninstallPluginAsync_SendsExpectedParams()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        await client.UninstallPluginAsync(new PluginUninstallOptions { PluginId = "plug-1", ForceRemoteSync = true });

        rpc.LastMethod.Should().Be("plugin/uninstall");
        JsonSerializer.Serialize(rpc.LastParams, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Contain("\"pluginId\":\"plug-1\"");
    }

    private static CodexAppServerClient CreateClient(RecordingRpc rpc) =>
        new(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => 0;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
