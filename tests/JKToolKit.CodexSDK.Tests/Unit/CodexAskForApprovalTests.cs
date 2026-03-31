using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexAskForApprovalTests
{
    private static readonly JsonSerializerOptions WireJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ToWireValue_ReturnsPolicyString_WhenPolicyConfigured()
    {
        var value = CodexAskForApproval.FromPolicy(CodexApprovalPolicy.OnRequest).ToWireValue();

        value.Should().Be("on-request");
    }

    [Fact]
    public void ToWireValue_SerializesObjectForm_AsGranular()
    {
        var askForApproval = CodexAskForApproval.FromGranular(new CodexAskForApprovalGranular
        {
            SandboxApproval = true,
            Rules = false,
            SkillApproval = true,
            RequestPermissions = true,
            McpElicitations = false
        });

        var json = JsonSerializer.Serialize(askForApproval.ToWireValue(), WireJsonOptions);

        json.Should().Contain("\"granular\"");
        json.Should().Contain("\"sandbox_approval\":true");
        json.Should().Contain("\"rules\":false");
        json.Should().Contain("\"skill_approval\":true");
        json.Should().Contain("\"request_permissions\":true");
        json.Should().Contain("\"mcp_elicitations\":false");
        json.Should().NotContain("\"reject\"");
    }

    [Fact]
    public void Rejecting_LegacyFactory_MapsToGranularShape()
    {
        var askForApproval = CodexAskForApproval.Rejecting(new CodexAskForApprovalReject
        {
            SandboxApproval = false,
            Rules = true,
            McpElicitations = false
        });

        askForApproval.Granular.Should().NotBeNull();
        askForApproval.Granular!.SandboxApproval.Should().BeTrue();
        askForApproval.Granular.Rules.Should().BeFalse();
        askForApproval.Granular.McpElicitations.Should().BeTrue();
        askForApproval.Granular.SkillApproval.Should().BeTrue();
        askForApproval.Granular.RequestPermissions.Should().BeTrue();

        var json = JsonSerializer.Serialize(askForApproval.ToWireValue(), WireJsonOptions);
        json.Should().Contain("\"granular\"");
        json.Should().NotContain("\"reject\"");
    }

    [Fact]
    public void BuildAskForApproval_PrefersAskForApprovalObjectOverStringPolicy()
    {
        var askForApproval = CodexAskForApproval.FromGranular(new CodexAskForApprovalGranular
        {
            SandboxApproval = true,
            Rules = false,
            McpElicitations = true
        });

        var value = CodexAppServerAskForApprovalWiring.BuildAskForApproval(askForApproval, CodexApprovalPolicy.Never);
        var json = JsonSerializer.Serialize(value, WireJsonOptions);

        json.Should().Contain("\"granular\"");
        json.Should().NotContain("\"never\"");
    }

    [Fact]
    public void Reject_ReturnsLegacyInverseSubset_ForGranularObjectForm()
    {
        var askForApproval = CodexAskForApproval.FromGranular(new CodexAskForApprovalGranular
        {
            SandboxApproval = true,
            Rules = false,
            SkillApproval = false,
            RequestPermissions = true,
            McpElicitations = false
        });

        askForApproval.Reject.Should().NotBeNull();
        askForApproval.Reject!.SandboxApproval.Should().BeFalse();
        askForApproval.Reject.Rules.Should().BeTrue();
        askForApproval.Reject.McpElicitations.Should().BeTrue();
    }
}
