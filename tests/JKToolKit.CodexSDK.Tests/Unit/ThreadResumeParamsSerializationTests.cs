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
}
