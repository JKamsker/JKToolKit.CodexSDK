using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteSkillsParsingTests
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
    public void ParseRemoteSkillsReadSkills_ExtractsFields_AndPreservesRaw()
    {
        var raw = LoadFixture("skills-remote-read-response.json");
        var skills = CodexAppServerClient.ParseRemoteSkillsReadSkills(raw);

        skills.Should().HaveCount(1);
        skills[0].Id.Should().Be("remote_1");
        skills[0].Name.Should().Be("RemoteSkill");
        skills[0].Description.Should().Be("remote desc");
        skills[0].Raw.TryGetProperty("unknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ParseRemoteSkillsReadSkills_ReturnsEmptyList_OnUnexpectedShape()
    {
        using var doc = JsonDocument.Parse("{\"data\": null}");
        var skills = CodexAppServerClient.ParseRemoteSkillsReadSkills(doc.RootElement);

        skills.Should().BeEmpty();
    }
}

