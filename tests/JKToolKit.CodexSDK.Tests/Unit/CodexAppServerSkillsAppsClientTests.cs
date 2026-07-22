using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;
using JKToolKit.CodexSDK.Tests.TestHelpers;

using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexAppServerSkillsAppsClientTests
{
    [Fact]
    public async Task ListSkillsAsync_SendsCurrentUpstreamParams()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;
                typed.Cwds.Should().Equal(XPaths.Abs("cwd-1"), XPaths.Abs("cwd-2"));
                typed.ForceReload.Should().BeTrue();

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            Cwds = [XPaths.Abs("cwd-1"), XPaths.Abs("cwd-2")],
            ForceReload = true
        };

        await client.ListSkillsAsync(options);
    }

    [Fact]
    public async Task ListSkillsAsync_IgnoresRemovedExtraRootsOptions()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;
                typed.Cwds.Should().Equal(XPaths.Abs("cwd-main"));
                typed.AdditionalProperties.Should().NotContainKey("perCwdExtraUserRoots");

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            Cwds = new[] { XPaths.Abs("cwd-main") },
            ExtraRootsForCwd = ["relative-extra-root"],
            PerCwdExtraUserRoots =
            [
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "relative\\cwd",
                    ExtraUserRoots = ["relative\\skills"]
                }
            ]
        };

        await client.ListSkillsAsync(options);
    }

    [Fact]
    public async Task ListSkillsAsync_AllowsRelativeCwds_ToPassThrough()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;

                typed.Cwds.Should().Equal("relative\\cwd", ".\\second");

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            Cwds = ["relative\\cwd", ".\\second"]
        });
    }

    [Fact]
    public async Task ListSkillsAsync_UsesSingleCwdWhenCwdsNotSet()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;

                typed.Cwds.Should().Equal(".\\repo");

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            Cwd = ".\\repo"
        });
    }

    [Fact]
    public void ProtocolSkillsListParams_DoesNotSerializeRemovedExtraRoots()
    {
        var json = JsonSerializer.Serialize(
            new JKToolKit.CodexSDK.AppServer.Protocol.V2.SkillsListParams
            {
                Cwds = ["C:\\repo"],
                PerCwdExtraUserRoots =
                [
                    new JKToolKit.CodexSDK.AppServer.Protocol.V2.SkillsListExtraRootsForCwd
                    {
                        Cwd = "C:\\repo",
                        ExtraUserRoots = ["C:\\skills"]
                    }
                ]
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("cwds");
        json.Should().NotContain("perCwdExtraUserRoots");
    }

    [Fact]
    public async Task SetSkillsExtraRootsAsync_SendsCurrentUpstreamParams()
    {
        var root = XPaths.Abs("skills-root");
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/extraRoots/set");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsExtraRootsSetParams>().Which;
                typed.ExtraRoots.Should().Equal(root);

                return Task.FromResult(JsonDocument.Parse("""{}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var result = await client.SetSkillsExtraRootsAsync(new SkillsExtraRootsSetOptions
        {
            ExtraRoots = [root]
        });

        result.Raw.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task SetSkillsExtraRootsAsync_RejectsRelativeRoots()
    {
        var client = new CodexAppServerSkillsAppsClient((_, _, _) =>
            Task.FromResult(JsonDocument.Parse("""{}""").RootElement.Clone()));

        var act = async () => await client.SetSkillsExtraRootsAsync(new SkillsExtraRootsSetOptions
        {
            ExtraRoots = ["relative\\skills"]
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ExtraRoots[0]*");
    }

    [Fact]
    public async Task ListAppsAsync_RejectsCwdScoping()
    {
        var client = new CodexAppServerSkillsAppsClient((_, _, _) =>
            Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone()));

        var act = async () => await client.ListAppsAsync(new AppsListOptions
        {
            Cwd = "C:\\repo"
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ThreadId*");
    }

    [Fact]
    public async Task ListAppsAsync_RejectsNegativeLimit()
    {
        var client = new CodexAppServerSkillsAppsClient((_, _, _) =>
            Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone()));

        var act = async () => await client.ListAppsAsync(new AppsListOptions
        {
            Limit = -1
        });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ReadAppsAsync_SendsCurrentUpstreamParams_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("app/read");
                var typed = @params.Should().BeOfType<UpstreamV2.AppsReadParams>().Which;
                typed.AppIds.Should().Equal("app-a", "app-b");
                typed.IncludeTools.Should().BeTrue();

                return Task.FromResult(JsonDocument.Parse(
                    """
                    {
                      "apps": [
                        {
                          "id": "app-a",
                          "name": "App A",
                          "description": "Reads things",
                          "iconUrl": "https://example.test/a.png",
                          "iconUrlDark": "https://example.test/a-dark.png",
                          "distributionChannel": "curated",
                          "installUrl": "https://example.test/install",
                          "pluginDisplayNames": ["Plugin A"],
                          "toolSummaries": [
                            {
                              "name": "lookup",
                              "title": "Lookup",
                              "description": "Finds data"
                            }
                          ]
                        }
                      ],
                      "missingAppIds": ["app-b"]
                    }
                    """).RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var result = await client.ReadAppsAsync(new AppsReadOptions
        {
            AppIds = ["app-a", "app-b"],
            IncludeTools = true
        });

        result.MissingAppIds.Should().Equal("app-b");
        var app = result.Apps.Should().ContainSingle().Subject;
        app.Id.Should().Be("app-a");
        app.Name.Should().Be("App A");
        app.Description.Should().Be("Reads things");
        app.IconUrl.Should().Be("https://example.test/a.png");
        app.IconUrlDark.Should().Be("https://example.test/a-dark.png");
        app.DistributionChannel.Should().Be("curated");
        app.InstallUrl.Should().Be("https://example.test/install");
        app.PluginDisplayNames.Should().Equal("Plugin A");
        var tool = app.ToolSummaries.Should().ContainSingle().Subject;
        tool.Name.Should().Be("lookup");
        tool.Title.Should().Be("Lookup");
        tool.Description.Should().Be("Finds data");
    }

    [Fact]
    public async Task ReadAppsAsync_OmitsIncludeTools_WhenFalse()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("app/read");
                var typed = @params.Should().BeOfType<UpstreamV2.AppsReadParams>().Which;
                typed.AppIds.Should().Equal("app-a");
                typed.IncludeTools.Should().BeNull();

                return Task.FromResult(JsonDocument.Parse("""{"apps":[],"missingAppIds":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ReadAppsAsync(new AppsReadOptions
        {
            AppIds = ["app-a"]
        });
    }

    [Fact]
    public async Task ReadAppsAsync_RejectsInvalidAppIds()
    {
        var client = new CodexAppServerSkillsAppsClient((_, _, _) =>
            Task.FromResult(JsonDocument.Parse("""{"apps":[],"missingAppIds":[]}""").RootElement.Clone()));

        var empty = async () => await client.ReadAppsAsync(new AppsReadOptions { AppIds = [] });
        var blank = async () => await client.ReadAppsAsync(new AppsReadOptions { AppIds = ["app-a", " "] });

        await empty.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*AppIds*");
        await blank.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*AppIds[1]*");
    }

    [Fact]
    public async Task ReadInstalledAppsAsync_SendsCurrentUpstreamParams_AndParsesResponse()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("app/installed");
                var typed = @params.Should().BeOfType<UpstreamV2.AppsInstalledParams>().Which;
                typed.ThreadId.Should().Be("thread-1");
                typed.ForceRefresh.Should().BeTrue();

                return Task.FromResult(JsonDocument.Parse(
                    """
                    {
                      "apps": [
                        {
                          "id": "demo-app",
                          "runtimeName": "Demo App",
                          "enabled": true,
                          "callable": false
                        }
                      ]
                    }
                    """).RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var result = await client.ReadInstalledAppsAsync(new AppsInstalledOptions
        {
            ThreadId = "thread-1",
            ForceRefresh = true
        });

        var app = result.Apps.Should().ContainSingle().Subject;
        app.Id.Should().Be("demo-app");
        app.RuntimeName.Should().Be("Demo App");
        app.Enabled.Should().BeTrue();
        app.Callable.Should().BeFalse();
    }

    private sealed class FakeRpc
    {
        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            SendRequestAsyncImpl?.Invoke(method, @params, ct) ?? Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
