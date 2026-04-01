using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerCollaborationModePayloadBuilderTests
{
    [Fact]
    public void TryGetFirstMask_ReadsNestedSettings_AndExplicitNullDeveloperInstructions()
    {
        using var doc = JsonDocument.Parse("""
        {
          "data": [
            {
              "name": "default",
              "mode": "plan",
              "settings": {
                "model": "gpt-5",
                "reasoning_effort": "medium",
                "developer_instructions": null
              }
            }
          ]
        }
        """);

        var ok = AppServerCollaborationModePayloadBuilder.TryGetFirstMask(doc.RootElement, out var mask);

        ok.Should().BeTrue();
        mask.Mode.Should().Be("plan");
        mask.Model.Should().Be("gpt-5");
        mask.ReasoningEffort.Should().Be("medium");
        mask.IncludesDeveloperInstructions.Should().BeTrue();
        mask.DeveloperInstructions.Should().BeNull();
    }

    [Fact]
    public void BuildCollaborationModeJson_PreservesExplicitNullDeveloperInstructions()
    {
        var payload = AppServerCollaborationModePayloadBuilder.BuildCollaborationModeJson(new CollaborationModeMaskProjection
        {
            Mode = "plan",
            Model = "gpt-5",
            IncludesDeveloperInstructions = true
        });

        payload.GetProperty("settings").TryGetProperty("developer_instructions", out var developerInstructions).Should().BeTrue();
        developerInstructions.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void BuildCollaborationModeJson_OmitsDeveloperInstructions_WhenMaskDoesNotIncludeIt()
    {
        var payload = AppServerCollaborationModePayloadBuilder.BuildCollaborationModeJson(new CollaborationModeMaskProjection
        {
            Mode = "plan",
            Model = "gpt-5"
        });

        payload.GetProperty("settings").TryGetProperty("developer_instructions", out _).Should().BeFalse();
    }
}
