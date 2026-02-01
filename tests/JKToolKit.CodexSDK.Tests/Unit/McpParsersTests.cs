using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.McpServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpParsersTests
{
    [Fact]
    public void ToolsListParser_ParsesToolDescriptors()
    {
        using var doc = JsonDocument.Parse("""
        {
          "tools": [
            { "name": "codex", "description": "start", "inputSchema": { "type": "object" } },
            { "name": "codex-reply" }
          ]
        }
        """);

        var tools = McpToolsListParser.Parse(doc.RootElement);
        tools.Should().HaveCount(2);
        tools[0].Name.Should().Be("codex");
        tools[0].InputSchema.Should().NotBeNull();
    }

    [Fact]
    public void CodexResultParser_ExtractsThreadId_AndText()
    {
        using var doc = JsonDocument.Parse("""
        {
          "content": [ { "type": "text", "text": "hello" } ],
          "structuredContent": { "threadId": "th_123" }
        }
        """);

        var parsed = CodexMcpResultParser.Parse(doc.RootElement);
        parsed.ThreadId.Should().Be("th_123");
        parsed.Text.Should().Be("hello");
        parsed.StructuredContent.ValueKind.Should().Be(JsonValueKind.Object);
    }
}

