using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadSummaryMutationContractTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);
    private static JsonObject Metadata(string suffix, int seconds, bool flag) => JsonSerializer.SerializeToNode(new
    {
        id = "thread-" + suffix, name = "name-" + suffix, preview = "preview-" + suffix,
        status = new { type = "active", activeFlags = new[] { "flag-" + suffix } }, archived = flag, isPinned = flag, ephemeral = flag,
        section = new { id = "section-" + suffix, name = "Section " + suffix, appearance = new { color = "color-" + suffix, icon = "icon-" + suffix } },
        sectionEnteredAt = seconds, createdAt = seconds + 1, updatedAt = seconds + 2,
        source = new { kind = "source-" + suffix, subAgent = new { threadSpawn = new { parentThreadId = "source-parent-" + suffix } } },
        parentThreadId = "parent-" + suffix, cliVersion = "version-" + suffix,
        gitInfo = new { sha = "sha-" + suffix, branch = "branch-" + suffix, originUrl = "https://git.test/" + suffix }
    })!.AsObject();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Metadata_UsesNestedThreadValuesBeforeEnvelopeFallbacks(bool hasPrimaryMetadata)
    {
        var primary = hasPrimaryMetadata ? Metadata("primary", 123, false) : new JsonObject { ["id"] = "thread-primary" };
        var envelope = Metadata("fallback", 456, true);
        var wrapper = new JsonObject { ["thread"] = primary, ["name"] = "wrapper-name" };
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(Element(wrapper), Element(envelope))!;
        var suffix = hasPrimaryMetadata ? "primary" : "fallback"; var seconds = hasPrimaryMetadata ? 123 : 456;
        summary.ThreadId.Should().Be("thread-primary"); summary.Name.Should().Be("name-" + suffix); summary.Preview.Should().Be("preview-" + suffix);
        summary.Archived.Should().Be(!hasPrimaryMetadata); summary.Ephemeral.Should().Be(!hasPrimaryMetadata);
#pragma warning disable CS0618
        summary.IsPinned.Should().Be(!hasPrimaryMetadata);
#pragma warning restore CS0618
        summary.StatusType.Should().Be("active"); summary.ActiveFlags.Should().Equal("flag-" + suffix);
        summary.Status!.Type.Should().Be("active"); summary.Status.ActiveFlags.Should().Equal("flag-" + suffix);
        summary.Section!.Id.Should().Be("section-" + suffix); summary.Section.Name.Should().Be("Section " + suffix);
        summary.Section.Appearance!.Color.Should().Be("color-" + suffix); summary.Section.Appearance.Icon.Should().Be("icon-" + suffix);
        summary.SectionEnteredAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(seconds));
        summary.CreatedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(seconds + 1)); summary.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(seconds + 2));
        summary.SourceKind.Should().Be("source-" + suffix); summary.ParentThreadId.Should().Be("parent-" + suffix); summary.CliVersion.Should().Be("version-" + suffix);
        summary.GitInfo!.Sha.Should().Be("sha-" + suffix); summary.GitInfo.Branch.Should().Be("branch-" + suffix); summary.GitInfo.OriginUrl.Should().Be("https://git.test/" + suffix);
    }

    [Theory]
    [InlineData("name", "canonical")]
    [InlineData("threadName", "thread alias")]
    [InlineData("title", "title alias")]
    [InlineData("preview", "preview fallback")]
    public void DisplayName_SelectsFirstAvailableAlias(string firstProperty, string expected)
    {
        var properties = new (string Name, string Value)[] { ("name", "canonical"), ("threadName", "thread alias"), ("title", "title alias"), ("preview", "preview fallback") };
        var payload = new JsonObject { ["id"] = "thread" };
        foreach (var (name, value) in properties.SkipWhile(p => p.Name != firstProperty)) payload[name] = value;
        CodexAppServerClientThreadParsers.ParseThreadSummary(Element(payload))!.Name.Should().Be(expected);
    }

    [Theory]
    [InlineData("data")]
    [InlineData("threads")]
    [InlineData("items")]
    [InlineData("sessions")]
    public void ThreadLists_SelectFirstAvailableRecognizedCollection(string firstCollection)
    {
        var names = new[] { "data", "threads", "items", "sessions" }; var payload = new JsonObject();
        foreach (var name in names.SkipWhile(n => n != firstCollection))
            payload[name] = new JsonArray(new JsonObject { ["id"] = name + "-thread" });
        CodexAppServerClientThreadParsers.ParseThreadListThreads(Element(payload)).Should().ContainSingle().Which.ThreadId.Should().Be(firstCollection + "-thread");
    }

    [Fact]
    public void EmptyPreferredCollection_DoesNotFallBackToOlderCollections()
    {
        var data = Json("""{"data":[],"threads":[{"id":"older"}],"items":[{"id":"oldest"}]}""");
        CodexAppServerClientThreadParsers.ParseThreadListThreads(data).Should().BeEmpty();
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(data).Should().BeEmpty();
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(Json("""{"data":["preferred"],"threads":["fallback"]}""")).Should().Equal("preferred");
    }

    [Theory]
    [InlineData("primary")]
    [InlineData("secondary")]
    public void TurnCount_DescribesParsedTurnsAfterMalformedEntriesAreSkipped(string owner)
    {
        var primary = new JsonObject { ["id"] = "thread" }; var secondary = new JsonObject();
        var turns = JsonNode.Parse("""[null,42,{"id":"kept","status":"completed","items":[]}]""");
        if (owner == "primary") { primary["turns"] = turns; secondary["turns"] = JsonNode.Parse("""[{"id":"ignored"},{"id":"also-ignored"}]"""); }
        else secondary["turns"] = turns;
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(Element(primary), Element(secondary))!;
        summary.TurnCount.Should().Be(1); summary.Turns.Should().ContainSingle().Which.Id.Should().Be("kept");
        CodexAppServerClientThreadParsers.ParseThreadSummary(Json("{\"id\":\"empty\"}"))!.TurnCount.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParentThread_UsesExplicitIdBeforeStructuredSourceAndPrimarySourceBeforeEnvelope(bool primarySource)
    {
        var primary = new JsonObject { ["id"] = "thread" }; var secondary = Metadata("secondary", 1, false); secondary.Remove("parentThreadId");
        if (primarySource) primary["source"] = Metadata("primary", 1, false)["source"]!.DeepClone();
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(Element(primary), Element(secondary))!;
        summary.ParentThreadId.Should().Be("source-parent-" + (primarySource ? "primary" : "secondary"));
        primary["parentThreadId"] = "explicit";
        CodexAppServerClientThreadParsers.ParseThreadSummary(Element(primary), Element(secondary))!.ParentThreadId.Should().Be("explicit");
    }

    [Fact]
    public void ParentAliases_PreferCamelCaseWithinTheSameStructuredSource()
    {
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(Json("""{"id":"thread","source":{"subAgent":{"threadSpawn":{"parentThreadId":"current","parent_thread_id":"legacy"}}}}"""))!;
        summary.ParentThreadId.Should().Be("current");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StringSource_UsesPrimaryValueAndFallsBackToEnvelope(bool primarySource)
    {
        var primary = new JsonObject { ["id"] = "thread" }; if (primarySource) primary["source"] = "primary-source";
        var secondary = Json("{\"source\":\"fallback-source\"}");
        CodexAppServerClientThreadParsers.ParseThreadSummary(Element(primary), secondary)!.SourceKind.Should().Be(primarySource ? "primary-source" : "fallback-source");
    }

    [Fact]
    public void BlankSourceKind_DoesNotHideStructuredSubAgentInformation()
    {
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(Json("""{"id":"thread","source":{"kind":" ","subAgent":{"review":{}}}}"""))!;
        summary.SourceKind.Should().Be("subAgentReview");
    }
}
