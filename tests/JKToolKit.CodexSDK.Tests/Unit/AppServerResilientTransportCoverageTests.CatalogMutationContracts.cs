using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    private static JsonObject CatalogModel() => JsonNode.Parse("""
        {"id":"catalog-id","model":"wire-model","displayName":"Display model","description":"Model description",
        "defaultReasoningEffort":"medium","supportedReasoningEfforts":[{"reasoningEffort":"low","description":"Fast"},{"reasoningEffort":"high","description":"Thorough"}],
        "hidden":true,"isDefault":false,"supportsPersonality":true,"inputModalities":["text","audio"],"upgrade":"next-model",
        "availabilityNux":{"message":"Now available"},"upgradeInfo":{"model":"next-model","upgradeCopy":"Try the upgrade","modelLink":"https://models.test/next","migrationMarkdown":"Migrate using this guide","retirementAt":1800000000}}
        """)!.AsObject();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CatalogMutation_ModelMetadataPreservesIndependentNamesFlagsAndCapabilities(bool flag)
    {
        var model = CatalogModel(); model["hidden"] = flag; model["isDefault"] = !flag; model["supportsPersonality"] = flag;
        var response = new JsonObject { ["data"] = new JsonArray(model), ["nextCursor"] = "next-page" };
        var rpc = new RecordingRpc(response.ToJsonString());
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ListModelsAsync(new ModelListOptions { Cursor = "current-page", Limit = 7, IncludeHidden = true });
        var actual = result.Data.Should().ContainSingle().Subject;
        actual.Id.Should().Be("catalog-id"); actual.Model.Should().Be("wire-model"); actual.DisplayName.Should().Be("Display model");
        actual.Description.Should().Be("Model description"); actual.DefaultReasoningEffort.Should().Be("medium");
        actual.Hidden.Should().Be(flag); actual.IsDefault.Should().Be(!flag); actual.SupportsPersonality.Should().Be(flag);
        actual.InputModalities.Should().Equal("text", "audio"); actual.AvailabilityNuxMessage.Should().Be("Now available"); actual.Upgrade.Should().Be("next-model");
        actual.SupportedReasoningEfforts.Select(e => (e.ReasoningEffort, e.Description)).Should().Equal(("low", "Fast"), ("high", "Thorough"));
        actual.UpgradeInfo!.Model.Should().Be("next-model"); actual.UpgradeInfo.UpgradeCopy.Should().Be("Try the upgrade");
        actual.UpgradeInfo.ModelLink.Should().Be("https://models.test/next"); actual.UpgradeInfo.MigrationMarkdown.Should().Be("Migrate using this guide");
        actual.UpgradeInfo.RetirementAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1800000000));
        result.NextCursor.Should().Be("next-page");
        var request = rpc.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be("model/list"); request.Parameters.GetProperty("cursor").GetString().Should().Be("current-page");
        request.Parameters.GetProperty("limit").GetInt32().Should().Be(7); request.Parameters.GetProperty("includeHidden").GetBoolean().Should().BeTrue();
        JsonElement.DeepEquals(actual.Raw, JsonSerializer.SerializeToElement(model)).Should().BeTrue();
    }

    [Theory]
    [InlineData("missing", true)]
    [InlineData("null", true)]
    [InlineData("[]", false)]
    public async Task CatalogMutation_MissingModalitiesUseDefaultsButExplicitEmptyListRemainsEmpty(string modalities, bool defaults)
    {
        var model = CatalogModel(); model.Remove("hidden"); model.Remove("isDefault"); model.Remove("supportsPersonality");
        if (modalities == "missing") model.Remove("inputModalities"); else model["inputModalities"] = JsonNode.Parse(modalities);
        model["supportedReasoningEfforts"] = JsonNode.Parse("""[null,{}, {"reasoningEffort":" "},{"reasoningEffort":"future"}]""");
        var rpc = new RecordingRpc(new JsonObject { ["data"] = new JsonArray(model) }.ToJsonString());
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var actual = (await client.ListModelsAsync()).Data.Should().ContainSingle().Subject;
        actual.Hidden.Should().BeFalse(); actual.IsDefault.Should().BeFalse(); actual.SupportsPersonality.Should().BeFalse();
        actual.InputModalities.Should().Equal(defaults ? new[] { "text", "image" } : []);
        var effort = actual.SupportedReasoningEfforts.Should().ContainSingle().Subject;
        effort.ReasoningEffort.Should().Be("future"); effort.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task CatalogMutation_ExperimentalFeatureMetadataKeepsDisplayAndRolloutInformation()
    {
        var rpc = new RecordingRpc("""{"data":[{"name":"feature-id","stage":"future-stage","displayName":"Feature display","description":"Feature description","announcement":"Available in preview","enabled":false,"defaultEnabled":true}],"nextCursor":"next-feature"}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.ListExperimentalFeaturesAsync(new ExperimentalFeatureListOptions { Cursor = " ", Limit = 3 });
        var feature = result.Data.Should().ContainSingle().Subject;
        feature.Name.Should().Be("feature-id"); feature.Stage.Should().Be("future-stage"); feature.DisplayName.Should().Be("Feature display");
        feature.Description.Should().Be("Feature description"); feature.Announcement.Should().Be("Available in preview");
        feature.Enabled.Should().BeFalse(); feature.DefaultEnabled.Should().BeTrue(); result.NextCursor.Should().Be("next-feature");
        rpc.Requests.Single().Parameters.TryGetProperty("cursor", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogMutation_ConfigWritesReturnServerVersionAndPath(bool batch)
    {
        var rpc = new RecordingRpc("""{"status":"okOverridden","version":"server-version","filePath":"/server/config.toml"}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var value = JsonSerializer.SerializeToElement("request-value");
        var result = batch
            ? await client.WriteConfigBatchAsync(new() { Edits = [new() { KeyPath = "model", Value = value, MergeStrategy = ConfigMergeStrategy.Replace }], FilePath = " ", ExpectedVersion = " " })
            : await client.WriteConfigValueAsync(new() { KeyPath = "model", Value = value, MergeStrategy = ConfigMergeStrategy.Replace, FilePath = " ", ExpectedVersion = " " });
        result.Status.Should().Be(ConfigWriteStatus.OkOverridden); result.Version.Should().Be("server-version"); result.FilePath.Should().Be(PathForPlatform("/server/config.toml"));
        result.OverriddenMetadata.Should().BeNull();
        var request = rpc.Requests.Should().ContainSingle().Subject;
        request.Parameters.TryGetProperty("filePath", out _).Should().BeFalse(); request.Parameters.TryGetProperty("expectedVersion", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CatalogMutation_FeedbackReturnsPromptHashAndNormalizesOptionalEmptyFields()
    {
        var rpc = new RecordingRpc("""{"threadId":"server-thread","promptHash":"prompt-digest"}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var result = await client.UploadFeedbackAsync(new() { Classification = "bug", Reason = " ", ThreadId = " ", IncludeLogs = false });
        result.ThreadId.Should().Be("server-thread"); result.PromptHash.Should().Be("prompt-digest");
        var request = rpc.Requests.Single(); request.Method.Should().Be("feedback/upload");
        request.Parameters.TryGetProperty("reason", out _).Should().BeFalse(); request.Parameters.TryGetProperty("threadId", out _).Should().BeFalse();
    }
}
