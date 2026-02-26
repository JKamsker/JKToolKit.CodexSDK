using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.McpServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexMcpResultParserTests
{
    [Fact]
    public void Parse_ConcatenatesAllContentTextBlocks()
    {
        var raw = Parse("""{"content":[{"text":"a"},{"text":"b"}]}""");
        var parsed = CodexMcpResultParser.Parse(raw);
        parsed.Text.Should().Be("ab");
    }

    [Fact]
    public void Parse_ThreadIdFallbacks_ToStructuredContentSnakeCase()
    {
        var raw = Parse("""{"structuredContent":{"thread_id":"t1"}}""");
        var parsed = CodexMcpResultParser.Parse(raw);
        parsed.ThreadId.Should().Be("t1");
    }

    [Fact]
    public void Parse_ThreadIdFallbacks_ToTopLevelConversationId()
    {
        var raw = Parse("""{"conversationId":"t2"}""");
        var parsed = CodexMcpResultParser.Parse(raw);
        parsed.ThreadId.Should().Be("t2");
    }

    [Fact]
    public void Parse_ThreadIdFallbacks_ToTopLevelConversationSnakeCase()
    {
        var raw = Parse("""{"conversation_id":"t3"}""");
        var parsed = CodexMcpResultParser.Parse(raw);
        parsed.ThreadId.Should().Be("t3");
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}

