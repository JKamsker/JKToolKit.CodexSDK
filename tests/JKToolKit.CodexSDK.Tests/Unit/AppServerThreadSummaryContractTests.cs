using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerThreadSummaryContractTests
{
    private static JsonElement Json(string text) => JsonSerializer.Deserialize<JsonElement>(text);

    [Theory]
    [InlineData("data")]
    [InlineData("threads")]
    [InlineData("items")]
    [InlineData("sessions")]
    public void ThreadList_AcceptsLegacyCollectionsAndFiltersInvalidEntries(string collection)
    {
        var result = CodexAppServerClientThreadParsers.ParseThreadListThreads(Json("{\"" + collection + "\":[null,{}, {\"id\":\"t\",\"title\":\"Title\"}]}"));
        var thread = result.Should().ContainSingle().Subject;
        thread.ThreadId.Should().Be("t"); thread.Name.Should().Be("Title");
    }

    [Fact]
    public void MissingListsAndCursorsAreEmpty()
    {
        CodexAppServerClientThreadParsers.ParseThreadListThreads(Json("{}")).Should().BeEmpty();
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(Json("{}")).Should().BeEmpty();
        CodexAppServerClientThreadParsers.ExtractNextCursor(Json("{}")).Should().BeNull();
        CodexAppServerClientThreadParsers.ExtractNextCursor(Json("{\"cursor\":\"old\"}")).Should().Be("old");
        CodexAppServerClientThreadParsers.ExtractNextCursor(Json("{\"nextCursor\":\"new\",\"cursor\":\"old\"}")).Should().Be("new");
    }

    [Theory]
    [InlineData("data")]
    [InlineData("threads")]
    public void LoadedThreads_HandlesIdsAndObjectsInWireOrder(string collection) =>
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(Json("{\"" + collection + "\":[\"first\",\" \",null,{}, {\"id\":\"second\"}, {\"thread\":{\"id\":\"third\"}}]}"))
            .Should().Equal("first", "second", "third");

    [Theory]
    [InlineData("\"cli\"", "cli", null)]
    [InlineData("{\"kind\":\"kind\",\"type\":\"ignored\"}", "kind", null)]
    [InlineData("{\"type\":\"type\"}", "type", null)]
    [InlineData("{\"subAgent\":{\"review\":{}}}", "subAgentReview", null)]
    [InlineData("{\"subAgent\":{\"compact\":{}}}", "subAgentCompact", null)]
    [InlineData("{\"subAgent\":{\"threadSpawn\":{\"parentThreadId\":\"parent\"}}}", "subAgentThreadSpawn", "parent")]
    [InlineData("{\"subAgent\":{\"thread_spawn\":{\"parent_thread_id\":\"legacy\"}}}", "subAgentThreadSpawn", "legacy")]
    [InlineData("{\"subAgent\":{\"future\":{}}}", "subAgentOther", null)]
    [InlineData("{\"FUTURE\":{}}", "future", null)]
    [InlineData("{\" \":{},\"NEXT\":{}}", "next", null)]
    [InlineData("{}", null, null)]
    public void ThreadSource_MapsStructuredOriginsAndParentAliases(string source, string? expected, string? parent)
    {
        var thread = CodexAppServerClientThreadParsers.ParseThreadSummary(Json("{\"id\":\"t\",\"source\":" + source + "}"))!;
        thread.SourceKind.Should().Be(expected); thread.ParentThreadId.Should().Be(parent);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("\"456\"", 456)]
    [InlineData("null", null)]
    [InlineData("\"bad\"", null)]
    [InlineData("1.5", null)]
    public void SectionTimestamp_ParsesNumericAndStringUnixSeconds(string value, int? seconds)
    {
        var thread = CodexAppServerClientThreadParsers.ParseThreadSummary(Json("{\"id\":\"t\",\"sectionEnteredAt\":" + value + "}"))!;
        thread.SectionEnteredAt.Should().Be(seconds is { } s ? DateTimeOffset.FromUnixTimeSeconds(s) : null);
    }

    [Theory]
    [InlineData("{}", "data array")]
    [InlineData("{\"data\":[null]}", "must be objects")]
    [InlineData("{\"data\":[{}]}", "id")]
    [InlineData("{\"data\":[{\"id\":\"s\"}]}", "name")]
    public void Sections_RejectMalformedResponses(string json, string error)
    {
        Action parse = () => CodexAppServerClientThreadParsers.ParseThreadSectionListPage(Json(json));
        parse.Should().Throw<InvalidOperationException>().WithMessage("*" + error + "*");
    }

    [Fact]
    public void SectionResult_RequiresSectionAndRetainsAbsentAppearance()
    {
        Action parse = () => CodexAppServerClientThreadParsers.ParseThreadSectionResult(Json("{}"), "threadSection/create");
        parse.Should().Throw<InvalidOperationException>().WithMessage("threadSection/create*section object*");
        var result = CodexAppServerClientThreadParsers.ParseThreadSectionResult(Json("""{"section":{"id":"s","name":"Section"}}"""), "threadSection/create");
        result.Section!.Id.Should().Be("s"); result.Section.Name.Should().Be("Section"); result.Section.Appearance.Should().BeNull();
        result.Section.Raw.GetProperty("name").GetString().Should().Be("Section");
    }
}
