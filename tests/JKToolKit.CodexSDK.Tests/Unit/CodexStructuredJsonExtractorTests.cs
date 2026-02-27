using JKToolKit.CodexSDK.StructuredOutputs;
using FluentAssertions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexStructuredJsonExtractorTests
{
    [Fact]
    public void ExtractJson_Strict_RequiresPureJson()
    {
        var json = CodexStructuredJsonExtractor.ExtractJson("{\"a\":1}", tolerant: false);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_StripsJsonCodeFence()
    {
        var raw = "```json\n{\"a\":1}\n```";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_ExtractsEmbeddedJsonObject()
    {
        var raw = "Here you go:\n\n{\"a\":1,\"b\":[2,3]}\n\nThanks.";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1,\"b\":[2,3]}");
    }

    [Fact]
    public void ExtractJson_Tolerant_ScansAllCodeFences_AndSelectsParseableJsonFence()
    {
        var raw = """
                  ```text
                  not json
                  ```

                  ```json
                  {"a":1}
                  ```
                  """;

        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_IgnoresMarkdownBracketsBeforeJson()
    {
        var raw = "See [link](https://example.com) then {\"a\":1}";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_IgnoresNonJsonBracesBeforeValidJson()
    {
        var raw = "{not json} then {\"a\":1}";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_RecoversAfterUnbalancedBrace()
    {
        var raw = "prefix { unbalanced {\"a\":1} suffix";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"a\":1}");
    }

    [Fact]
    public void ExtractJson_Tolerant_PrefersLastParseableJsonValue_WhenMultipleArePresent()
    {
        var raw = "first {\"a\":1} second {\"b\":2}";
        var json = CodexStructuredJsonExtractor.ExtractJson(raw, tolerant: true);
        json.Should().Be("{\"b\":2}");
    }
}

