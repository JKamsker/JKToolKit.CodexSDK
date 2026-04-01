using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExternalAgentConfigWrappersTests
{
    [Fact]
    public async Task DetectExternalAgentConfigAsync_ParsesClosedEnum_AndCallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "externalAgentConfig/detect",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("includeHome").GetBoolean().Should().BeTrue();
                json.GetProperty("cwds").GetArrayLength().Should().Be(1);
            },
            Result = JsonSerializer.SerializeToElement(new
            {
                items = new object[]
                {
                    new
                    {
                        itemType = "AGENTS_MD",
                        description = "Import AGENTS.md",
                        cwd = "C:/repo"
                    }
                }
            })
        };

        await using var client = CreateClient(rpc);

        var result = await client.DetectExternalAgentConfigAsync(new ExternalAgentConfigDetectOptions
        {
            IncludeHome = true,
            Cwds = ["C:/repo"]
        });

        result.Items.Should().ContainSingle();
        result.Items[0].ItemType.Should().Be(ExternalAgentConfigMigrationItemType.AgentsMd);
        result.Items[0].Cwd.Should().Be("C:/repo");
    }

    [Fact]
    public async Task DetectExternalAgentConfigAsync_WhenItemsMissing_Throws()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.DetectExternalAgentConfigAsync(new ExternalAgentConfigDetectOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*items*externalAgentConfig/detect*");
    }

    [Fact]
    public async Task DetectExternalAgentConfigAsync_WhenItemTypeUnknown_Throws()
    {
        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new
            {
                items = new object[]
                {
                    new
                    {
                        itemType = "NOT_REAL",
                        description = "broken"
                    }
                }
            })
        };

        await using var client = CreateClient(rpc);

        var act = async () => await client.DetectExternalAgentConfigAsync(new ExternalAgentConfigDetectOptions());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*itemType*NOT_REAL*");
    }

    [Fact]
    public async Task ImportExternalAgentConfigAsync_AllowsEmptySelection()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "externalAgentConfig/import",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                json.GetProperty("migrationItems").GetArrayLength().Should().Be(0);
            },
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = CreateClient(rpc);

        await client.ImportExternalAgentConfigAsync([]);
    }

    [Fact]
    public async Task ImportExternalAgentConfigAsync_SerializesClosedEnumWireValue()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "externalAgentConfig/import",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                var item = json.GetProperty("migrationItems")[0];
                item.GetProperty("itemType").GetString().Should().Be("MCP_SERVER_CONFIG");
                item.GetProperty("description").GetString().Should().Be("Import MCP config");
            },
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = CreateClient(rpc);

        await client.ImportExternalAgentConfigAsync(
        [
            new ExternalAgentConfigMigrationItem
            {
                ItemType = ExternalAgentConfigMigrationItemType.McpServerConfig,
                Description = "Import MCP config"
            }
        ]);
    }

    [Fact]
    public async Task ImportExternalAgentConfigAsync_AllowsEmptyDescription_AndSerializesNullHomeCwd()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "externalAgentConfig/import",
            AssertParams = p =>
            {
                var json = JsonSerializer.SerializeToElement(p);
                var item = json.GetProperty("migrationItems")[0];
                item.TryGetProperty("cwd", out var cwd).Should().BeTrue();
                cwd.ValueKind.Should().Be(JsonValueKind.Null);
                item.GetProperty("description").GetString().Should().BeEmpty();
                item.GetProperty("itemType").GetString().Should().Be("CONFIG");
            },
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = CreateClient(rpc);

        await client.ImportExternalAgentConfigAsync(
        [
            new ExternalAgentConfigMigrationItem
            {
                Cwd = null,
                ItemType = ExternalAgentConfigMigrationItemType.Config,
                Description = string.Empty
            }
        ]);
    }

    private static CodexAppServerClient CreateClient(FakeRpc rpc) =>
        new(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync()
        {
            _tcs.TrySetCanceled();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public string AssertMethod { get; init; } = string.Empty;

        public Action<object?>? AssertParams { get; init; }

        public JsonElement Result { get; init; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(AssertMethod))
            {
                method.Should().Be(AssertMethod);
            }

            AssertParams?.Invoke(@params);
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
