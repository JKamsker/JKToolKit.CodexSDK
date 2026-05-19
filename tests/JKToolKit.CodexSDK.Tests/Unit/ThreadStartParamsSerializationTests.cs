using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadStartParamsSerializationTests
{
    [Fact]
    public void Serialize_OmitsExperimentalRawEvents_WhenFalse()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ExperimentalRawEvents = false },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().NotContain("experimentalRawEvents");
    }

    [Fact]
    public void Serialize_IncludesExperimentalRawEvents_WhenTrue()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ExperimentalRawEvents = true },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"experimentalRawEvents\":true");
    }

    [Fact]
    public void Serialize_WritesApprovalsReviewer_AsClosedEnum()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ApprovalsReviewer = CodexApprovalsReviewer.GuardianSubagent },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"approvalsReviewer\":\"guardian_subagent\"");
    }

    [Fact]
    public void Serialize_WritesAutoReviewApprovalsReviewer()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ApprovalsReviewer = CodexApprovalsReviewer.AutoReview },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"approvalsReviewer\":\"auto_review\"");
    }

    [Fact]
    public void Serialize_WritesSessionStartSource_WhenProvided()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { SessionStartSource = ThreadSessionStartSource.Clear.Value },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"sessionStartSource\":\"clear\"");
    }

    [Fact]
    public void Serialize_WritesRuntimeRootsEnvironmentsAndPermissions()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams
            {
                RuntimeWorkspaceRoots = ["C:/repo", "C:/repo/sub"],
                Environments =
                [
                    new TurnEnvironmentParams
                    {
                        EnvironmentId = "env-1",
                        Cwd = "C:/repo"
                    }
                ],
                Permissions = "profile-1"
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"runtimeWorkspaceRoots\":[\"C:/repo\",\"C:/repo/sub\"]");
        json.Should().Contain("\"environments\":[{\"environmentId\":\"env-1\",\"cwd\":\"C:/repo\"}]");
        json.Should().Contain("\"permissions\":\"profile-1\"");
    }
}
