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
                var entries = typed.PerCwdExtraUserRoots.ToArray();
                entries.Should().HaveCount(2);

                entries[0].Cwd.Should().Be("C:\\cwd-1");
                entries[0].ExtraUserRoots.Should().Equal(new[] { "C:\\root-a" });
                entries[1].Cwd.Should().Be("D:\\cwd-2");
                entries[1].ExtraUserRoots.Should().Equal(new[] { "D:\\root-b", "D:\\root-c" });

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
                    Cwd = "C:\\cwd-1",
                    ExtraUserRoots = new[] { "C:\\root-a" }
                },
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "D:\\cwd-2",
                    ExtraUserRoots = new[] { "D:\\root-b", "D:\\root-c" }
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
                var entries = typed.PerCwdExtraUserRoots.ToArray();
                entries.Should().HaveCount(1);

                entries[0].Cwd.Should().Be("C:\\cwd-main");
                entries[0].ExtraUserRoots.Should().Equal(new[] { "C:\\extra" });

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            Cwds = new[] { "C:\\cwd-main" },
            ExtraRootsForCwd = new[] { "C:\\extra" }
        };

        await client.ListSkillsAsync(options);
    }

    [Fact]
    public async Task ListSkillsAsync_RejectsRelativePerCwdExtraRoots()
    {
        var client = new CodexAppServerSkillsAppsClient((_, _, _) =>
            Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone()));

        var act = async () => await client.ListSkillsAsync(new SkillsListOptions
        {
            PerCwdExtraUserRoots =
            [
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "C:\\repo",
                    ExtraUserRoots = ["relative\\skills"]
                }
            ]
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*absolute paths*");
    }

    [Fact]
    public async Task ListSkillsAsync_AllowsRelativeCwdsAndPerCwdScopes_ToPassThrough()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;

                typed.Cwds.Should().Equal("relative\\cwd", ".\\second");
                typed.PerCwdExtraUserRoots.Should().ContainSingle();
                typed.PerCwdExtraUserRoots.Single().Cwd.Should().Be("relative\\cwd");
                typed.PerCwdExtraUserRoots.Single().ExtraUserRoots.Should().Equal("C:\\extra-root");

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            Cwds = ["relative\\cwd", ".\\second"],
            PerCwdExtraUserRoots =
            [
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = "relative\\cwd",
                    ExtraUserRoots = ["C:\\extra-root"]
                }
            ]
        });
    }

    [Fact]
    public async Task ListSkillsAsync_AllowsRelativeSingleCwd_WhenUsingExtraRootsForCwd()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;

                typed.Cwds.Should().Equal(".\\repo");
                typed.PerCwdExtraUserRoots.Should().ContainSingle();
                typed.PerCwdExtraUserRoots.Single().Cwd.Should().Be(".\\repo");
                typed.PerCwdExtraUserRoots.Single().ExtraUserRoots.Should().Equal("C:\\extra-root");

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            Cwd = ".\\repo",
            ExtraRootsForCwd = ["C:\\extra-root"]
        });
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
