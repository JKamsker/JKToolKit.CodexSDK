using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Theory]
    [InlineData("exec")]
    [InlineData("write")]
    [InlineData("resize")]
    [InlineData("terminate")]
    [InlineData("usage")]
    [InlineData("size")]
    public async Task Operation_NullOptionsProvideArgumentContract(string kind)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> operation = kind switch
        {
            "exec" => () => client.CommandExecAsync(null!),
            "write" => () => client.CommandExecWriteAsync(null!),
            "resize" => () => client.CommandExecResizeAsync(null!),
            "terminate" => () => client.CommandExecTerminateAsync(null!),
            "size" => () => client.CommandExecResizeAsync(new() { ProcessId = "process-1", Size = null! }),
            _ => () => client.ReadAccountTokenUsageAsync((AccountTokenUsageReadOptions)null!)
        };
        await operation.Should().ThrowAsync<ArgumentNullException>().WithParameterName(kind == "size" ? "options.Size" : "options");
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("exec", "[]", "command/exec response")]
    [InlineData("exec", "{}", "command/exec response")]
    [InlineData("exec", "{\"exitCode\":0}", "command/exec response")]
    [InlineData("exec", "{\"exitCode\":0,\"stdout\":\"\"}", "command/exec response")]
    [InlineData("write", "[]", "command/exec/write response")]
    [InlineData("resize", "[]", "command/exec/resize response")]
    [InlineData("terminate", "[]", "command/exec/terminate response")]
    [InlineData("usage", "{}", "account/usage/read response missing required object property 'summary'")]
    [InlineData("usage", "{\"summary\":{},\"dailyUsageBuckets\":[42]}", "account/usage/read dailyUsageBuckets[] entries must be objects")]
    [InlineData("usage", "{\"summary\":{},\"dailyUsageBuckets\":[{\"tokens\":1}]}", "account/usage/read dailyUsageBuckets[]")]
    [InlineData("usage", "{\"summary\":{},\"dailyUsageBuckets\":[{\"startDate\":\"2026-10-01\"}]}", "account/usage/read dailyUsageBuckets[]")]
    public async Task InvalidOperationResponse_IdentifiesFailingWireContract(string kind, string response, string expected)
    {
        var rpc = new RecordingRpc(response);
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> operation = kind switch
        {
            "exec" => () => client.CommandExecAsync(new() { Command = ["echo"] }),
            "write" => () => client.CommandExecWriteAsync(new() { ProcessId = "process-1", CloseStdin = true }),
            "resize" => () => client.CommandExecResizeAsync(new() { ProcessId = "process-1", Size = new() { Rows = 24, Columns = 80 } }),
            "terminate" => () => client.CommandExecTerminateAsync(new() { ProcessId = "process-1" }),
            _ => () => client.ReadAccountTokenUsageAsync()
        };
        var error = (await operation.Should().ThrowAsync<InvalidOperationException>()).Which;
        error.Message.Should().Contain(expected);
        rpc.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task CommandExecution_ZeroLimitsAndEnvironmentValuesArePreserved()
    {
        var rpc = new RecordingRpc("""{"exitCode":0,"stdout":"","stderr":""}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        await client.CommandExecAsync(new()
        {
            Command = ["echo"], TimeoutMs = 0, OutputBytesCap = 0,
            Env = new Dictionary<string, string?> { ["ENABLED"] = "yes", ["REMOVE_ME"] = null }
        });
        var request = rpc.Requests.Should().ContainSingle().Which.Parameters;
        request.GetProperty("timeoutMs").GetInt64().Should().Be(0);
        request.GetProperty("outputBytesCap").GetInt64().Should().Be(0);
        request.GetProperty("env").GetProperty("ENABLED").GetString().Should().Be("yes");
        request.GetProperty("env").GetProperty("REMOVE_ME").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData("stdin")]
    [InlineData("stdout")]
    [InlineData("tty")]
    public async Task CommandExecution_EachStreamingModeRequiresProcessIdentity(string flag)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var operation = () => client.CommandExecAsync(new()
        {
            Command = ["echo"], StreamStdin = flag == "stdin", StreamStdoutStderr = flag == "stdout", Tty = flag == "tty"
        });
        await operation.Should().ThrowAsync<ArgumentException>().WithParameterName("processId");
        rpc.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("empty-command", "Command cannot be empty")]
    [InlineData("write-process", "ProcessId cannot be empty or whitespace")]
    [InlineData("resize-process", "ProcessId cannot be empty or whitespace")]
    [InlineData("terminate-process", "ProcessId cannot be empty or whitespace")]
    [InlineData("output-cap", "DisableOutputCap cannot be combined with OutputBytesCap")]
    [InlineData("size-without-tty", "Size requires tty to be enabled")]
    public async Task InvalidCommandOptions_ExplainWhichValueMustBeCorrected(string kind, string expected)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        Func<Task> operation = kind switch
        {
            "empty-command" => () => client.CommandExecAsync(new() { Command = [] }),
            "write-process" => () => client.CommandExecWriteAsync(new() { ProcessId = " " }),
            "resize-process" => () => client.CommandExecResizeAsync(new() { ProcessId = " ", Size = new() { Rows = 24, Columns = 80 } }),
            "terminate-process" => () => client.CommandExecTerminateAsync(new() { ProcessId = " " }),
            "output-cap" => () => client.CommandExecAsync(new() { Command = ["echo"], DisableOutputCap = true, OutputBytesCap = 1 }),
            _ => () => client.CommandExecAsync(new() { Command = ["echo"], Size = new() { Rows = 24, Columns = 80 } })
        };
        var error = (await operation.Should().ThrowAsync<ArgumentException>()).Which;
        error.Message.Should().Contain(expected);
        rpc.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ThreadUsage_PreservesEveryBreakdownCounterAndSkipsNonObjectGroups()
    {
        var rpc = new RecordingRpc("""{"summary":{},"threadUsage":{"threadId":"thread-1","groups":[42,{"model":"model-1","reasoningEffort":"high","speed":"fast","inputTokens":101,"cachedInputTokens":102,"netNewInputTokens":103,"outputTokens":104,"totalTokens":105}]}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ReadAccountTokenUsageAsync(new AccountTokenUsageReadOptions { ThreadId = "thread-1" });
        var group = result.ThreadUsage!.Groups.Should().ContainSingle().Which;
        group.Model.Should().Be("model-1");
        group.ReasoningEffort.Should().Be("high");
        group.Speed.Should().Be("fast");
        group.InputTokens.Should().Be(101);
        group.CachedInputTokens.Should().Be(102);
        group.NetNewInputTokens.Should().Be(103);
        group.OutputTokens.Should().Be(104);
        group.TotalTokens.Should().Be(105);
    }
}
