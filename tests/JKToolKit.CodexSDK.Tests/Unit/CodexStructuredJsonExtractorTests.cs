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
}

