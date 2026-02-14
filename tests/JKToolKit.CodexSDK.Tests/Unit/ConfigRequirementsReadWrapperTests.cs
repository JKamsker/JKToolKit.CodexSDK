using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ConfigRequirementsReadWrapperTests
{
    [Fact]
    public async Task ReadConfigRequirementsAsync_CallsExpectedMethod_AndParsesRequirements()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            requirements = new
            {
                allowedApprovalPolicies = new[] { "never" }
            }
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "configRequirements/read",
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.ReadConfigRequirementsAsync();

        result.Raw.ValueKind.Should().Be(JsonValueKind.Object);
        result.Requirements.Should().NotBeNull();
        result.Requirements!.AllowedApprovalPolicies!.Select(p => p.Value).Should().Equal("never");
    }

    [Fact]
    public async Task ReadConfigRequirementsAsync_DoesNotPopulateNetwork_WhenExperimentalApiIsDisabled()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            requirements = new
            {
                network = new { enabled = true }
            }
        });

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = false },
            new FakeProcess(),
            new FakeRpc { AssertMethod = "configRequirements/read", Result = rawResult },
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.ReadConfigRequirementsAsync();
        result.Requirements.Should().NotBeNull();
        result.Requirements!.Network.Should().BeNull();
    }

    [Fact]
    public async Task ReadConfigRequirementsAsync_PopulatesNetwork_WhenExperimentalApiIsEnabled()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            requirements = new
            {
                network = new { enabled = true, httpPort = 8080 }
            }
        });

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            new FakeRpc { AssertMethod = "configRequirements/read", Result = rawResult },
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.ReadConfigRequirementsAsync();
        result.Requirements.Should().NotBeNull();
        result.Requirements!.Network.Should().NotBeNull();
        result.Requirements.Network!.HttpPort.Should().Be(8080);
    }

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public string AssertMethod { get; init; } = string.Empty;
        public JsonElement Result { get; init; }

        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            method.Should().Be(AssertMethod);
            @params.Should().BeNull();
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

