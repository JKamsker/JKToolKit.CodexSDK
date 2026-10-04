using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerCatalogParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("skills")]
    [InlineData("items")]
    public void LegacySkills_MapsMetadataAndFiltersMalformedEntries(string arrayName)
    {
        var raw = Json("{\"" + arrayName + "\":" + """
            [null,42,{}, {"name":" "},{"id":"legacy","description":"long","shortDescription":"short","path":"/skill","enabled":false,"scope":"user","dependencies":{"tools":[]},"interface":{"displayName":"Skill"}}]}
            """);
        var entry = CodexAppServerClientSkillsAppsParsers.ParseSkillsListEntries(raw).Should().ContainSingle().Subject;
        entry.Cwd.Should().BeNull(); entry.Errors.Should().BeEmpty(); entry.Raw.GetRawText().Should().Be(raw.GetRawText());
        var skill = entry.Skills.Should().ContainSingle().Subject;
        skill.Name.Should().Be("legacy"); skill.Description.Should().Be("long"); skill.ShortDescription.Should().Be("short");
        skill.Path.Should().Be("/skill"); skill.Enabled.Should().BeFalse(); skill.Scope.Should().Be("user"); skill.Cwd.Should().BeNull();
        skill.Dependencies!.Value.GetProperty("tools").GetArrayLength().Should().Be(0);
        skill.Interface!.Value.GetProperty("displayName").GetString().Should().Be("Skill");
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(raw).Should().ContainSingle().Which.Name.Should().Be("legacy");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"skills\":false,\"items\":null}")]
    public void MissingSkillCollectionsAreEmpty(string json)
    {
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListEntries(Json(json)).Should().BeEmpty();
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(Json(json)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"apps\":[null]}", "entries must be objects")]
    [InlineData("{}", "no apps array")]
    public void StrictAppCollections_RejectMissingAndNonObjectEntries(string json, string error)
    {
        Action read = () => CodexAppServerClientSkillsAppsParsers.ParseAppsReadApps(Json(json));
        Action installed = () => CodexAppServerClientSkillsAppsParsers.ParseInstalledApps(Json(json));
        read.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
        installed.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Fact]
    public void AppRead_ToolsDefaultCapabilitiesAndCloneRawPayload()
    {
        IReadOnlyList<AppConnectorMetadata> apps;
        using (var document = JsonDocument.Parse("""{"apps":[{"id":"a","name":"App","toolSummaries":[{"name":"lookup","description":"Search"},{"name":"write","description":"Write","isEnabled":false,"isReadOnly":true,"disabledReason":"policy","title":"Write tool"}]}]}"""))
            apps = CodexAppServerClientSkillsAppsParsers.ParseAppsReadApps(document.RootElement);
        var first = apps[0].ToolSummaries[0];
        first.Name.Should().Be("lookup"); first.Description.Should().Be("Search"); first.IsEnabled.Should().BeTrue(); first.IsReadOnly.Should().BeFalse();
        var second = apps[0].ToolSummaries[1];
        second.IsEnabled.Should().BeFalse(); second.IsReadOnly.Should().BeTrue(); second.DisabledReason.Should().Be("policy"); second.Title.Should().Be("Write tool");
        second.Raw.GetProperty("name").GetString().Should().Be("write");
        apps[0].Raw.GetProperty("id").GetString().Should().Be("a");
    }

    [Fact]
    public void AppRead_MalformedToolSummaryThrows()
    {
        Action parse = () => CodexAppServerClientSkillsAppsParsers.ParseAppsReadApps(Json("""{"apps":[{"id":"a","name":"App","toolSummaries":[null]}]}"""));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*toolSummaries[] entries must be objects*");
    }

    [Theory]
    [InlineData("data")]
    [InlineData("apps")]
    [InlineData("items")]
    public void AppList_MapsCollectionAndLegacyAliases(string collection)
    {
        var app = CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(Json("{\"" + collection + "\":" + """[null,{"id":"a","logo_url":"light","logo_url_dark":"dark","enabled":false,"labels":{"valid":"v","invalid":42}}]}"""))
            .Should().ContainSingle().Subject;
        app.Id.Should().Be("a"); app.LogoUrl.Should().Be("light"); app.LogoUrlDark.Should().Be("dark"); app.IsEnabled.Should().BeFalse();
        app.Labels.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string,string>("valid", "v"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"a\":null}")]
    public void AppList_EmptyOrNonStringLabelsAreAbsent(string labels)
    {
        var app = CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(Json("{\"apps\":[{\"labels\":" + labels + "}]}"))[0];
        app.Labels.Should().BeNull();
    }

    [Theory]
    [InlineData("active", ThreadGoalStatus.Active)]
    [InlineData("paused", ThreadGoalStatus.Paused)]
    [InlineData("blocked", ThreadGoalStatus.Blocked)]
    [InlineData("usageLimited", ThreadGoalStatus.UsageLimited)]
    [InlineData("budgetLimited", ThreadGoalStatus.BudgetLimited)]
    [InlineData("complete", ThreadGoalStatus.Complete)]
    public void GoalStatus_RoundTripsKnownValues(string wire, ThreadGoalStatus status)
    {
        CodexAppServerThreadManagementParsers.ParseThreadGoalStatus(wire).Should().Be(status);
        CodexAppServerThreadManagementParsers.FormatThreadGoalStatus(status).Should().Be(wire);
    }

    [Fact]
    public void GoalStatus_UnknownInboundIsPreservedButCannotBeSent()
    {
        var goal = CodexAppServerThreadManagementParsers.ParseThreadGoal(Json("""{"threadId":"t","objective":"","status":"future"}"""))!;
        goal.Status.Should().Be(ThreadGoalStatus.Unknown); goal.StatusValue.Should().Be("future"); goal.Objective.Should().BeEmpty();
        goal.TokenBudget.Should().BeNull(); goal.TokensUsed.Should().Be(0); goal.TimeUsedSeconds.Should().Be(0); goal.CreatedAt.Should().Be(0); goal.UpdatedAt.Should().Be(0);
        Action format = () => CodexAppServerThreadManagementParsers.FormatThreadGoalStatus(ThreadGoalStatus.Unknown);
        format.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("status");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"threadId\":\" \",\"objective\":\"o\",\"status\":\"active\"}")]
    [InlineData("{\"threadId\":\"t\",\"status\":\"active\"}")]
    [InlineData("{\"threadId\":\"t\",\"objective\":\"o\",\"status\":\" \"}")]
    public void Goal_InvalidRequiredFieldsReturnNull(string json) =>
        CodexAppServerThreadManagementParsers.ParseThreadGoal(Json(json)).Should().BeNull();

    [Fact]
    public void PermissionProfiles_IgnoreInvalidIdsAndCloneRetainedPayloads()
    {
        PermissionProfileListPage page;
        using (var document = JsonDocument.Parse("""{"data":[null,{}, {"id":" "},{"id":"p","description":"Profile"}],"nextCursor":"next"}"""))
            page = CodexAppServerThreadManagementParsers.ParsePermissionProfiles(document.RootElement);
        page.NextCursor.Should().Be("next");
        var profile = page.Profiles.Should().ContainSingle().Subject;
        profile.Id.Should().Be("p"); profile.Description.Should().Be("Profile"); profile.Raw.GetProperty("id").GetString().Should().Be("p");
    }
}
