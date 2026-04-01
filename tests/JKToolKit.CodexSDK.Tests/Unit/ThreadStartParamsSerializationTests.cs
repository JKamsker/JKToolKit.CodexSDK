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
}
