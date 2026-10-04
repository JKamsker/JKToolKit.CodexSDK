using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerPluginParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);
    private static JsonElement Element(JsonNode node) => Json(node.ToJsonString());
    private static JsonObject Summary() => JsonNode.Parse("""{"id":"plug","name":"Plugin","installed":true,"enabled":false,"authPolicy":"ON_USE","installPolicy":"AVAILABLE","source":{"type":"remote","path":"relative/package","url":"https://plugin.test","refName":"main","sha":"abc"}}""")!.AsObject();
    private static JsonObject Detail() => new()
    {
        ["summary"] = Summary(), ["marketplaceName"] = "market", ["skills"] = new JsonArray(),
        ["apps"] = new JsonArray(), ["mcpServers"] = new JsonArray()
    };

    [Fact]
    public void Summary_MapsSourceAndForwardCompatiblePolicyValuesAndDefaults()
    {
        var raw = Summary();
        raw["remotePluginId"] = "remote"; raw["version"] = "2"; raw["localVersion"] = "1";
        raw["installedAt"] = "123"; raw["authPolicy"] = "future-auth"; raw["installPolicy"] = "future-install";
        raw["installPolicySource"] = "future-policy-source"; raw["disabledReason"] = "future-disabled";
        raw["availability"] = "future-availability"; raw["eligiblePlanTypes"] = new JsonArray("pro", "team");
        raw["keywords"] = new JsonArray("one", "two");
        var plugin = CodexAppServerClientPluginParsers.ParsePluginSummary(Element(raw));
        plugin.Id.Should().Be("plug"); plugin.Name.Should().Be("Plugin"); plugin.Installed.Should().BeTrue(); plugin.Enabled.Should().BeFalse();
        plugin.RemotePluginId.Should().Be("remote"); plugin.Version.Should().Be("2"); plugin.LocalVersion.Should().Be("1");
        plugin.InstalledAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(123));
        plugin.AuthPolicy.Should().Be("future-auth"); plugin.AuthPolicyValue.Value.Should().Be("future-auth");
        plugin.InstallPolicy.Should().Be("future-install"); plugin.InstallPolicyValue.Value.Should().Be("future-install");
        plugin.InstallPolicySource.Should().Be("future-policy-source"); plugin.InstallPolicySourceValue!.Value.Value.Should().Be("future-policy-source");
        plugin.DisabledReason.Should().Be("future-disabled"); plugin.DisabledReasonValue!.Value.Value.Should().Be("future-disabled");
        plugin.Availability.Should().Be("future-availability"); plugin.AvailabilityValue.Value.Should().Be("future-availability");
        plugin.EligiblePlanTypes.Should().Equal("pro", "team"); plugin.Keywords.Should().Equal("one", "two");
        plugin.SourceInfo!.Type.Should().Be(PluginSourceType.Remote); plugin.SourceInfo.Path.Should().Be("relative/package");
        plugin.SourceInfo.Url.Should().Be("https://plugin.test"); plugin.SourceInfo.RefName.Should().Be("main"); plugin.SourceInfo.Sha.Should().Be("abc");
        plugin.Source.GetProperty("type").GetString().Should().Be("remote");
        var minimal = CodexAppServerClientPluginParsers.ParsePluginSummary(Element(Summary()));
        minimal.AvailabilityValue.Should().Be(PluginAvailability.Available); minimal.InstalledAt.Should().BeNull();
        minimal.InstallPolicySourceValue.Should().BeNull(); minimal.DisabledReasonValue.Should().BeNull();
        minimal.Interface.Should().BeNull(); minimal.ShareContext.Should().BeNull(); minimal.Keywords.Should().BeEmpty();
    }

    [Fact]
    public void Summary_MapsAndClonesAllInterfaceMetadata()
    {
        var raw = Summary();
        raw["interface"] = JsonSerializer.SerializeToNode(new
        {
            displayName = "Display", shortDescription = "Short", longDescription = "Long", category = "Tools", developerName = "Developer", brandColor = "#123456",
            defaultPrompt = new[] { "Prompt" }, capabilities = new[] { "read" }, screenshots = new[] { XPaths.Abs("shot.png") }, screenshotUrls = new[] { "https://shot.test" },
            privacyPolicyUrl = "https://privacy.test", termsOfServiceUrl = "https://terms.test", websiteUrl = "https://web.test",
            composerIcon = XPaths.Abs("icon.png"), composerIconUrl = "https://icon.test", logo = XPaths.Abs("logo.png"), logoUrl = "https://logo.test"
        });
        PluginSummaryDescriptor plugin;
        using (var document = JsonDocument.Parse(raw.ToJsonString()))
            plugin = CodexAppServerClientPluginParsers.ParsePluginSummary(document.RootElement);
        var ui = plugin.Interface!;
        ui.DisplayName.Should().Be("Display"); ui.ShortDescription.Should().Be("Short"); ui.LongDescription.Should().Be("Long");
        ui.Category.Should().Be("Tools"); ui.DeveloperName.Should().Be("Developer"); ui.BrandColor.Should().Be("#123456");
        ui.DefaultPrompts.Should().Equal("Prompt"); ui.Capabilities.Should().Equal("read"); ui.Screenshots.Should().Equal(XPaths.Abs("shot.png"));
        ui.ScreenshotUrls.Should().Equal("https://shot.test"); ui.PrivacyPolicyUrl.Should().Be("https://privacy.test");
        ui.TermsOfServiceUrl.Should().Be("https://terms.test"); ui.WebsiteUrl.Should().Be("https://web.test");
        ui.ComposerIconPath.Should().Be(XPaths.Abs("icon.png")); ui.ComposerIconUrl.Should().Be("https://icon.test"); ui.ComposerIcon!.Value.GetString().Should().Be(XPaths.Abs("icon.png"));
        ui.LogoPath.Should().Be(XPaths.Abs("logo.png")); ui.LogoUrl.Should().Be("https://logo.test"); ui.Logo!.Value.GetString().Should().Be(XPaths.Abs("logo.png"));
        ui.Raw.GetProperty("category").GetString().Should().Be("Tools"); plugin.Raw.GetProperty("name").GetString().Should().Be("Plugin");
        plugin.SourceInfo!.Raw.GetProperty("sha").GetString().Should().Be("abc");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"composerIcon\":null,\"logo\":null}")]
    public void Summary_OptionalInterfaceAssetsAndListsRemainAbsent(string value)
    {
        var raw = Summary(); raw["interface"] = JsonNode.Parse(value);
        var ui = CodexAppServerClientPluginParsers.ParsePluginSummary(Element(raw)).Interface!;
        ui.ComposerIcon.Should().BeNull(); ui.Logo.Should().BeNull(); ui.ComposerIconPath.Should().BeNull(); ui.LogoPath.Should().BeNull();
        ui.DefaultPrompts.Should().BeEmpty(); ui.Capabilities.Should().BeEmpty(); ui.Screenshots.Should().BeEmpty(); ui.ScreenshotUrls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("installed")]
    [InlineData("enabled")]
    [InlineData("authPolicy")]
    [InlineData("installPolicy")]
    [InlineData("source")]
    public void Summary_RejectsMissingRequiredFields(string field)
    {
        var raw = Summary(); raw.Remove(field);
        Action parse = () => CodexAppServerClientPluginParsers.ParsePluginSummary(Element(raw));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + field + "*");
    }

    [Theory]
    [InlineData("null", "cannot be null")]
    [InlineData("[]", "required object property 'source'")]
    [InlineData("{}", "'type' is missing or invalid")]
    [InlineData("{\"type\":\" \"}", "'type' is missing or invalid")]
    [InlineData("{\"type\":\"local\",\"path\":\"relative\"}", "path")]
    public void Summary_RejectsMalformedSources(string source, string error)
    {
        var raw = Summary(); raw["source"] = JsonNode.Parse(source);
        Action parse = () => CodexAppServerClientPluginParsers.ParsePluginSummary(Element(raw));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("1.5")]
    [InlineData("\"bad\"")]
    public void Summary_InvalidOptionalInstalledTimestampIsAbsent(string value)
    {
        var raw = Summary(); raw["installedAt"] = JsonNode.Parse(value);
        CodexAppServerClientPluginParsers.ParsePluginSummary(Element(raw)).InstalledAt.Should().BeNull();
    }

    [Fact]
    public void Detail_MapsSkillsOnboardingAppsHooksAndOptionalCollections()
    {
        var detail = Detail();
        detail["description"] = "Description"; detail["marketplacePath"] = XPaths.Abs("market"); detail["mcpServers"] = new JsonArray("mcp");
        var skill = JsonSerializer.SerializeToNode(new
        {
            name = "Skill", path = XPaths.Abs("skill"), enabled = true, description = "Description", shortDescription = "Short",
            @interface = new { displayName = "Display", shortDescription = "Hint", defaultPrompt = "Prompt", brandColor = "Blue", iconSmall = "small", iconLarge = "large", iconSmallUrl = "https://small.test", iconLargeUrl = "https://large.test" }
        })!;
        detail["skills"] = new JsonArray(skill); detail["onboardingSkill"] = skill.DeepClone();
        detail["apps"] = JsonNode.Parse("""[{"id":"app","name":"App","needsAuth":true,"description":"Connect","installUrl":"https://install.test"}]""");
        detail["hooks"] = JsonNode.Parse("""[{"key":"hook","eventName":"SessionStart"}]""");
        PluginReadResult result;
        using (var document = JsonDocument.Parse(new JsonObject { ["plugin"] = detail }.ToJsonString()))
            result = CodexAppServerClientPluginParsers.ParsePluginReadResult(document.RootElement);
        var plugin = result.Plugin;
        plugin.Description.Should().Be("Description"); plugin.MarketplaceName.Should().Be("market"); plugin.MarketplacePath.Should().Be(XPaths.Abs("market"));
        plugin.Summary.Id.Should().Be("plug"); plugin.McpServers.Should().Equal("mcp"); plugin.AppTemplates.Should().BeEmpty();
        var parsed = plugin.Skills.Should().ContainSingle().Subject;
        parsed.Name.Should().Be("Skill"); parsed.Path.Should().Be(XPaths.Abs("skill")); parsed.Enabled.Should().BeTrue(); parsed.Description.Should().Be("Description"); parsed.ShortDescription.Should().Be("Short");
        var ui = parsed.Interface!;
        ui.DisplayName.Should().Be("Display"); ui.ShortDescription.Should().Be("Hint"); ui.DefaultPrompt.Should().Be("Prompt"); ui.BrandColor.Should().Be("Blue");
        ui.IconSmall.Should().Be("small"); ui.IconLarge.Should().Be("large"); ui.IconSmallUrl.Should().Be("https://small.test"); ui.IconLargeUrl.Should().Be("https://large.test");
        ui.Raw.GetProperty("displayName").GetString().Should().Be("Display"); parsed.Raw.GetProperty("name").GetString().Should().Be("Skill");
        plugin.OnboardingSkill!.Name.Should().Be("Skill");
        var app = plugin.Apps.Should().ContainSingle().Subject;
        app.Id.Should().Be("app"); app.Name.Should().Be("App"); app.NeedsAuth.Should().BeTrue(); app.Description.Should().Be("Connect"); app.InstallUrl.Should().Be("https://install.test");
        app.Raw.GetProperty("id").GetString().Should().Be("app");
        var hook = plugin.Hooks.Should().ContainSingle().Subject;
        hook.Key.Should().Be("hook"); hook.EventName.Should().Be("SessionStart"); hook.Raw.GetProperty("key").GetString().Should().Be("hook");
        plugin.Raw.GetProperty("description").GetString().Should().Be("Description");
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("skills")]
    [InlineData("apps")]
    [InlineData("mcpServers")]
    [InlineData("marketplaceName")]
    public void Detail_RejectsMissingRequiredCollectionsAndMetadata(string field)
    {
        var detail = Detail(); detail.Remove(field);
        Action parse = () => CodexAppServerClientPluginParsers.ParsePluginReadResult(Element(new JsonObject { ["plugin"] = detail }));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + field + "*");
    }

    [Theory]
    [InlineData("skills", "[null]", "skills[] entries must be objects")]
    [InlineData("apps", "[null]", "apps[] entries must be objects")]
    [InlineData("hooks", "[null]", "hooks[] entries must be objects")]
    [InlineData("mcpServers", "[false]", "entries must be strings")]
    [InlineData("appTemplates", "[null]", "appTemplates[] entries must be objects")]
    public void Detail_RejectsMalformedCollectionEntries(string field, string values, string error)
    {
        var detail = Detail(); detail[field] = JsonNode.Parse(values);
        Action parse = () => CodexAppServerClientPluginParsers.ParsePluginReadResult(Element(new JsonObject { ["plugin"] = detail }));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Theory]
    [InlineData("list", "{}", "marketplaces array")]
    [InlineData("list", "{\"marketplaces\":[null]}", "marketplaces[] entries must be objects")]
    [InlineData("list", "{\"marketplaces\":[{}]}", "plugins array")]
    [InlineData("list", "{\"marketplaces\":[{\"plugins\":[null]}]}", "plugins[] entries must be objects")]
    [InlineData("list", "{\"marketplaces\":[],\"marketplaceLoadErrors\":[null]}", "marketplaceLoadErrors[] entries must be objects")]
    [InlineData("list", "{\"marketplaces\":[],\"marketplaceLoadErrors\":[{}]}", "marketplacePath and message")]
    [InlineData("list", "{\"marketplaces\":[],\"marketplaceLoadErrors\":[{\"marketplacePath\":\"relative\",\"message\":\"Error\"}]}", "marketplacePath")]
    [InlineData("read", "{}", "plugin object")]
    [InlineData("install", "{}", "appsNeedingAuth array")]
    [InlineData("install", "{\"appsNeedingAuth\":[null]}", "entries must be objects")]
    [InlineData("search", "{}", "data array")]
    [InlineData("search", "{\"data\":[null]}", "entries must be objects")]
    [InlineData("search", "{\"data\":[{}]}", "plugin object")]
    [InlineData("reconcile", "null", "JSON object")]
    [InlineData("reconcile", "{}", "changedPlugins array")]
    [InlineData("reconcile", "{\"changedPlugins\":[null]}", "entries must be objects")]
    [InlineData("uninstall", "null", "JSON object")]
    public void Operations_RejectMalformedResponsesWithContext(string operation, string json, string error)
    {
        Action parse = () => ParseOperation(operation, Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Fact]
    public void ListSearchInstallAndReconcile_MapOperationSpecificPayloads()
    {
        var marketplace = new JsonObject
        {
            ["name"] = "Market", ["path"] = XPaths.Abs("market"),
            ["interface"] = new JsonObject { ["displayName"] = "Marketplace Display" },
            ["plugins"] = new JsonArray(Summary())
        };
        var list = CodexAppServerClientPluginParsers.ParsePluginListResult(Element(new JsonObject
        {
            ["marketplaces"] = new JsonArray(marketplace), ["featuredPluginIds"] = new JsonArray("plug"), ["remoteSyncError"] = "remote unavailable",
            ["marketplaceLoadErrors"] = new JsonArray(new JsonObject { ["marketplacePath"] = XPaths.Abs("broken"), ["message"] = "load failed" })
        }));
        list.FeaturedPluginIds.Should().Equal("plug"); list.RemoteSyncError.Should().Be("remote unavailable");
        var market = list.Marketplaces.Should().ContainSingle().Subject;
        market.Name.Should().Be("Market"); market.Path.Should().Be(XPaths.Abs("market")); market.Plugins.Should().ContainSingle().Which.Id.Should().Be("plug");
        market.Interface!.DisplayName.Should().Be("Marketplace Display"); market.Interface.Raw.GetProperty("displayName").GetString().Should().Be("Marketplace Display");
        market.Raw.GetProperty("name").GetString().Should().Be("Market");
        var error = list.MarketplaceLoadErrors.Should().ContainSingle().Subject;
        error.MarketplacePath.Should().Be(XPaths.Abs("broken")); error.Message.Should().Be("load failed");
        list.Raw.GetProperty("remoteSyncError").GetString().Should().Be("remote unavailable");
        var search = CodexAppServerClientPluginParsers.ParsePluginSearchPage(Element(new JsonObject
        {
            ["data"] = new JsonArray(new JsonObject { ["plugin"] = Summary(), ["marketplaceName"] = "Search Market", ["marketplacePath"] = XPaths.Abs("search") }),
            ["nextCursor"] = "cursor"
        }));
        search.NextCursor.Should().Be("cursor"); var hit = search.Data.Should().ContainSingle().Subject;
        hit.Plugin.Id.Should().Be("plug"); hit.MarketplaceName.Should().Be("Search Market"); hit.MarketplacePath.Should().Be(XPaths.Abs("search"));
        hit.Raw.GetProperty("marketplaceName").GetString().Should().Be("Search Market");
        var install = CodexAppServerClientPluginParsers.ParsePluginInstallResult(Json("""{"appsNeedingAuth":[{"id":"app","name":"App","needsAuth":true}],"authPolicy":"ON_INSTALL"}"""));
        install.AuthPolicy.Should().Be("ON_INSTALL"); install.AuthPolicyValue.Should().Be(PluginAuthPolicy.OnInstall);
        install.AppsNeedingAuth.Should().ContainSingle().Which.Id.Should().Be("app"); install.Raw.GetProperty("authPolicy").GetString().Should().Be("ON_INSTALL");
        var reconcile = CodexAppServerClientPluginParsers.ParsePluginReconcileResult(Json("""{"changedPlugins":[{"id":"p","hasMcps":true,"hasApps":false,"hasHooks":true,"hasSkills":false}],"failedRemotePluginIds":["remote-failed"],"failedMaterializationRemotePluginIds":["materialization-failed"]}"""));
        var changed = reconcile.ChangedPlugins.Should().ContainSingle().Subject;
        changed.Id.Should().Be("p"); changed.HasMcps.Should().BeTrue(); changed.HasApps.Should().BeFalse(); changed.HasHooks.Should().BeTrue(); changed.HasSkills.Should().BeFalse();
        changed.Raw.GetProperty("id").GetString().Should().Be("p");
        reconcile.FailedRemotePluginIds.Should().Equal("remote-failed"); reconcile.FailedMaterializationRemotePluginIds.Should().Equal("materialization-failed");
        reconcile.Raw.GetProperty("changedPlugins").GetArrayLength().Should().Be(1);
        CodexAppServerClientPluginParsers.ParsePluginUninstallResult(Json("{\"future\":true}")).Raw.GetProperty("future").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void MinimalSkillHasNoInterface_AndAbsentHooksStayEmpty()
    {
        var detail = Detail(); detail["skills"] = JsonNode.Parse("""[{"name":"Skill","enabled":false,"description":"Description"}]""");
        var plugin = CodexAppServerClientPluginParsers.ParsePluginReadResult(Element(new JsonObject { ["plugin"] = detail })).Plugin;
        plugin.Skills.Should().ContainSingle().Which.Interface.Should().BeNull(); plugin.Hooks.Should().BeEmpty(); plugin.OnboardingSkill.Should().BeNull();
    }

    [Fact]
    public void AppTemplates_PreserveOptionalMetadataAndFutureReasons()
    {
        var templates = CodexAppServerClientPluginAppTemplateParsers.ParseAppTemplates(Json("""
            {"appTemplates":[{"templateId":"template","name":"Template","description":"Description","canonicalConnectorId":"connector","logoUrl":"light","logoUrlDark":"dark","materializedAppIds":["app"],"reason":"FUTURE"},
            {"templateId":"minimal","name":"Minimal"}]}
            """));
        var template = templates[0];
        template.TemplateId.Should().Be("template"); template.Name.Should().Be("Template"); template.Description.Should().Be("Description");
        template.CanonicalConnectorId.Should().Be("connector"); template.LogoUrl.Should().Be("light"); template.LogoUrlDark.Should().Be("dark");
        template.MaterializedAppIds.Should().Equal("app"); template.Reason.Should().Be("FUTURE"); template.ReasonValue!.Value.Value.Should().Be("FUTURE");
        template.Raw.GetProperty("templateId").GetString().Should().Be("template"); templates[1].ReasonValue.Should().BeNull(); templates[1].MaterializedAppIds.Should().BeEmpty();
    }

    private static object ParseOperation(string operation, JsonElement json) => operation switch
    {
        "list" => CodexAppServerClientPluginParsers.ParsePluginListResult(json),
        "read" => CodexAppServerClientPluginParsers.ParsePluginReadResult(json),
        "install" => CodexAppServerClientPluginParsers.ParsePluginInstallResult(json),
        "search" => CodexAppServerClientPluginParsers.ParsePluginSearchPage(json),
        "reconcile" => CodexAppServerClientPluginParsers.ParsePluginReconcileResult(json),
        "uninstall" => CodexAppServerClientPluginParsers.ParsePluginUninstallResult(json),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
}
