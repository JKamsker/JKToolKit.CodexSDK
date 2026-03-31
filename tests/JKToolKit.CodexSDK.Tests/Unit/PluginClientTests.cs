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
    public async Task ListPluginsAsync_ParsesTypedMarketplacePluginAndSourceMetadata()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "featuredPluginIds": ["plug-1"],
              "marketplaceLoadErrors": [{ "marketplacePath": "C:\\market", "message": "failed" }],
              "marketplaces": [
                {
                  "name": "official",
                  "path": "C:\\market",
                  "interface": { "displayName": "Official Marketplace" },
                  "plugins": [
                    {
                      "id": "plug-1",
                      "name": "Plugin One",
                      "installed": true,
                      "enabled": true,
                      "authPolicy": "ON_INSTALL",
                      "installPolicy": "AVAILABLE",
                      "interface": {
                        "displayName": "Plugin One Display",
                        "shortDescription": "Short",
                        "longDescription": "Long",
                        "category": "developer-tools",
                        "developerName": "Example Corp",
                        "brandColor": "#123456",
                        "defaultPrompt": ["Open a pull request"],
                        "capabilities": ["issues", "pull-requests"],
                        "screenshots": ["https://example.test/shot.png"]
                      },
                      "source": {
                        "type": "curated",
                        "channel": "official"
                      }
                    }
                  ]
                }
              ]
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions { ForceRemoteSync = true });

        result.FeaturedPluginIds.Should().Equal("plug-1");
        result.MarketplaceLoadErrors.Should().ContainSingle();
        result.Marketplaces.Should().ContainSingle();
        result.Marketplaces[0].Interface.Should().NotBeNull();
        result.Marketplaces[0].Interface!.DisplayName.Should().Be("Official Marketplace");
        result.Marketplaces[0].Plugins.Should().ContainSingle();
        result.Marketplaces[0].Plugins[0].Id.Should().Be("plug-1");
        result.Marketplaces[0].Plugins[0].AuthPolicyValue.Should().Be(PluginAuthPolicy.OnInstall);
        result.Marketplaces[0].Plugins[0].InstallPolicyValue.Should().Be(PluginInstallPolicy.Available);
        result.Marketplaces[0].Plugins[0].Interface.Should().NotBeNull();
        result.Marketplaces[0].Plugins[0].Interface!.DisplayName.Should().Be("Plugin One Display");
        result.Marketplaces[0].Plugins[0].Interface!.Capabilities.Should().Equal("issues", "pull-requests");
        result.Marketplaces[0].Plugins[0].SourceInfo.Should().NotBeNull();
        result.Marketplaces[0].Plugins[0].SourceInfo!.Type.Should().Be(PluginSourceType.Parse("curated"));
        result.Marketplaces[0].Plugins[0].Source.Should().NotBeNull();
        result.Marketplaces[0].Plugins[0].Source!.Value.TryGetProperty("channel", out var channel).Should().BeTrue();
        channel.GetString().Should().Be("official");
        rpc.LastMethod.Should().Be("plugin/list");
    }

    [Fact]
    public async Task ListPluginsAsync_DefaultsOptionalArraysToEmpty()
    {
        using var doc = JsonDocument.Parse("""{"marketplaces":[{"name":"official","path":"C:\\market","plugins":[]}]}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ListPluginsAsync(new PluginListOptions());

        result.FeaturedPluginIds.Should().BeEmpty();
        result.MarketplaceLoadErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPluginAsync_ParsesDetailSurfaceAndSkillInterfaceMetadata()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "plugin": {
                "description": "desc",
                "marketplaceName": "official",
                "marketplacePath": "C:\\market",
                "skills": [
                  {
                    "name": "skill-a",
                    "path": "skills\\a",
                    "enabled": true,
                    "description": "desc",
                    "interface": {
                      "displayName": "Skill A",
                      "brandColor": "#abcdef",
                      "defaultPrompt": "Explain the failing build",
                      "iconLarge": "https://example.test/skill-large.png",
                      "iconSmall": "https://example.test/skill-small.png",
                      "shortDescription": "Skill short"
                    }
                  }
                ],
                "summary": {
                  "id": "plug-1",
                  "name": "Plugin One",
                  "installed": true,
                  "enabled": true,
                  "authPolicy": "ON_USE",
                  "installPolicy": "INSTALLED_BY_DEFAULT",
                  "interface": {
                    "displayName": "Plugin One Display"
                  }
                }
              }
            }
            """);
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.ReadPluginAsync(new PluginReadOptions
        {
            MarketplacePath = "C:\\market",
            PluginName = "plugin-one"
        });

        result.Plugin.Summary.Id.Should().Be("plug-1");
        result.Plugin.Summary.AuthPolicyValue.Should().Be(PluginAuthPolicy.OnUse);
        result.Plugin.Summary.InstallPolicyValue.Should().Be(PluginInstallPolicy.InstalledByDefault);
        result.Plugin.Summary.Interface.Should().NotBeNull();
        result.Plugin.Summary.Interface!.DisplayName.Should().Be("Plugin One Display");
        result.Plugin.McpServers.Should().BeEmpty();
        result.Plugin.Skills.Should().ContainSingle();
        result.Plugin.Skills[0].Interface.Should().NotBeNull();
        result.Plugin.Skills[0].Interface!.DisplayName.Should().Be("Skill A");
        result.Plugin.Skills[0].Interface!.DefaultPrompt.Should().Be("Explain the failing build");
        result.Plugin.Apps.Should().BeEmpty();
        rpc.LastMethod.Should().Be("plugin/read");
    }

    [Fact]
    public async Task InstallPluginAsync_ParsesTypedAuthPolicyAndDefaultsAppsNeedingAuth()
    {
        using var doc = JsonDocument.Parse("""{"authPolicy":"ON_INSTALL"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };
        await using var client = CreateClient(rpc);

        var result = await client.InstallPluginAsync(new PluginInstallOptions
        {
            MarketplacePath = "C:\\market",
            PluginName = "plugin-one"
        });

        result.AuthPolicy.Should().Be("ON_INSTALL");
        result.AuthPolicyValue.Should().Be(PluginAuthPolicy.OnInstall);
        result.AppsNeedingAuth.Should().BeEmpty();
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
