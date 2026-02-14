using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ConfigRequirementsParsingTests
{
    private static JsonElement LoadFixture(string name)
    {
        var relative = Path.Combine("Fixtures", name);
        var fullPath = Path.Combine(AppContext.BaseDirectory, relative);

        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "JKToolKit.CodexSDK.Tests", relative);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ParseConfigRequirementsReadRequirements_ExtractsTypedFields_AndPreservesRaw()
    {
        var raw = LoadFixture("config-requirements-read-response.json");

        var requirements = CodexAppServerClient.ParseConfigRequirementsReadRequirements(raw, experimentalApiEnabled: true);
        requirements.Should().NotBeNull();

        requirements!.AllowedApprovalPolicies!.Select(p => p.Value).Should().Equal("never", "on-request");
        requirements.AllowedSandboxModes!.Select(m => m.Value).Should().Equal("read-only", "workspace-write");
        requirements.AllowedWebSearchModes!.Select(m => m.Value).Should().Equal("disabled", "cached", "live");
        requirements.EnforceResidency!.Value.Value.Should().Be("us");

        requirements.Network.Should().NotBeNull();
        requirements.Network!.HttpPort.Should().Be(8080);
        requirements.Network.Raw.TryGetProperty("unknownField", out _).Should().BeTrue();

        requirements.Raw.TryGetProperty("unknownTopLevelField", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseConfigRequirementsReadRequirements_DoesNotPopulateNetwork_WhenExperimentalApiDisabled()
    {
        var raw = LoadFixture("config-requirements-read-response.json");

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

