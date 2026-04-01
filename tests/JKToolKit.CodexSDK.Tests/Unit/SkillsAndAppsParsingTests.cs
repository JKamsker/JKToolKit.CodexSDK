using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SkillsAndAppsParsingTests
{
    [Fact]
    public void ParseSkillsListSkills_ExtractsName_AndPreservesRaw()
    {
        var raw = JsonFixtures.Load("skills-list-response.json");
        var skills = CodexAppServerClient.ParseSkillsListSkills(raw);

        skills.Select(s => s.Name).Should().Equal("tasklist-executor", "fallback-id");
        skills[0].Enabled.Should().BeTrue();
        skills[0].Cwd.Should().Be("C:\\repo");
        skills[0].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseAppsListApps_ExtractsIds_AndDisabledReason()
    {
        var raw = JsonFixtures.Load("app-list-response.json");
        var apps = CodexAppServerClient.ParseAppsListApps(raw);

        apps.Select(a => a.Id).Should().Equal("app_1", "app_2");
        apps[1].IsEnabled.Should().BeFalse();
        apps[1].DisabledReason.Should().Be("authRequired");
        apps[0].LogoUrl.Should().NotBeNullOrWhiteSpace();
        apps[0].PluginDisplayNames.Should().Equal("GitHub", "GitHub Enterprise");
        apps[0].AppMetadata.Should().NotBeNull();
        apps[0].AppMetadata!.Value.TryGetProperty("version", out var version).Should().BeTrue();
        version.GetString().Should().Be("1.2.3");
        apps[0].Branding.Should().NotBeNull();
        apps[0].Branding!.Value.TryGetProperty("developer", out var developer).Should().BeTrue();
        developer.GetString().Should().Be("Example Corp");
        apps[0].Labels.Should().NotBeNull();
        var labels = apps[0].Labels!;
        labels["category"].Should().Be("developer-tools");
        labels["tier"].Should().Be("official");
        labels.ContainsKey("ignored").Should().BeFalse();
        apps[1].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseAppsListApps_DefaultsMissingPluginDisplayNamesToEmpty()
    {
        using var doc = JsonDocument.Parse("""{"data":[{"id":"app_1","name":"Calendar","isEnabled":true}]}""");

        var apps = CodexAppServerClient.ParseAppsListApps(doc.RootElement);

        apps.Should().ContainSingle();
        apps[0].PluginDisplayNames.Should().BeEmpty();
    }
}

