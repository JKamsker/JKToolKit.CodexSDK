using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadLifecycleEnvelopeTests
{
    [Fact]
    public async Task StartThreadAsync_ReturnsTypedLifecycleEnvelope()
    {
        var response = JsonFixtures.Load("thread-start-response.json");
        var client = new CodexAppServerThreadsClient(
            sendRequestAsync: (_, _, _) => Task.FromResult(response),
            experimentalApiEnabled: () => false);

        var thread = await client.StartThreadAsync(new ThreadStartOptions
        {
            Model = CodexModel.Gpt52Codex
        });

        thread.Id.Should().Be("t_started");
        thread.Thread.Model.Should().Be("gpt-5");
        thread.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.User);
        thread.ServiceTier.Should().Be(CodexServiceTier.Fast);
    }

    [Fact]
    public async Task ResumeThreadAsync_ReturnsTypedLifecycleEnvelope()
    {
        var response = JsonFixtures.Load("thread-resume-response.json");
        var client = new CodexAppServerThreadsClient(
            sendRequestAsync: (_, _, _) => Task.FromResult(response),
            experimentalApiEnabled: () => true);

        var thread = await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            Path = "C:\\rollout.jsonl"
        });

        thread.Id.Should().Be("t_resumed");
        thread.Thread.TurnCount.Should().Be(2);
        thread.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.GuardianSubagent);
        thread.Sandbox.Should().Be(CodexSandboxMode.DangerFullAccess);
    }
}
