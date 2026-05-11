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

    private sealed class FakeRpc
    {
        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            SendRequestAsyncImpl?.Invoke(method, @params, ct) ?? Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
