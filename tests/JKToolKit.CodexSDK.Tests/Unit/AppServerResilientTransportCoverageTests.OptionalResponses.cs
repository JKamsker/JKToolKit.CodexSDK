using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Fact]
    public async Task CollaborationModes_SkipsUnnamedEntriesAndPreservesOptionalFallbacks()
    {
        var rpc = new RecordingRpc("""{"data":[{}, {"name":42}, {"name":" "}, {"name":"plan","mode":false,"reasoningEffort":"high"}, {"name":"default","model":null}]}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ListCollaborationModesAsync();
        result.Data.Select(x => x.Name).Should().Equal("plan", "default");
        result.Data[0].Mode.Should().BeNull();
        result.Data[0].ReasoningEffort.Should().Be("high");
        result.Data[1].Model.Should().BeNull();
        result.Data[1].ReasoningEffort.Should().BeNull();
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("collaborationMode/list");
    }

    [Theory]
    [InlineData("headline", WorkspaceMessageKind.Headline)]
    [InlineData("announcement", WorkspaceMessageKind.Announcement)]
    [InlineData("future", WorkspaceMessageKind.Unknown)]
    public async Task WorkspaceMessages_PreservesKnownAndFutureMessageKinds(string kind, WorkspaceMessageKind expected)
    {
        var rpc = new RecordingRpc(JsonSerializer.Serialize(new { featureEnabled = true, messages = new[] { new { messageId = "message-1", messageType = kind, messageBody = "body" } } }));
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ReadWorkspaceMessagesAsync();
        result.Messages.Should().ContainSingle().Which.MessageKind.Should().Be(expected);
        result.Messages[0].MessageType.Should().Be(kind);
        result.Messages[0].MessageBody.Should().Be("body");
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("account/workspaceMessages/read");
    }

    [Fact]
    public async Task ThreadUsage_AbsentGroupsMeansEmptyBreakdown()
    {
        var rpc = new RecordingRpc("""{"summary":{},"threadUsage":{"threadId":"thread-1"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ReadAccountTokenUsageAsync(new AccountTokenUsageReadOptions { ThreadId = "thread-1" });
        result.ThreadUsage.Should().NotBeNull();
        result.ThreadUsage!.ThreadId.Should().Be("thread-1");
        result.ThreadUsage.Groups.Should().BeEmpty();
        rpc.Requests.Should().ContainSingle().Which.Method.Should().Be("account/usage/read");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Turns_AcceptAndReuseSupportedReadOnlyAccessOverrides(bool workspaceWrite)
    {
        var rpc = new RecordingRpc("""{"turn":{"id":"turn-1"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        SandboxPolicy policy = workspaceWrite
            ? new SandboxPolicy.WorkspaceWrite { ReadOnlyAccess = new ReadOnlyAccess.FullAccess() }
            : new SandboxPolicy.ReadOnly { Access = new ReadOnlyAccess.FullAccess() };
        await using var first = await client.StartTurnAsync("thread-1", new() { SandboxPolicy = policy });
        await using var second = await client.StartTurnAsync("thread-1", new() { SandboxPolicy = policy });
        first.TurnId.Should().Be("turn-1");
        second.TurnId.Should().Be("turn-1");
        rpc.Requests.Should().HaveCount(2);
        foreach (var request in rpc.Requests)
        {
            request.Method.Should().Be("turn/start");
            request.Parameters.GetProperty("sandboxPolicy").GetProperty(workspaceWrite ? "readOnlyAccess" : "access").GetProperty("type").GetString().Should().Be("fullAccess");
        }
    }

    [Fact]
    public async Task CommandExecution_TransportsSandboxPolicyAndPtySize()
    {
        var rpc = new RecordingRpc("""{"stdout":"done","stderr":"","exitCode":0}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.CommandExecAsync(new()
        {
            Command = ["echo", "hello"], SandboxPolicy = new SandboxPolicy.ReadOnly(),
            Tty = true, ProcessId = "process-1", Size = new() { Rows = 24, Columns = 80 }
        });
        result.Stdout.Should().Be("done");
        var request = rpc.Requests.Should().ContainSingle().Which;
        request.Parameters.GetProperty("sandboxPolicy").GetProperty("type").GetString().Should().Be("readOnly");
        request.Parameters.GetProperty("size").GetProperty("rows").GetInt32().Should().Be(24);
        request.Parameters.GetProperty("size").GetProperty("cols").GetInt32().Should().Be(80);
    }
}
