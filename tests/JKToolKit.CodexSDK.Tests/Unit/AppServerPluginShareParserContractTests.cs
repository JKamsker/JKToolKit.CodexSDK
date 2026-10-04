using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerPluginShareParserContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Fact]
    public void ShareContext_MapsIdentityPrincipalsAndUnknownWireValues()
    {
        PluginShareContextDescriptor context;
        using (var document = JsonDocument.Parse("""
            {"shareContext":{"remotePluginId":"remote","remoteVersion":"v2","discoverability":"future","shareUrl":"https://share.test",
            "creatorAccountUserId":"creator","creatorName":"Creator","canPublishToWorkspace":false,
            "sharePrincipals":[{"principalType":"future-type","principalId":"principal","role":"future-role","name":"Display"}]}}
            """))
            context = CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(document.RootElement)!;
        context.RemotePluginId.Should().Be("remote"); context.RemoteVersion.Should().Be("v2"); context.Discoverability!.Value.Value.Should().Be("future");
        context.ShareUrl.Should().Be("https://share.test"); context.CreatorAccountUserId.Should().Be("creator"); context.CreatorName.Should().Be("Creator");
        context.CanPublishToWorkspace.Should().BeFalse(); context.Raw.GetProperty("remoteVersion").GetString().Should().Be("v2");
        var principal = context.SharePrincipals.Should().ContainSingle().Subject;
        principal.PrincipalType.Value.Should().Be("future-type"); principal.PrincipalId.Should().Be("principal");
        principal.Role.Value.Should().Be("future-role"); principal.Name.Should().Be("Display"); principal.Raw.GetProperty("principalId").GetString().Should().Be("principal");
    }

    [Fact]
    public void ShareContext_OptionalMetadataRemainsAbsent()
    {
        CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(Json("{}")).Should().BeNull();
        var context = CodexAppServerClientPluginShareParsers.ParseShareContextOrNull(Json("""{"shareContext":{"remotePluginId":"r"}}"""))!;
        context.Discoverability.Should().BeNull(); context.SharePrincipals.Should().BeNull(); context.CanPublishToWorkspace.Should().BeNull();
    }

    [Theory]
    [InlineData("{}", "principals")]
    [InlineData("{\"principals\":[null]}", "entries must be objects")]
    [InlineData("{\"principals\":[]}", "discoverability")]
    [InlineData("{\"principals\":[{}]}", "principalType")]
    [InlineData("{\"principals\":[{\"principalType\":\"user\"}]}", "principalId")]
    [InlineData("{\"principals\":[{\"principalType\":\"user\",\"principalId\":\"u\"}]}", "role")]
    [InlineData("{\"principals\":[{\"principalType\":\"user\",\"principalId\":\"u\",\"role\":\"reader\"}]}", "name")]
    public void UpdateTargets_RejectsMissingOrMalformedPrincipals(string json, string error)
    {
        Action parse = () => CodexAppServerClientPluginShareParsers.ParseUpdateTargetsResult(Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Theory]
    [InlineData("{}", "data array")]
    [InlineData("{\"data\":[null]}", "entries must be objects")]
    [InlineData("{\"data\":[{}]}", "plugin object")]
    public void ShareList_RejectsInvalidEnvelopes(string json, string error)
    {
        Action parse = () => CodexAppServerClientPluginShareParsers.ParseListResult(Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Fact]
    public void ShareResults_MapOperationSpecificIdentifiersAndPreserveRaw()
    {
        var save = CodexAppServerClientPluginShareParsers.ParseSaveResult(Json("""{"remotePluginId":"r","shareUrl":"https://share.test","canPublishToWorkspace":true}"""));
        save.RemotePluginId.Should().Be("r"); save.ShareUrl.Should().Be("https://share.test"); save.CanPublishToWorkspace.Should().BeTrue();
        save.Raw.GetProperty("remotePluginId").GetString().Should().Be("r");
        var update = CodexAppServerClientPluginShareParsers.ParseUpdateTargetsResult(Json("""{"principals":[],"discoverability":"future"}"""));
        update.Principals.Should().BeEmpty(); update.Discoverability.Value.Should().Be("future"); update.Raw.GetProperty("discoverability").GetString().Should().Be("future");
        var checkout = CodexAppServerClientPluginShareParsers.ParseCheckoutResult(JsonSerializer.SerializeToElement(new
        {
            remotePluginId = "remote", pluginId = "plugin", pluginName = "Plugin", pluginPath = XPaths.Abs("plugin"),
            marketplaceName = "Market", marketplacePath = XPaths.Abs("market"), remoteVersion = "version"
        }));
        checkout.RemotePluginId.Should().Be("remote"); checkout.PluginId.Should().Be("plugin"); checkout.PluginName.Should().Be("Plugin"); checkout.PluginPath.Should().Be(XPaths.Abs("plugin"));
        checkout.MarketplaceName.Should().Be("Market"); checkout.MarketplacePath.Should().Be(XPaths.Abs("market")); checkout.RemoteVersion.Should().Be("version");
        checkout.Raw.GetProperty("pluginId").GetString().Should().Be("plugin");
        CodexAppServerClientPluginShareParsers.ParseDeleteResult(Json("{\"future\":true}")).Raw.GetProperty("future").GetBoolean().Should().BeTrue();
        Action invalidDelete = () => CodexAppServerClientPluginShareParsers.ParseDeleteResult(Json("null"));
        invalidDelete.Should().Throw<InvalidOperationException>().WithMessage("*plugin/share/delete*JSON object*");
    }

    [Fact]
    public void ShareTargets_KeepNullDistinctFromEmptyAndEmitWireFields()
    {
        CodexAppServerClientPluginShareParsers.BuildShareTargetsOrNull(null, "options").Should().BeNull();
        CodexAppServerClientPluginShareParsers.BuildShareTargets(null, "options").Should().BeEmpty();
        CodexAppServerClientPluginShareParsers.BuildShareTargetsOrNull([], "options").Should().BeEmpty();
        var targets = CodexAppServerClientPluginShareParsers.BuildShareTargets([
            new PluginShareTarget { PrincipalType = PluginSharePrincipalType.Parse("future-type"), PrincipalId = "id", Role = PluginShareTargetRole.Parse("future-role") }
        ], "options");
        JsonSerializer.Serialize(targets).Should().Be("[{\"principalType\":\"future-type\",\"principalId\":\"id\",\"role\":\"future-role\"}]");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("type")]
    [InlineData("id")]
    [InlineData("role")]
    public void ShareTargets_RejectIncompleteValuesBeforeSerialization(string invalid)
    {
        var target = invalid == "null" ? null : new PluginShareTarget
        {
            PrincipalType = invalid == "type" ? default : PluginSharePrincipalType.Parse("user"),
            PrincipalId = invalid == "id" ? " " : "id",
            Role = invalid == "role" ? default : PluginShareTargetRole.Parse("reader")
        };
        Action build = () => CodexAppServerClientPluginShareParsers.BuildShareTargets([target!], "options");
        build.Should().Throw<ArgumentException>().WithParameterName("options").WithMessage("*ShareTargets[0]*");
    }
}
