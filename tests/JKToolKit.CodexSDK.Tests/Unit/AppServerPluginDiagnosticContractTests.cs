using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerPluginDiagnosticContractTests
{
    private static JsonObject Object(string json) => JsonNode.Parse(json)!.AsObject();
    private static JsonElement Element(JsonNode node) => JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
    private static JsonObject Summary() => Object("""{"id":"p","name":"P","installed":true,"enabled":false,"authPolicy":"ON_USE","installPolicy":"AVAILABLE","source":{"type":"remote"}}""");
    private static JsonObject Detail() => new() { ["summary"] = Summary(), ["marketplaceName"] = "M", ["skills"] = new JsonArray(), ["apps"] = new JsonArray(), ["mcpServers"] = new JsonArray() };
    private static void AssertError(Action parse, string property, string context)
    {
        var exception = parse.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain(property).And.Contain(context);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("installed")]
    [InlineData("enabled")]
    [InlineData("authPolicy")]
    [InlineData("installPolicy")]
    [InlineData("source")]
    public void SummaryErrors_IdentifyThePropertyAndOwningObject(string property)
    {
        var summary = Summary(); summary.Remove(property);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginSummary(Element(summary)), property, "plugin summary");
        if (property is "authPolicy" or "installPolicy" or "source")
        {
            summary[property] = property == "source" ? new JsonArray() : JsonValue.Create(" ");
            AssertError(() => CodexAppServerClientPluginParsers.ParsePluginSummary(Element(summary)), property, "plugin summary");
        }
    }

    [Theory]
    [InlineData("screenshots")]
    [InlineData("composerIcon")]
    [InlineData("logo")]
    public void InterfacePathErrors_IdentifyTheAsset(string property)
    {
        var summary = Summary();
        summary["interface"] = new JsonObject { [property] = property == "screenshots" ? new JsonArray("relative") : JsonValue.Create("relative") };
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginSummary(Element(summary)), property, "plugin interface");
    }

    [Theory]
    [InlineData("skills", "name", "plugin skill")]
    [InlineData("skills", "enabled", "plugin skill")]
    [InlineData("skills", "description", "plugin skill")]
    [InlineData("skills", "path", "plugin skill")]
    [InlineData("apps", "id", "plugin app")]
    [InlineData("apps", "name", "plugin app")]
    [InlineData("apps", "needsAuth", "plugin app")]
    [InlineData("hooks", "key", "plugin hook")]
    [InlineData("hooks", "eventName", "plugin hook")]
    [InlineData("appTemplates", "templateId", "plugin app template")]
    [InlineData("appTemplates", "name", "plugin app template")]
    public void NestedDetailErrors_IdentifyTheEntryAndProperty(string collection, string property, string context)
    {
        var item = Object("""{"name":"Name","enabled":true,"description":"Description","id":"id","needsAuth":false,"key":"hook","eventName":"SessionStart","templateId":"template"}""");
        if (property == "path") item[property] = "relative"; else item.Remove(property);
        var detail = Detail(); detail[collection] = new JsonArray(item);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginReadResult(Element(new JsonObject { ["plugin"] = detail })), property, context);
    }

    [Theory]
    [InlineData("mcpServers")]
    [InlineData("marketplaceName")]
    [InlineData("marketplacePath")]
    public void DetailErrors_IdentifyTheResponseAndProperty(string property)
    {
        var detail = Detail();
        if (property == "marketplacePath") detail[property] = "relative"; else detail.Remove(property);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginReadResult(Element(new JsonObject { ["plugin"] = detail })), property, "plugin/read plugin");
    }

    [Theory]
    [InlineData("name")]
    [InlineData("path")]
    public void MarketplaceErrors_IdentifyTheListEntry(string property)
    {
        var market = new JsonObject { ["plugins"] = new JsonArray(), ["name"] = "Market" };
        if (property == "path") market[property] = "relative"; else market.Remove(property);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginListResult(Element(new JsonObject { ["marketplaces"] = new JsonArray(market) })), property, "plugin/list marketplaces[]");
    }

    [Theory]
    [InlineData("marketplaceName")]
    [InlineData("marketplacePath")]
    public void SearchErrors_IdentifyTheSearchHit(string property)
    {
        var hit = new JsonObject { ["plugin"] = Summary(), ["marketplaceName"] = "Market" };
        if (property == "marketplacePath") hit[property] = "relative"; else hit.Remove(property);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginSearchPage(Element(new JsonObject { ["data"] = new JsonArray(hit) })), property, "plugin/search data[]");
    }

    [Theory]
    [InlineData("id")]
    [InlineData("hasMcps")]
    [InlineData("hasApps")]
    [InlineData("hasHooks")]
    [InlineData("hasSkills")]
    [InlineData("failedRemotePluginIds")]
    [InlineData("failedMaterializationRemotePluginIds")]
    public void ReconcileErrors_IdentifyTheChangedPluginOrResponseArray(string property)
    {
        var result = Object("""{"changedPlugins":[{"id":"p","hasMcps":false,"hasApps":true,"hasHooks":false,"hasSkills":true}],"failedRemotePluginIds":[],"failedMaterializationRemotePluginIds":[]}""");
        var isArray = property.StartsWith("failed", StringComparison.Ordinal);
        if (isArray) result.Remove(property); else result["changedPlugins"]![0]!.AsObject().Remove(property);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginReconcileResult(Element(result)), property, isArray ? "plugin/reconcile response" : "plugin/reconcile changedPlugins[]");
    }

    [Fact]
    public void InstallErrors_IdentifyMissingAndInvalidAuthPolicy()
    {
        foreach (var policy in new string?[] { null, " " })
        {
            var result = new JsonObject { ["appsNeedingAuth"] = new JsonArray(), ["authPolicy"] = policy };
            AssertError(() => CodexAppServerClientPluginParsers.ParsePluginInstallResult(Element(result)), "authPolicy", "plugin/install response");
        }
    }

    [Theory]
    [InlineData("marketplacePath")]
    [InlineData("message")]
    public void LoadErrors_RequireBothPathAndMessage(string missing)
    {
        var error = new JsonObject { ["marketplacePath"] = XPaths.Abs("market"), ["message"] = "error" }; error.Remove(missing);
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginListResult(Element(new JsonObject { ["marketplaces"] = new JsonArray(), ["marketplaceLoadErrors"] = new JsonArray(error) })), missing, "plugin/list marketplaceLoadErrors[]");
    }

    [Fact]
    public void LoadErrorPath_ReportsTheOwningErrorEntry()
    {
        var result = Object("""{"marketplaces":[],"marketplaceLoadErrors":[{"marketplacePath":"relative","message":"error"}]}""");
        AssertError(() => CodexAppServerClientPluginParsers.ParsePluginListResult(Element(result)), "marketplacePath", "plugin/list marketplaceLoadErrors[]");
    }

    [Theory]
    [InlineData("remotePluginId")]
    [InlineData("shareUrl")]
    public void ShareSaveErrors_IdentifyMissingResponseFields(string property)
    {
        var result = Object("""{"remotePluginId":"remote","shareUrl":"https://share.test"}"""); result.Remove(property);
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseSaveResult(Element(result)), property, "plugin/share/save response");
    }

    [Theory]
    [InlineData("remotePluginId")]
    [InlineData("pluginId")]
    [InlineData("pluginName")]
    [InlineData("pluginPath")]
    [InlineData("marketplaceName")]
    [InlineData("marketplacePath")]
    public void CheckoutErrors_IdentifyMissingFieldsAndInvalidPaths(string property)
    {
        var result = Object("""{"remotePluginId":"remote","pluginId":"plugin","pluginName":"Plugin","marketplaceName":"Market"}""");
        result["pluginPath"] = XPaths.Abs("plugin"); result["marketplacePath"] = XPaths.Abs("market"); result.Remove(property);
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseCheckoutResult(Element(result)), property, "plugin/share/checkout response");
        if (property.EndsWith("Path", StringComparison.Ordinal))
        {
            result[property] = "relative";
            AssertError(() => CodexAppServerClientPluginShareParsers.ParseCheckoutResult(Element(result)), property, "plugin/share/checkout response");
        }
    }

    [Theory]
    [InlineData("principalType")]
    [InlineData("principalId")]
    [InlineData("role")]
    [InlineData("name")]
    public void PrincipalErrors_IdentifyThePrincipalField(string property)
    {
        var principal = Object("""{"principalType":"user","principalId":"user-id","role":"reader","name":"Name"}"""); principal.Remove(property);
        var result = new JsonObject { ["principals"] = new JsonArray(principal), ["discoverability"] = "PRIVATE" };
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseUpdateTargetsResult(Element(result)), property, "plugin share principal");
    }

    [Fact]
    public void ShareContextAndUpdateErrors_IdentifyRequiredCollectionsAndMalformedEntries()
    {
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(Element(Object("{\"shareContext\":{}}"))), "remotePluginId", "plugin share context");
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(Element(Object("""{"shareContext":{"remotePluginId":"r","sharePrincipals":[null]}}"""))), "sharePrincipals", "plugin share context");
        foreach (var data in new[] { "{}", "{\"principals\":[null]}", "{\"principals\":[]}" })
            AssertError(() => CodexAppServerClientPluginShareParsers.ParseUpdateTargetsResult(Element(Object(data))), data == "{\"principals\":[]}" ? "discoverability" : "principals", "plugin/share/updateTargets response");
        var item = new JsonObject { ["plugin"] = Summary(), ["localPluginPath"] = "relative" };
        AssertError(() => CodexAppServerClientPluginShareParsers.ParseListResult(Element(new JsonObject { ["data"] = new JsonArray(item) })), "localPluginPath", "plugin/share/list data[]");
    }
}
