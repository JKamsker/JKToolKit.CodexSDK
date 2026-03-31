using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExperimentalApiGuardsTests
{
    [Fact]
    public void ValidateThreadResume_Throws_WhenHistorySet_AndExperimentalDisabled()
    {
        using var doc = JsonDocument.Parse("""{"items":[]}""");
        var options = new ThreadResumeOptions { ThreadId = "t", History = doc.RootElement };

        Action act = () => ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/resume.history");
    }

    [Fact]
    public void ValidateThreadResume_Throws_WhenPathSet_AndExperimentalDisabled()
    {
        var options = new ThreadResumeOptions { ThreadId = "t", Path = "C:\\rollout" };

        Action act = () => ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/resume.path");
    }

    [Fact]
    public void ValidateTurnStart_Throws_WhenCollaborationModeSet_AndExperimentalDisabled()
    {
        using var doc = JsonDocument.Parse("""{"type":"test"}""");
        var options = new TurnStartOptions { CollaborationMode = doc.RootElement };

        Action act = () => ExperimentalApiGuards.ValidateTurnStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("turn/start.collaborationMode");
    }

    [Fact]
    public void ValidateThreadStart_Throws_WhenAskForApprovalGranularSet_AndExperimentalDisabled()
    {
        var options = new ThreadStartOptions
        {
            AskForApproval = new CodexAskForApprovalGranular
            {
                SandboxApproval = true,
                Rules = false,
                RequestPermissions = true,
                McpElicitations = true
            }
        };

        Action act = () => ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("askForApproval.granular");
    }

    [Fact]
    public void ValidateThreadResume_Throws_WhenAskForApprovalGranularSet_AndExperimentalDisabled()
    {
        var options = new ThreadResumeOptions
        {
            ThreadId = "t",
            AskForApproval = new CodexAskForApprovalGranular
            {
                SandboxApproval = true,
                Rules = true,
                McpElicitations = false
            }
        };

        Action act = () => ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("askForApproval.granular");
    }

    [Fact]
    public void ValidateTurnStart_Throws_WhenAskForApprovalGranularSet_AndExperimentalDisabled()
    {
        var options = new TurnStartOptions
        {
            AskForApproval = new CodexAskForApprovalGranular
            {
                SandboxApproval = true,
                Rules = false,
                SkillApproval = true,
                McpElicitations = true
            }
        };

        Action act = () => ExperimentalApiGuards.ValidateTurnStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("askForApproval.granular");
    }

    [Fact]
    public void ValidateThreadStart_Throws_WhenExperimentalRawEventsTrue_AndExperimentalDisabled()
    {
        var options = new ThreadStartOptions { ExperimentalRawEvents = true };

        Action act = () => ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/start.experimentalRawEvents");
    }

    [Fact]
    public void ValidateThreadStart_Throws_WhenDynamicToolsSet_AndExperimentalDisabled()
    {
        using var schema = JsonDocument.Parse("""{"type":"object"}""");

        var options = new ThreadStartOptions
        {
            DynamicTools =
            [
                new JKToolKit.CodexSDK.AppServer.Protocol.V2.DynamicToolSpec
                {
                    Name = "echo",
                    Description = "Echo",
                    InputSchema = schema.RootElement.Clone()
                }
            ]
        };

        Action act = () => ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/start.dynamicTools");
    }

    [Fact]
    public void ValidateThreadStart_Throws_WhenPersistExtendedHistoryTrue_AndExperimentalDisabled()
    {
        var options = new ThreadStartOptions { PersistExtendedHistory = true };

        Action act = () => ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/start.persistFullHistory");
    }

    [Fact]
    public void ValidateAll_DoesNotThrow_WhenExperimentalEnabled()
    {
        using var history = JsonDocument.Parse("""{"items":[]}""");
        using var collab = JsonDocument.Parse("""{"type":"test"}""");
        using var schema = JsonDocument.Parse("""{"type":"object"}""");

        Action act = () =>
        {
            ExperimentalApiGuards.ValidateThreadStart(new ThreadStartOptions
            {
                AskForApproval = new CodexAskForApprovalGranular
                {
                    SandboxApproval = true,
                    Rules = false,
                    RequestPermissions = true,
                    McpElicitations = true
                },
                ExperimentalRawEvents = true,
                PersistExtendedHistory = true,
                DynamicTools =
                [
                    new JKToolKit.CodexSDK.AppServer.Protocol.V2.DynamicToolSpec
                    {
                        Name = "echo",
                        Description = "Echo",
                        InputSchema = schema.RootElement.Clone()
                    }
                ]
            }, experimentalApiEnabled: true);

            ExperimentalApiGuards.ValidateThreadResume(new ThreadResumeOptions
            {
                ThreadId = "t",
                AskForApproval = new CodexAskForApprovalGranular
                {
                    SandboxApproval = true,
                    Rules = false,
                    SkillApproval = true,
                    McpElicitations = true
                },
                History = history.RootElement,
                Path = "C:\\rollout",
                PersistExtendedHistory = true
            }, experimentalApiEnabled: true);

            ExperimentalApiGuards.ValidateThreadFork(new ThreadForkOptions
            {
                Path = "C:\\rollout",
                PersistExtendedHistory = true
            }, experimentalApiEnabled: true);
            ExperimentalApiGuards.ValidateTurnStart(new TurnStartOptions
            {
                AskForApproval = new CodexAskForApprovalGranular
                {
                    SandboxApproval = true,
                    Rules = false,
                    RequestPermissions = true,
                    McpElicitations = true
                },
                CollaborationMode = collab.RootElement
            }, experimentalApiEnabled: true);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateThreadFork_Throws_WhenPathSet_AndExperimentalDisabled()
    {
        var options = new ThreadForkOptions { Path = "C:\\rollout" };

        Action act = () => ExperimentalApiGuards.ValidateThreadFork(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/fork.path");
    }

    [Fact]
    public void ValidateThreadFork_Throws_WhenPersistExtendedHistoryTrue_AndExperimentalDisabled()
    {
        var options = new ThreadForkOptions { ThreadId = "thr_1", PersistExtendedHistory = true };

        Action act = () => ExperimentalApiGuards.ValidateThreadFork(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/fork.persistFullHistory");
    }

    [Fact]
    public void ValidateThreadResume_Throws_WhenPersistExtendedHistoryTrue_AndExperimentalDisabled()
    {
        var options = new ThreadResumeOptions { ThreadId = "t", PersistExtendedHistory = true };

        Action act = () => ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/resume.persistFullHistory");
    }

    [Fact]
    public void ValidateThreadFork_Throws_WhenNeitherThreadIdNorPathSet()
    {
        var options = new ThreadForkOptions();

        Action act = () => ExperimentalApiGuards.ValidateThreadFork(options, experimentalApiEnabled: false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateThreadFork_Throws_WhenBothThreadIdAndPathSet()
    {
        var options = new ThreadForkOptions { ThreadId = "thr_123", Path = "C:\\rollout" };

        Action act = () => ExperimentalApiGuards.ValidateThreadFork(options, experimentalApiEnabled: true);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Specify either ThreadId or Path, not both.*");
    }

    [Fact]
    public void ValidateThreadFork_DoesNotThrow_WhenThreadIdSet_AndExperimentalDisabled()
    {
        var options = new ThreadForkOptions { ThreadId = "thr_123" };

        Action act = () => ExperimentalApiGuards.ValidateThreadFork(options, experimentalApiEnabled: false);

        act.Should().NotThrow();
    }
}
