using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

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
    public void ValidateThreadStart_Throws_WhenExperimentalRawEventsTrue_AndExperimentalDisabled()
    {
        var options = new ThreadStartOptions { ExperimentalRawEvents = true };

        Action act = () => ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: false);

        act.Should().Throw<CodexExperimentalApiRequiredException>()
            .Which.Descriptor.Should().Be("thread/start.experimentalRawEvents");
    }

    [Fact]
    public void ValidateAll_DoesNotThrow_WhenExperimentalEnabled()
    {
        using var history = JsonDocument.Parse("""{"items":[]}""");
        using var collab = JsonDocument.Parse("""{"type":"test"}""");

        Action act = () =>
        {
            ExperimentalApiGuards.ValidateThreadStart(new ThreadStartOptions { ExperimentalRawEvents = true }, experimentalApiEnabled: true);
            ExperimentalApiGuards.ValidateThreadResume(new ThreadResumeOptions { ThreadId = "t", History = history.RootElement, Path = "C:\\rollout" }, experimentalApiEnabled: true);
            ExperimentalApiGuards.ValidateThreadFork(new ThreadForkOptions { Path = "C:\\rollout" }, experimentalApiEnabled: true);
            ExperimentalApiGuards.ValidateTurnStart(new TurnStartOptions { CollaborationMode = collab.RootElement }, experimentalApiEnabled: true);
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
}
