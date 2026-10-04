using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.ApprovalHandlers;

namespace JKToolKit.CodexSDK.Tests.Unit;

[CollectionDefinition("Console approval", DisableParallelization = true)]
public sealed class ConsoleApprovalCollection;

[Collection("Console approval")]
public sealed class PromptConsoleApprovalHandlerTests
{
    [Theory]
    [InlineData("execCommandApproval", "Y\n", "approved")]
    [InlineData("applyPatchApproval", "yes\n", "approved")]
    [InlineData("execCommandApproval", "n\n", "denied")]
    [InlineData("applyPatchApproval", "", "denied")]
    public async Task LegacyApproval_UsesExplicitConsent(string method, string input, string expected)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync(method, Json("{\"command\":\"pwd\"}"), default);
        result.GetProperty("decision").GetString().Should().Be(expected);
        console.Error.Should().Contain(method).And.Contain("pwd").And.Contain("Approve? [y/N]: ");
    }

    [Fact]
    public async Task LegacyApproval_AllowsMissingOrUndefinedParams()
    {
        using var console = new ConsoleScope("\n\n");
        var handler = new PromptConsoleApprovalHandler();
        (await handler.HandleAsync("applyPatchApproval", null, default)).GetProperty("decision").GetString().Should().Be("denied");
        (await handler.HandleAsync("execCommandApproval", default(JsonElement), default)).GetProperty("decision").GetString().Should().Be("denied");
        console.Error.Should().Contain("(no params)");
    }

    [Theory]
    [InlineData("item/commandExecution/requestApproval")]
    [InlineData("item/fileChange/requestApproval")]
    [InlineData("item/permissions/requestApproval")]
    [InlineData("mcpServer/elicitation/request")]
    [InlineData("item/tool/requestUserInput")]
    [InlineData("item/tool/call")]
    [InlineData("account/chatgptAuthTokens/refresh")]
    public async Task TypedRequests_RejectMissingNullAndMalformedParamsWithoutPrompting(string method)
    {
        using var console = new ConsoleScope("");
        var handler = new PromptConsoleApprovalHandler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(method, null, default).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(method, Json("null"), default).AsTask());
        await Assert.ThrowsAsync<JsonException>(() => handler.HandleAsync(method, Json("[]"), default).AsTask());
        console.Error.Should().BeEmpty();
    }

    [Theory]
    [InlineData("item/commandExecution/requestApproval", "y\n", "accept")]
    [InlineData("item/commandExecution/requestApproval", "yes\n", "accept")]
    [InlineData("item/commandExecution/requestApproval", "", "decline")]
    [InlineData("item/fileChange/requestApproval", "yes\n", "accept")]
    [InlineData("item/fileChange/requestApproval", "no\n", "decline")]
    [InlineData("item/fileChange/requestApproval", "\n", "decline")]
    public async Task ApprovalWithoutChoices_UsesExplicitConsent(string method, string input, string expected)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync(method, Json("""{"threadId":"thread","turnId":"turn","itemId":"item"}"""), default);
        result.GetProperty("decision").GetString().Should().Be(expected);
        console.Error.Should().Contain("threadId=thread turnId=turn itemId=item").And.Contain("Approve? [y/N]: ");
    }

    [Theory]
    [InlineData("item/commandExecution/requestApproval", "\n", "accept")]
    [InlineData("item/fileChange/requestApproval", "2\n", "decline")]
    [InlineData("item/fileChange/requestApproval", "", "accept")]
    public async Task AvailableDecisions_SelectNumberOrDefault(string method, string input, string expected)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync(method, Json("""{"threadId":"t","turnId":"u","itemId":"i","availableDecisions":["accept","decline"]}"""), default);
        result.GetProperty("decision").GetString().Should().Be(expected);
        console.Error.Should().Contain("Available decisions:").And.Contain("Decision [1]: ").And.Contain("1) accept").And.Contain("2) decline");
    }

    [Theory]
    [InlineData("0\n")]
    [InlineData("3\n")]
    [InlineData("junk\n")]
    public async Task AvailableDecisions_RejectInvalidSelection(string input)
    {
        using var console = new ConsoleScope(input);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptConsoleApprovalHandler().HandleAsync("item/fileChange/requestApproval", Json("""{"threadId":"t","turnId":"u","itemId":"i","availableDecisions":["accept","decline"]}"""), default).AsTask());
        error.Message.Should().Be("Invalid approval decision selection.");
    }

    [Fact]
    public async Task CommandApproval_DisplaysContextAndPreservesStructuredDecision()
    {
        using var console = new ConsoleScope("1\n");
        var result = await new PromptConsoleApprovalHandler().HandleAsync("item/commandExecution/requestApproval", Json("""{"kind":"writeStdin","threadId":"t","turnId":"u","itemId":"i","approvalId":"approval","reason":"because","command":"pwd","cwd":"/repo","networkApprovalContext":{"protocol":"https","host":"example.test"},"availableDecisions":[{"acceptWithExecpolicyAmendment":{"rule":"allow"}}]}"""), default);
        result.GetProperty("decision").GetProperty("acceptWithExecpolicyAmendment").GetProperty("rule").GetString().Should().Be("allow");
        console.Error.Should().Contain("kind=writeStdin").And.Contain("approvalId=approval").And.Contain("reason=because").And.Contain("command=pwd").And.Contain("cwd=/repo").And.Contain("network=https://example.test");
    }

    [Fact]
    public async Task FileApproval_DisplaysGrantRootAndReason()
    {
        using var console = new ConsoleScope("y\n");
        await new PromptConsoleApprovalHandler().HandleAsync("item/fileChange/requestApproval", Json("""{"threadId":"t","turnId":"u","itemId":"i","reason":"because","grantRoot":"/repo"}"""), default);
        console.Error.Should().Contain("reason=because").And.Contain("grantRoot=/repo");
    }

    [Theory]
    [InlineData("", false, "turn")]
    [InlineData("no\n", false, "turn")]
    [InlineData("y\ny\n", true, "session")]
    [InlineData("yes\nyes\n", true, "session")]
    [InlineData("yes\nn\n", true, "turn")]
    public async Task Permissions_RequireConsentAndSeparateSessionScope(string input, bool granted, string scope)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync("item/permissions/requestApproval", Json("""{"threadId":"t","turnId":"u","itemId":"i","environmentId":"environment","reason":"because","permissions":{"network":{"enabled":true}}}"""), default);
        result.GetProperty("scope").GetString().Should().Be(scope);
        result.GetProperty("permissions").TryGetProperty("network", out _).Should().Be(granted);
        console.Error.Should().Contain("environmentId=environment").And.Contain("reason=because");
    }

    [Theory]
    [InlineData("a\n{\"answer\":42}\n", "accept", "{\"answer\":42}")]
    [InlineData("accept\n\n", "accept", "{}")]
    [InlineData("y\n", "accept", "{}")]
    [InlineData("yes\n", "accept", "{}")]
    [InlineData("c\n", "cancel", null)]
    [InlineData("cancel\n", "cancel", null)]
    [InlineData("", "decline", null)]
    [InlineData("unknown\n", "decline", null)]
    public async Task FormElicitation_UsesActionAndParsesContent(string input, string action, string? content)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync("mcpServer/elicitation/request", Json("""{"threadId":"t","turnId":"u","serverName":"mcp","mode":"form","message":"Question","requestedSchema":{"type":"object"}}"""), default);
        result.GetProperty("action").GetString().Should().Be(action);
        if (content is null) result.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        else result.GetProperty("content").GetRawText().Should().Be(content);
        console.Error.Should().Contain("Question").And.Contain("object").And.Contain("Action? [a]ccept/[d]ecline/[c]ancel (default decline): ");
        if (action == "accept") console.Error.Should().Contain("Accepted form content as JSON (blank for {}): ");
    }

    [Fact]
    public async Task FormElicitation_ReportsInvalidJson()
    {
        using var console = new ConsoleScope("accept\nnot-json\n");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptConsoleApprovalHandler().HandleAsync("mcpServer/elicitation/request", Json("""{"threadId":"t","serverName":"mcp","mode":"form","message":"Question"}"""), default).AsTask());
        error.InnerException.Should().BeAssignableTo<JsonException>();
        error.Message.Should().Be("Accepted elicitation content must be valid JSON.");
    }

    [Fact]
    public async Task UrlElicitation_DisplaysLinkAndDoesNotReadContent()
    {
        using var console = new ConsoleScope("yes\nunconsumed\n");
        var result = await new PromptConsoleApprovalHandler().HandleAsync("mcpServer/elicitation/request", Json("""{"threadId":"t","serverName":"mcp","mode":"url","message":"Sign in","url":"https://example.test","elicitationId":"elicitation"}"""), default);
        result.GetProperty("content").ValueKind.Should().Be(JsonValueKind.Null);
        Console.ReadLine().Should().Be("unconsumed");
        console.Error.Should().Contain("turnId=n/a").And.Contain("url=https://example.test").And.Contain("elicitationId=elicitation");
    }

    [Theory]
    [InlineData("\n\n", true, false)]
    [InlineData("y\noutput\n", true, true)]
    [InlineData("yes\noutput\n", true, true)]
    [InlineData("no\n", false, false)]
    public async Task DynamicTool_RecordsSuccessAndOptionalText(string input, bool success, bool hasOutput)
    {
        using var console = new ConsoleScope(input);
        var result = await new PromptConsoleApprovalHandler().HandleAsync("item/tool/call", Json("""{"threadId":"t","turnId":"u","callId":"call","tool":"tool","arguments":{"x":1}}"""), default);
        result.GetProperty("success").GetBoolean().Should().Be(success);
        result.GetProperty("contentItems").GetArrayLength().Should().Be(hasOutput ? 1 : 0);
        if (hasOutput) result.GetProperty("contentItems")[0].GetProperty("text").GetString().Should().Be("output");
        console.Error.Should().Contain("callId=call tool=tool").And.Contain("\"x\":1");
    }

    [Fact]
    public async Task DynamicTool_MissingArgumentsRejectsMalformedRequest()
    {
        using var console = new ConsoleScope("");
        await Assert.ThrowsAsync<JsonException>(() => new PromptConsoleApprovalHandler().HandleAsync("item/tool/call", Json("{}"), default).AsTask());
        console.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task UserInput_MapsOptionNumbersAndPreservesFreeText()
    {
        using var console = new ConsoleScope(" 1,2,0,3, custom ,,\nfree text\n");
        var result = await new PromptConsoleApprovalHandler().HandleAsync("item/tool/requestUserInput", Json("""{"threadId":"t","turnId":"u","itemId":"i","questions":[{"id":"choice","header":"Pick","question":"Which?","isOther":true,"options":[{"label":"First","description":"one"},{"label":"Second","description":"two"}]},{"id":"text","header":"Text","question":"Explain"},{"id":"empty","header":"Empty","question":"Optional","options":[]}]}"""), default);
        result.GetProperty("answers").GetProperty("choice").GetProperty("answers").EnumerateArray().Select(x => x.GetString()).Should().Equal("First", "Second", "0", "3", "custom");
        result.GetProperty("answers").GetProperty("text").GetProperty("answers")[0].GetString().Should().Be("free text");
        result.GetProperty("answers").GetProperty("empty").GetProperty("answers").GetArrayLength().Should().Be(0);
        console.Error.Should().Contain("1) First — one").And.Contain("Other: enter free-form text");
    }

    [Fact]
    public async Task SecretInput_EditsKeysWithoutEchoingSecret()
    {
        using var console = new ConsoleScope("");
        var handler = HandlerWithKeys('\b', 's', 'x', '\b', '\t', 'e', 'c', 'r', 'e', 't', '\r');
        var result = await handler.HandleAsync("item/tool/requestUserInput", Json("""{"threadId":"t","turnId":"u","itemId":"i","questions":[{"id":"password","header":"Password","question":"Password","isSecret":true}]}"""), default);
        result.GetProperty("answers").GetProperty("password").GetProperty("answers")[0].GetString().Should().Be("secret");
        console.Error.Should().Contain("input hidden").And.NotContain("secret").And.EndWith(Environment.NewLine);
    }

    [Theory]
    [InlineData("account\nplus\n", "plus")]
    [InlineData("account\n\n", null)]
    public async Task TokenRefresh_ReturnsTokensWithoutEchoingThem(string input, string? plan)
    {
        using var console = new ConsoleScope(input);
        var result = await HandlerWithKeys('t', 'o', 'k', 'e', 'n', '\r').HandleAsync("account/chatgptAuthTokens/refresh", Json("""{"reason":"unauthorized","previousAccountId":"old"}"""), default);
        result.GetProperty("accessToken").GetString().Should().Be("token");
        result.GetProperty("chatgptAccountId").GetString().Should().Be("account");
        result.GetProperty("chatgptPlanType").GetString().Should().Be(plan);
        console.Error.Should().Contain("previousAccountId=old").And.NotContain("\ntoken");
    }

    [Theory]
    [InlineData("")]
    [InlineData("account\n\n")]
    public async Task TokenRefresh_RequiresAccountAndToken(string input)
    {
        using var console = new ConsoleScope(input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => HandlerWithKeys('\r').HandleAsync("account/chatgptAuthTokens/refresh", Json("""{"reason":"unauthorized"}"""), default).AsTask());
        console.Error.Should().Contain("previousAccountId=n/a");
    }

    [Fact]
    public async Task UnknownRequest_ThrowsWithoutPrompting()
    {
        using var console = new ConsoleScope("");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PromptConsoleApprovalHandler().HandleAsync("unknown", null, default).AsTask());
        error.Message.Should().Contain("unknown");
        console.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelledRequest_DoesNotPromptOrReadInput()
    {
        using var console = new ConsoleScope("unconsumed\n");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PromptConsoleApprovalHandler().HandleAsync("applyPatchApproval", null, new CancellationToken(true)).AsTask());
        console.Error.Should().BeEmpty();
        Console.ReadLine().Should().Be("unconsumed");
    }

    [Fact]
    public void Handler_RejectsMissingKeyboardReader() =>
        Assert.Throws<ArgumentNullException>(() => new PromptConsoleApprovalHandler(null!));

    [Fact]
    public async Task UrlElicitation_MissingIdDisplaysPlaceholder()
    {
        using var console = new ConsoleScope("cancel\n");
        await new PromptConsoleApprovalHandler().HandleAsync("mcpServer/elicitation/request", Json("""{"threadId":"t","serverName":"mcp","mode":"url","message":"Sign in","url":"https://example.test"}"""), default);
        console.Error.Should().Contain("elicitationId=n/a");
    }

    private static PromptConsoleApprovalHandler HandlerWithKeys(params char[] chars)
    {
        var keys = new Queue<ConsoleKeyInfo>(chars.Select(c => new ConsoleKeyInfo(c, c switch { '\r' => ConsoleKey.Enter, '\b' => ConsoleKey.Backspace, _ => ConsoleKey.A }, false, false, false)));
        return new PromptConsoleApprovalHandler(() => keys.Dequeue());
    }

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private sealed class ConsoleScope : IDisposable
    {
        private readonly TextReader _originalInput = Console.In;
        private readonly TextWriter _originalError = Console.Error;
        private readonly StringReader _input;
        private readonly StringWriter _error = new();
        public string Error => _error.ToString();

        public ConsoleScope(string input)
        {
            _input = new StringReader(input);
            Console.SetIn(_input);
            Console.SetError(_error);
        }

        public void Dispose()
        {
            Console.SetIn(_originalInput);
            Console.SetError(_originalError);
            _input.Dispose();
            _error.Dispose();
        }
    }
}
