using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SkillsAndAppsParsingTests
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
    public void ParseSkillsListSkills_ExtractsName_AndPreservesRaw()
    {
        var raw = LoadFixture("skills-list-response.json");
        var skills = CodexAppServerClient.ParseSkillsListSkills(raw);

        skills.Select(s => s.Name).Should().Equal("tasklist-executor", "fallback-id");
        skills[0].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseAppsListApps_ExtractsIds_AndDisabledReason()
    {
        var raw = LoadFixture("app-list-response.json");
        var apps = CodexAppServerClient.ParseAppsListApps(raw);

        apps.Select(a => a.Id).Should().Equal("app_1", "app_2");
        apps[1].Enabled.Should().BeFalse();
        apps[1].DisabledReason.Should().Be("authRequired");
        apps[1].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }
}

