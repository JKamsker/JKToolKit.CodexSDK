using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadResumeParamsSerializationTests
{
    [Fact]
    public void Serialize_WritesApprovalsReviewer_AsClosedEnum()
    {
        var json = JsonSerializer.Serialize(
            new ThreadResumeParams
            {
                ThreadId = "thr_123",
                ApprovalsReviewer = CodexApprovalsReviewer.GuardianSubagent
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"approvalsReviewer\":\"guardian_subagent\"");
    }

    [Fact]
    public void Serialize_WritesRuntimeRootsAndPermissions()
    {
        var json = JsonSerializer.Serialize(
            new ThreadResumeParams
            {
                ThreadId = "thr_123",
                RuntimeWorkspaceRoots = ["C:/repo"],
                Permissions = "profile-1"
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"runtimeWorkspaceRoots\":[\"C:/repo\"]");
        json.Should().Contain("\"permissions\":\"profile-1\"");
    }

    [Fact]
    public void Serialize_WritesInitialTurnsPage()
    {
        var json = JsonSerializer.Serialize(
            new ThreadResumeParams
            {
                ThreadId = "thr_123",
                InitialTurnsPage = new ThreadResumeInitialTurnsPageParams
                {
                    Limit = 25,
                    SortDirection = "desc",
                    ItemsView = "summary"
                }
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"initialTurnsPage\":{")
            .And.Contain("\"limit\":25")
            .And.Contain("\"sortDirection\":\"desc\"")
            .And.Contain("\"itemsView\":\"summary\"");
    }
}
