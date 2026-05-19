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
        thread.Thread.Turns.Should().NotBeNull();
        thread.Thread.Turns.Should().HaveCount(2);
        thread.Thread.Turns![0].Id.Should().Be("turn_1");
        thread.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.GuardianSubagent);
        thread.Sandbox.Should().Be(CodexSandboxMode.DangerFullAccess);
    }

    [Fact]
    public async Task StartThreadAsync_ReturnsRuntimeRootsInstructionSourcesAndPermissionProfile()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            """
            {
              "thread": {
                "id": "t_started"
              },
              "runtimeWorkspaceRoots": ["C:/repo", "C:/repo/sub"],
              "instructionSources": ["C:/repo/AGENTS.md"],
              "activePermissionProfile": {
                "id": "profile-1",
                "extends": "base"
              }
            }
            """);
        var response = doc.RootElement.Clone();
        var client = new CodexAppServerThreadsClient(
            sendRequestAsync: (_, _, _) => Task.FromResult(response),
            experimentalApiEnabled: () => false);

        var thread = await client.StartThreadAsync(new ThreadStartOptions());

        thread.RuntimeWorkspaceRoots.Should().Equal("C:/repo", "C:/repo/sub");
        thread.InstructionSources.Should().Equal("C:/repo/AGENTS.md");
        thread.ActivePermissionProfile.Should().NotBeNull();
        thread.ActivePermissionProfile!.Id.Should().Be("profile-1");
        thread.ActivePermissionProfile.Extends.Should().Be("base");
    }
}
