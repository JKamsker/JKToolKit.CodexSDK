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
        apps[1].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }
}

