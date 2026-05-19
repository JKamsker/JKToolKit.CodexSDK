using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ConfigRequirementsParsingTests
{
    [Fact]
    public void ParseConfigRequirementsReadRequirements_ExtractsTypedFields_AndPreservesRaw()
    {
        var raw = JsonFixtures.Load("config-requirements-read-response.json");

        var requirements = CodexAppServerClient.ParseConfigRequirementsReadRequirements(raw, experimentalApiEnabled: true);
        requirements.Should().NotBeNull();

        requirements!.AllowedApprovalPolicies!.Select(p => p.Value).Should().Equal("never", "on-request");
        requirements.AllowedAskForApproval.Should().NotBeNull();
        requirements.AllowedAskForApproval!.Select(a => a.Policy!.Value).Should().Equal("never", "on-request");
        requirements.AllowedSandboxModes!.Select(m => m.Value).Should().Equal("read-only", "workspace-write");
        requirements.AllowedWebSearchModes!.Select(m => m.Value).Should().Equal("disabled", "cached", "live");
        requirements.FeatureRequirements.Should().NotBeNull();
        requirements.FeatureRequirements!["apps"].Should().BeTrue();
        requirements.FeatureRequirements["network"].Should().BeFalse();
        requirements.FeatureRequirements.ContainsKey("ignoreMe").Should().BeFalse();
        requirements.AllowManagedHooksOnly.Should().BeTrue();
        requirements.EnforceResidency!.Value.Value.Should().Be("us");

        requirements.Network.Should().NotBeNull();
        requirements.Network!.HttpPort.Should().Be(8080);
#pragma warning disable CS0618
        requirements.Network.DangerouslyAllowNonLoopbackAdmin.Should().BeNull();
#pragma warning restore CS0618
        requirements.Network.Raw.TryGetProperty("unknownField", out _).Should().BeTrue();
        requirements.Network.Raw.TryGetProperty("dangerouslyAllowNonLoopbackAdmin", out _).Should().BeTrue();

        requirements.Raw.TryGetProperty("unknownTopLevelField", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseConfigRequirementsReadRequirements_PopulatesAskForApprovalUnion()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "requirements": {
                "allowedApprovalPolicies": [
                  "never",
                  {
                    "granular": {
                      "sandbox_approval": true,
                      "rules": false,
                      "mcp_elicitations": true,
                      "skill_approval": true,
                      "request_permissions": false
                    }
                  }
                ]
              }
            }
            """);

        var requirements = CodexAppServerClient.ParseConfigRequirementsReadRequirements(doc.RootElement, experimentalApiEnabled: true);
        requirements.Should().NotBeNull();
        requirements!.AllowedApprovalPolicies!.Select(p => p.Value).Should().Equal("never");
        requirements.AllowedAskForApproval.Should().NotBeNull();
        requirements.AllowedAskForApproval!.Should().HaveCount(2);
        var granular = requirements.AllowedAskForApproval[1].Granular;
        granular.Should().NotBeNull();
        granular!.SandboxApproval.Should().BeTrue();
        granular.Rules.Should().BeFalse();
        granular.McpElicitations.Should().BeTrue();
        granular.SkillApproval.Should().BeTrue();
        granular.RequestPermissions.Should().BeFalse();
    }

    [Fact]
    public void ParseConfigRequirementsReadRequirements_DoesNotPopulateNetwork_WhenExperimentalApiDisabled()
    {
        var raw = JsonFixtures.Load("config-requirements-read-response.json");

        var requirements = CodexAppServerClient.ParseConfigRequirementsReadRequirements(raw, experimentalApiEnabled: false);
        requirements.Should().NotBeNull();
        requirements!.Network.Should().BeNull();
        requirements.Raw.TryGetProperty("network", out _).Should().BeTrue("raw should preserve forward-compatible fields");
    }

    [Fact]
    public void ParseConfigRequirementsReadRequirements_ReturnsNull_WhenRequirementsMissing()
    {
        using var doc = JsonDocument.Parse("{\"requirements\": null}");
        var requirements = CodexAppServerClient.ParseConfigRequirementsReadRequirements(doc.RootElement, experimentalApiEnabled: true);
        requirements.Should().BeNull();
    }
}
