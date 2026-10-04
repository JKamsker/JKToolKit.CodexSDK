using System.Text.Json;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.McpServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpParserBoundaryBehaviorTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("{\"tools\":true}")]
    [InlineData("{}")]
    public void InvalidToolsList_ReturnsFalseAndEmptyPublicResult(string json)
    {
        var raw = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.False(McpToolsListParser.TryParse(raw, out var tools, out var cursor));
        Assert.Empty(tools);
        Assert.Null(cursor);
        Assert.Empty(CodexMcpToolResultParsers.ParseToolsList(raw));
    }

    [Fact]
    public void ToolsList_SnakeCursorAndMalformedEntries_AreHandledWithoutLosingValidTools()
    {
        using var doc = JsonDocument.Parse("""{"nextCursor":5,"next_cursor":"page2","tools":[null,5,{}, {"name":" "},{"name":3},{"name":"tool","description":4,"inputSchema":true}]}""");
        Assert.True(McpToolsListParser.TryParse(doc.RootElement, out var tools, out var cursor));
        Assert.Equal("page2", cursor);
        var tool = Assert.Single(tools);
        Assert.Equal("tool", tool.Name);
        Assert.Null(tool.Description);
        Assert.True(tool.InputSchema!.Value.GetBoolean());
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("[]", null)]
    [InlineData("{\"content\":[null,{}, {\"text\":null},{\"text\":\"\"}]}", null)]
    [InlineData("{\"content\":[{\"text\":\" \"}],\"structuredContent\":{\"content\":\"fallback\"}}", "fallback")]
    [InlineData("{\"structured_content\":{\"content\":\"snake\"}}", "snake")]
    [InlineData("{\"structuredContent\":{\"content\":3},\"structured_content\":{\"content\":\"snake\"}}", "snake")]
    [InlineData("{\"structuredContent\":{\"content\":null},\"structured_content\":3}", null)]
    public void TextExtraction_HandlesInvalidBlocksAndStructuredFallback(string json, string? expected)
    {
        Assert.Equal(expected, CodexMcpToolResultParsers.TryExtractText(JsonSerializer.Deserialize<JsonElement>(json)));
    }

    [Theory]
    [InlineData("threadId")]
    [InlineData("thread_id")]
    [InlineData("conversationId")]
    [InlineData("conversation_id")]
    public void ThreadIdAliases_AreSupportedAtRootAndWithinStructuredContent(string key)
    {
        var root = JsonSerializer.SerializeToElement(new Dictionary<string, object?> { [key] = "root-id" });
        Assert.Equal("root-id", CodexMcpResultParser.Parse(root).ThreadId);
        var nested = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["threadId"] = "root-id", ["structuredContent"] = new Dictionary<string, object?> { [key] = "nested-id" }
        });
        Assert.Equal("nested-id", CodexMcpResultParser.Parse(nested).ThreadId);
        var fallback = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            [key] = "root-id", ["structuredContent"] = new { threadId = " " }
        });
        Assert.Equal("root-id", CodexMcpResultParser.Parse(fallback).ThreadId);
    }
}
