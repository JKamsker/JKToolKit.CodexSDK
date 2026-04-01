using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AppServer.ThreadRead;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadApiParsingTests
{
    [Fact]
    public void ParseThreadListThreads_ParsesIds_AndNextCursor()
    {
        var raw = JsonFixtures.Load("thread-list-response.json");

        var threads = CodexAppServerClient.ParseThreadListThreads(raw);
        var cursor = CodexAppServerClient.ExtractNextCursor(raw);

        threads.Select(t => t.ThreadId).Should().Equal("t_1", "t_2");
        cursor.Should().Be("cursor_2");
        threads[0].ModelProvider.Should().Be("openai");
        threads[0].Preview.Should().Be("Preview One");
        threads[0].SourceKind.Should().Be("cli");
        threads[0].TurnCount.Should().Be(1);
        threads[0].ServiceTier.Should().BeNull();
        threads[0].Status.Should().NotBeNull();
        threads[0].Status!.Kind.Should().Be(CodexThreadStatusKind.Active);
        threads[0].Status.ActiveFlags.Should().Contain("waitingOnUserInput");
        threads[0].Raw.TryGetProperty("unknownField", out _).Should().BeTrue();
        threads[1].Path.Should().Contain("rollout-t_2");
        threads[1].SourceKind.Should().Be("subAgentThreadSpawn");
        threads[1].AgentNickname.Should().Be("beta");
        threads[1].AgentRole.Should().Be("reviewer");
        threads[1].ServiceTier.Should().BeNull();
        threads[1].Raw.TryGetProperty("wrapperUnknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ReadThreadParsing_ExtractsThreadId_FromThreadObject()
    {
        var raw = JsonFixtures.Load("thread-read-response.json");
        raw.TryGetProperty("thread", out var threadObj).Should().BeTrue();

        CodexAppServerClient.ExtractThreadId(threadObj).Should().Be("t_read");

        var summary = CodexAppServerClient.ParseThreadSummary(threadObj, raw);
        summary.Should().NotBeNull();
        summary!.Name.Should().Be("Read Me");
        summary.ModelProvider.Should().Be("openai");
        summary.Path.Should().Contain("rollout-t_read");
        summary.SourceKind.Should().Be("cli");
        summary.ServiceTier.Should().BeNull();
        summary.TurnCount.Should().Be(3);
    }

    [Fact]
    public void ExtractThreadId_HandlesCommonShapes()
    {
        CodexAppServerClient.ExtractThreadId(JsonFixtures.Load("thread-fork-response.json")).Should().Be("t_forked");
        CodexAppServerClient.ExtractThreadId(JsonFixtures.Load("thread-unarchive-response.json")).Should().Be("t_arch");
    }

    [Fact]
    public void ParseThreadLoadedListThreadIds_ParsesIds_AndNextCursor()
    {
        var raw = JsonFixtures.Load("thread-loaded-list-response.json");

        var ids = CodexAppServerClient.ParseThreadLoadedListThreadIds(raw);
        var cursor = CodexAppServerClient.ExtractNextCursor(raw);

        ids.Should().Equal("t_loaded_1", "t_loaded_2");
        cursor.Should().Be("cursor_loaded_3");
    }

    [Fact]
    public void ParseLifecycleThread_ProjectsTypedEnvelopeFields()
    {
        var raw = JsonFixtures.Load("thread-fork-response.json");

        var result = CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(raw);

        result.Id.Should().Be("t_forked");
        result.Thread.Name.Should().Be("Forked");
        result.Thread.Model.Should().Be("gpt-5");
        result.Thread.Cwd.Should().Be("C:\\repo");
        result.ApprovalPolicy.Should().Be(JKToolKit.CodexSDK.Models.CodexApprovalPolicy.Never);
        result.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.GuardianSubagent);
        result.Sandbox.Should().Be(JKToolKit.CodexSDK.Models.CodexSandboxMode.WorkspaceWrite);
        result.ServiceTier.Should().Be(JKToolKit.CodexSDK.Models.CodexServiceTier.Flex);
    }

    [Fact]
    public void ParseLifecycleThread_PreservesStructuredPayloads()
    {
        var raw = JsonFixtures.Load("thread-structured-lifecycle-response.json");

        var result = CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(raw);

        result.ApprovalPolicy.Should().BeNull();
        result.ApprovalPolicyRaw.Should().NotBeNull();
        result.ApprovalPolicyRaw!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        result.SandboxRaw.Should().NotBeNull();
        result.SandboxRaw!.Value.ValueKind.Should().Be(JsonValueKind.Object);
        result.ReasoningEffort.Should().Be(CodexReasoningEffort.XHigh);
        result.Thread.Status.Should().NotBeNull();
        result.Thread.Status!.Kind.Should().Be(CodexThreadStatusKind.Active);
        result.Thread.Status.ActiveFlags.Should().HaveCount(2)
            .And.Contain(new[] { "waitingOnApproval", "waitingOnUserInput" });
    }

    [Fact]
    public void ParseReadResult_ReturnsThreadSummary()
    {
        var raw = JsonFixtures.Load("thread-read-response.json");

        var result = CodexAppServerClientThreadResponseParsers.ParseReadResult(raw, "fallback");

        result.Thread.ThreadId.Should().Be("t_read");
        result.Turns.Should().HaveCount(3);

        var response = JsonSerializer.Deserialize<ThreadReadResponse>(JsonFixtures.LoadText("thread-read-response.json"));
        response!.Thread.HasValue.Should().BeTrue();
    }

    [Fact]
    public void ParseReadResult_PreservesEmptyTurnsCollection()
    {
        using var doc = JsonDocument.Parse("""{"thread":{"id":"t_empty","turns":[]}}""");

        var result = CodexAppServerClientThreadResponseParsers.ParseReadResult(doc.RootElement, "fallback");

        result.Thread.ThreadId.Should().Be("t_empty");
        result.Thread.TurnCount.Should().Be(0);
        result.Thread.Turns.Should().NotBeNull();
        result.Thread.Turns.Should().BeEmpty();
        result.Turns.Should().NotBeNull();
        result.Turns.Should().BeEmpty();
    }

    [Fact]
    public void ParseReadResult_ParsesRichThreadItemVariants()
    {
        var raw = JsonFixtures.Load("thread-read-rich-items-response.json");

        var result = CodexAppServerClientThreadResponseParsers.ParseReadResult(raw, "fallback");
        var items = result.Turns.Should().ContainSingle().Subject.Items;

        items.Should().Contain(x => x is CodexThreadItemUserMessage);
        items.Should().Contain(x => x is CodexThreadItemHookPrompt);
        items.Should().Contain(x => x is CodexThreadItemPlan);
        items.Should().Contain(x => x is CodexThreadItemReasoning);
        items.Should().Contain(x => x is CodexThreadItemAgentMessage);
        items.Should().Contain(x => x is CodexThreadItemMcpToolCall);
        items.Should().Contain(x => x is CodexThreadItemDynamicToolCall);
        items.Should().Contain(x => x is CodexThreadItemCollabAgentToolCall);
        items.Should().Contain(x => x is CodexThreadItemImageView);
        items.Should().Contain(x => x is CodexThreadItemImageGeneration);
        items.Should().Contain(x => x is CodexThreadItemEnteredReviewMode);
        items.Should().Contain(x => x is CodexThreadItemExitedReviewMode);
        items.Should().Contain(x => x is CodexThreadItemContextCompaction);

        var command = items.OfType<CodexThreadItemCommandExecution>().Should().ContainSingle().Subject;
        command.Source.Should().Be(CodexCommandExecutionSource.UserShell);
        command.WorkingDirectory.Should().Be("C:\\repo");
        command.CommandActions.Should().ContainSingle().Which.Type.Should().Be("listFiles");

        var fileChange = items.OfType<CodexThreadItemFileChange>().Should().ContainSingle().Subject;
        fileChange.Changes[0].Kind.Type.Should().Be(CodexPatchChangeKindType.Add);
        fileChange.Changes[1].Kind.Type.Should().Be(CodexPatchChangeKindType.Update);
        fileChange.Changes[1].Kind.MovePath.Should().Be("c.txt");

        var collab = items.OfType<CodexThreadItemCollabAgentToolCall>().Should().ContainSingle().Subject;
        collab.ReasoningEffort.Should().Be(CodexReasoningEffort.High);
        collab.AgentsStates.Should().ContainKey("child");

        var search = items.OfType<CodexThreadItemWebSearch>().Should().ContainSingle().Subject;
        search.Action.Should().NotBeNull();
        search.Action!.Kind.Should().Be(CodexWebSearchActionKind.FindInPage);
        search.Action.Url.Should().Be("https://example.com");
        search.Action.Pattern.Should().Be("cli");
    }

    [Fact]
    public void ParseReadResult_MalformedKnownThreadItem_FallsBackToUnknown()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "thread": {
                "id": "t_bad",
                "turns": [
                  {
                    "id": "turn_bad",
                    "status": "completed",
                    "items": [
                      {
                        "id": "file_bad",
                        "type": "fileChange",
                        "status": "completed",
                        "changes": [
                          {
                            "path": "a.txt",
                            "kind": {
                              "movePath": "b.txt"
                            },
                            "diff": "+++ a.txt"
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var result = CodexAppServerClientThreadResponseParsers.ParseReadResult(doc.RootElement, "fallback");

        result.Turns.Should().ContainSingle()
            .Which.Items.Should().ContainSingle()
            .Which.Should().BeOfType<CodexThreadItemUnknown>();
    }

    [Fact]
    public void ThreadRollbackResponse_ExtractsThreadId_FromThreadObject()
    {
        var raw = JsonFixtures.Load("thread-rollback-response.json");
        raw.TryGetProperty("thread", out var threadObj).Should().BeTrue();

        CodexAppServerClient.ExtractThreadId(threadObj).Should().Be("t_rb");
    }

    [Fact]
    public void ThreadUnarchiveResponse_CapturesExtensionData_ForForwardCompatibility()
    {
        var json = JsonFixtures.LoadText("thread-unarchive-response.json");
        var response = JsonSerializer.Deserialize<ThreadUnarchiveResponse>(json);

        response.Should().NotBeNull();
        response!.Thread.Should().NotBeNull();
        response.ExtensionData.Should().NotBeNull();
        response.ExtensionData!.Should().ContainKey("futureField");
    }

    [Fact]
    public void ThreadLifecycleResponses_DeserializeTypedReviewer()
    {
        var start = JsonSerializer.Deserialize<ThreadStartResponse>(JsonFixtures.LoadText("thread-start-response.json"));
        var resume = JsonSerializer.Deserialize<ThreadResumeResponse>(JsonFixtures.LoadText("thread-resume-response.json"));
        var fork = JsonSerializer.Deserialize<ThreadForkResponse>(JsonFixtures.LoadText("thread-fork-response.json"));

        start!.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.User);
        start.ServiceTier.Should().Be("fast");
        start.ExtensionData.Should().ContainKey("futureField");

        resume!.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.GuardianSubagent);
        resume.Sandbox.Should().Be("danger-full-access");
        resume.ExtensionData.Should().ContainKey("futureField");

        fork!.ApprovalsReviewer.Should().Be(CodexApprovalsReviewer.GuardianSubagent);
        fork.Sandbox.Should().Be("workspace-write");
        fork.ExtensionData.Should().ContainKey("unknown");
    }
}

