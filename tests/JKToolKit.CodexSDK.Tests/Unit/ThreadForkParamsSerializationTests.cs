using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadForkParamsSerializationTests
{
    [Fact]
    public void Serialize_OmitsSandbox_WhenNull()
    {
        var json = JsonSerializer.Serialize(
            new ThreadForkParams { ThreadId = "thr_123" },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"threadId\":\"thr_123\"");
        json.Should().NotContain("\"sandbox\"");
    }

    [Fact]
    public void Serialize_IncludesSandbox_WhenSet()
    {
        var json = JsonSerializer.Serialize(
            new ThreadForkParams
            {
                ThreadId = "thr_123",
                Sandbox = "workspace-write"
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"sandbox\":\"workspace-write\"");
    }

    [Fact]
    public async Task ForkThreadAsync_MapsSandbox_FromOptionsToWireParams()
    {
        ThreadForkParams? captured = null;
        using var doc = JsonDocument.Parse("""{"threadId":"thr_new"}""");
        var response = doc.RootElement.Clone();

        var client = new CodexAppServerThreadsClient(
            sendRequestAsync: (method, @params, _) =>
            {
                method.Should().Be("thread/fork");
                captured = @params.Should().BeOfType<ThreadForkParams>().Subject;
                return Task.FromResult(response);
            },
            experimentalApiEnabled: () => true);

        var thread = await client.ForkThreadAsync(new ThreadForkOptions
        {
            ThreadId = "thr_source",
            Sandbox = CodexSandboxMode.WorkspaceWrite
        });

        thread.Id.Should().Be("thr_new");
        captured.Should().NotBeNull();
        captured!.ThreadId.Should().Be("thr_source");
        captured.Sandbox.Should().Be("workspace-write");
    }

    [Fact]
    public async Task ForkThreadAsync_PathTakesPrecedence_AndIgnoresThreadId()
    {
        ThreadForkParams? captured = null;
        using var doc = JsonDocument.Parse("""{"thread":{"id":"thr_new"}}""");
        var response = doc.RootElement.Clone();

        var client = new CodexAppServerThreadsClient(
            sendRequestAsync: (method, @params, _) =>
            {
                method.Should().Be("thread/fork");
                captured = @params.Should().BeOfType<ThreadForkParams>().Subject;
                return Task.FromResult(response);
            },
            experimentalApiEnabled: () => true);

        var thread = await client.ForkThreadAsync(new ThreadForkOptions
        {
            ThreadId = "thr_source",
            Path = "C:\\rollout.jsonl"
        });

        thread.Id.Should().Be("thr_new");
        captured.Should().NotBeNull();
        captured!.ThreadId.Should().BeEmpty();
        captured.Path.Should().Be("C:\\rollout.jsonl");
    }

    [Fact]
    public void Serialize_WritesApprovalsReviewer_AsClosedEnum()
    {
        var json = JsonSerializer.Serialize(
            new ThreadForkParams { ThreadId = "thr_123", ApprovalsReviewer = CodexApprovalsReviewer.User },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"approvalsReviewer\":\"user\"");
    }
}
