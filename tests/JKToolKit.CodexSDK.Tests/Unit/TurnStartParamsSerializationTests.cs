using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class TurnStartParamsSerializationTests
{
    [Fact]
    public void Serialize_WritesRuntimeRootsEnvironmentsAndPermissions()
    {
        var json = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                Input = [],
                RuntimeWorkspaceRoots = ["C:/repo"],
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

        json.Should().Contain("\"runtimeWorkspaceRoots\":[\"C:/repo\"]");
        json.Should().Contain("\"environments\":[{\"environmentId\":\"env-1\",\"cwd\":\"C:/repo\"}]");
        json.Should().Contain("\"permissions\":\"profile-1\"");
    }

    [Fact]
    public void Serialize_WritesClientMetadataAndAdditionalContext()
    {
        var json = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                Input = [TurnInputItem.Text("hello")],
                ClientUserMessageId = "msg_1",
                ResponsesApiClientMetadata = new Dictionary<string, string>
                {
                    ["workspace_kind"] = "local"
                },
                AdditionalContext = new Dictionary<string, TurnAdditionalContextEntryParams>
                {
                    ["note"] = new()
                    {
                        Value = "extra context",
                        Kind = "application"
                    }
                }
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"clientUserMessageId\":\"msg_1\"")
            .And.Contain("\"responsesapiClientMetadata\":{\"workspace_kind\":\"local\"}")
            .And.Contain("\"additionalContext\":{\"note\":{\"value\":\"extra context\",\"kind\":\"application\"}}");
    }
}
