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

                entries[0].Cwd.Should().Be(XPaths.Abs("cwd-1"));
                entries[0].ExtraUserRoots.Should().Equal(new[] { XPaths.Abs("root-a") });
                entries[1].Cwd.Should().Be(XPaths.Abs("cwd-2"));
                entries[1].ExtraUserRoots.Should().Equal(new[] { XPaths.Abs("root-b"), XPaths.Abs("root-c") });

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
                    Cwd = XPaths.Abs("cwd-1"),
                    ExtraUserRoots = new[] { XPaths.Abs("root-a") }
                },
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = XPaths.Abs("cwd-2"),
                    ExtraUserRoots = new[] { XPaths.Abs("root-b"), XPaths.Abs("root-c") }
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

                entries[0].Cwd.Should().Be(XPaths.Abs("cwd-main"));
                entries[0].ExtraUserRoots.Should().Equal(new[] { XPaths.Abs("extra") });

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        var options = new SkillsListOptions
        {
            Cwds = new[] { XPaths.Abs("cwd-main") },
            ExtraRootsForCwd = new[] { XPaths.Abs("extra") }
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
                typed.PerCwdExtraUserRoots.Single().ExtraUserRoots.Should().Equal(XPaths.Abs("extra-root"));

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
                    ExtraUserRoots = [XPaths.Abs("extra-root")]
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
                typed.PerCwdExtraUserRoots.Single().ExtraUserRoots.Should().Equal(XPaths.Abs("extra-root"));

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            Cwd = ".\\repo",
            ExtraRootsForCwd = [XPaths.Abs("extra-root")]
        });
    }

    [Fact]
    public async Task ListSkillsAsync_AllowsBlankPerCwdScopes_AndEmptyExtraRoots_ToPassThrough()
    {
        var rpc = new FakeRpc
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                method.Should().Be("skills/list");
                var typed = @params.Should().BeOfType<UpstreamV2.SkillsListParams>().Which;

                typed.PerCwdExtraUserRoots.Should().ContainSingle();
                typed.PerCwdExtraUserRoots.Single().Cwd.Should().Be("");
                typed.PerCwdExtraUserRoots.Single().ExtraUserRoots.Should().BeEmpty();

                return Task.FromResult(JsonDocument.Parse("""{"data":[]}""").RootElement.Clone());
            }
        };

        var client = new CodexAppServerSkillsAppsClient(rpc.SendRequestAsync);

        await client.ListSkillsAsync(new SkillsListOptions
        {
            PerCwdExtraUserRoots =
            [
                new SkillsListExtraRootsForCwdEntry
                {
                    Cwd = string.Empty,
                    ExtraUserRoots = Array.Empty<string>()
                }
            ]
        });
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
