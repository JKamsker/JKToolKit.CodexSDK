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
                ResponsesApiClientMetadataPatch = new Dictionary<string, string>
                {
                    ["request_kind"] = "followup"
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
            .And.Contain("\"responsesapiClientMetadataPatch\":{\"request_kind\":\"followup\"}")
            .And.Contain("\"additionalContext\":{\"note\":{\"value\":\"extra context\",\"kind\":\"application\"}}");
    }

    [Fact]
    public void Serialize_WritesStableTurnTriggerServiceTierForTurnAndToolOutput()
    {
        var json = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                Input = [],
                TurnTrigger = "toolResult",
                ParentTurnId = "turn-parent",
                RootTurnId = "turn-root",
                ServiceTierForTurn = "default",
                ToolOutput = TurnToolOutput.Text("memory_lookup", "Alice mentioned you.", "memories")
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"turnTrigger\":\"toolResult\"")
            .And.Contain("\"parentTurnId\":\"turn-parent\"")
            .And.Contain("\"rootTurnId\":\"turn-root\"")
            .And.Contain("\"serviceTierForTurn\":\"default\"")
            .And.Contain("\"toolOutput\":{\"name\":\"memory_lookup\",\"namespace\":\"memories\",\"output\":\"Alice mentioned you.\"}");
    }

    [Fact]
    public void Serialize_WritesRetainedToolOutputOnlyWhenRequested()
    {
        var retained = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                Input = [],
                ToolOutput = new TurnToolOutput
                {
                    Name = "memory_lookup",
                    Output = JsonSerializer.SerializeToElement("result"),
                    Retain = true
                }
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        retained.Should().Contain("\"retain\":true");

        var transient = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                Input = [],
                ToolOutput = TurnToolOutput.Text("memory_lookup", "result")
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        transient.Should().NotContain("\"retain\"");
    }

    [Fact]
    public void Serialize_WritesDisabledPluginsAndImageFileReference()
    {
        var json = JsonSerializer.Serialize(
            new TurnStartParams
            {
                ThreadId = "thr_123",
                DisabledPluginIds = ["plugin-a"],
                Input = [TurnInputItem.ImageFile("file_123")]
            },
            CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"disabledPluginIds\":[\"plugin-a\"]")
            .And.Contain("\"type\":\"image\"")
            .And.Contain("\"fileId\":\"file_123\"")
            .And.NotContain("\"url\"");
    }
}
