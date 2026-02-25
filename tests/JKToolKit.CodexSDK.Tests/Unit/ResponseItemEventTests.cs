using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class ResponseItemEventTests
{
    private readonly JsonlEventParser _parser = new(NullLogger<JsonlEventParser>.Instance);

    [Fact]
    public async Task ParsesReasoningResponseItem_WithSummaryText()
    {
        var line = """{"timestamp":"2025-11-21T10:53:36.569Z","type":"response_item","payload":{"type":"reasoning","summary":[{"type":"summary_text","text":"**Planning read-only exploration**"}],"content":null,"encrypted_content":"gAAAAA..."}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("reasoning");
        var payload = response.Payload.Should().BeOfType<ReasoningResponseItemPayload>().Subject;
        payload.SummaryTexts.Should().ContainSingle("**Planning read-only exploration**");
    }

    [Fact]
    public async Task ParsesMessageResponseItem_WithTextParts()
    {
        var line = """{"timestamp":"2025-11-21T10:53:37Z","type":"response_item","payload":{"type":"message","role":"assistant","content":[{"type":"output_text","text":"Hello there"}]}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("message");
        var payload = response.Payload.Should().BeOfType<MessageResponseItemPayload>().Subject;
        payload.Role.Should().Be("assistant");
        payload.TextParts.Should().ContainSingle("Hello there");
    }

    [Fact]
    public async Task ParsesFunctionCallResponseItem_WithArguments()
    {
        var line = """{"timestamp":"2025-11-21T10:53:38Z","type":"response_item","payload":{"type":"function_call","name":"shell_command","arguments":{"command":"ls"},"call_id":"call_123"}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("function_call");
        var payload = response.Payload.Should().BeOfType<FunctionCallResponseItemPayload>().Subject;
        payload.Name.Should().Be("shell_command");
        payload.ArgumentsJson.Should().Contain("ls");
        payload.CallId.Should().Be("call_123");
    }

    [Fact]
    public async Task ParsesWebSearchCallResponseItem_WithUrlAndPattern()
    {
        var line = """
                   {"timestamp":"2025-11-21T10:53:38Z","type":"response_item","payload":{"type":"web_search_call","status":"completed","action":{"type":"find_in_page","url":"https://example.com","pattern":"foo"}}}
                   """;

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("web_search_call");
        var payload = response.Payload.Should().BeOfType<WebSearchCallResponseItemPayload>().Subject;
        payload.Status.Should().Be("completed");
        payload.Action!.Type.Should().Be("find_in_page");
        payload.Action.Url.Should().Be("https://example.com");
        payload.Action.Pattern.Should().Be("foo");
    }

    [Fact]
    public async Task ParsesLocalShellCallResponseItem_WithExecAction()
    {
        var line = """
                   {"timestamp":"2025-11-21T10:53:39Z","type":"response_item","payload":{"type":"local_shell_call","call_id":"call_1","status":"completed","action":{"type":"exec","command":["bash","-lc","ls"],"timeout_ms":123,"working_directory":"/tmp","env":{"A":"B"},"user":"root"}}}
                   """;

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("local_shell_call");
        var payload = response.Payload.Should().BeOfType<LocalShellCallResponseItemPayload>().Subject;
        payload.CallId.Should().Be("call_1");
        payload.Status.Should().Be("completed");
        payload.ActionType.Should().Be("exec");
        payload.Command.Should().ContainInOrder("bash", "-lc", "ls");
        payload.TimeoutMs.Should().Be(123);
        payload.WorkingDirectory.Should().Be("/tmp");
        payload.Env.Should().ContainKey("A").WhoseValue.Should().Be("B");
        payload.User.Should().Be("root");
    }

    [Fact]
    public async Task ParsesCompactionSummaryResponseItem_AsCompactionPayload()
    {
        var line = """{"timestamp":"2025-11-21T10:53:40Z","type":"response_item","payload":{"type":"compaction_summary","encrypted_content":"gAAAAA..."}}""";

        var evt = await ParseSingleAsync(line);

        var response = Assert.IsType<ResponseItemEvent>(evt);
        response.PayloadType.Should().Be("compaction_summary");
        var payload = response.Payload.Should().BeOfType<CompactionResponseItemPayload>().Subject;
        payload.EncryptedContent.Should().Be("gAAAAA...");
    }

    private async Task<CodexEvent> ParseSingleAsync(string line)
    {
        var singleLine = GetSingleLineAsync(line);
        await foreach (var evt in _parser.ParseAsync(singleLine))
        {
            return evt;
        }
        throw new InvalidOperationException("No event parsed.");
    }

    private static async IAsyncEnumerable<string> GetSingleLineAsync(string line)
    {
        yield return line;
        await Task.CompletedTask;
    }
}
