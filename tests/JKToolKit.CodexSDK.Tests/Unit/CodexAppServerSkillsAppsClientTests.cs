using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexAppServerSkillsAppsClientTests
{
    [Fact]
    public async Task ListSkillsAsync_IncludesPerCwdExtraUserRoots()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;
                typed.PerCwdExtraUserRoots.Should().HaveCount(2);

                typed.PerCwdExtraUserRoots[0].Cwd.Should().Be("cwd-1");
                typed.PerCwdExtraUserRoots[0].ExtraUserRoots.Should().Equal(new[] { "root-a" });
                typed.PerCwdExtraUserRoots[1].Cwd.Should().Be("cwd-2");
                typed.PerCwdExtraUserRoots[1].ExtraUserRoots.Should().Equal(new[] { "root-b", "root-c" });

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            PerCwdExtraUserRoots = new[]
            {
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "cwd-1",
                    ExtraUserRoots = new[] { "root-a" }
                },
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "cwd-2",
                    ExtraUserRoots = new[] { "root-b", "root-c" }
                }
            }
        };

        await client.ListSkillsAsync(options);
    }

    [Fact]
    public async Task ListSkillsAsync_IncludesSingleCwdExtraRootsWhenPerCwdNotSet()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;
                typed.PerCwdExtraUserRoots.Should().HaveCount(1);

                typed.PerCwdExtraUserRoots[0].Cwd.Should().Be("cwd-main");
                typed.PerCwdExtraUserRoots[0].ExtraUserRoots.Should().Equal(new[] { "extra" });

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            Cwds = new[] { "cwd-main" },
            ExtraRootsForCwd = new[] { "extra" }
        };

        await client.ListSkillsAsync(options);
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
