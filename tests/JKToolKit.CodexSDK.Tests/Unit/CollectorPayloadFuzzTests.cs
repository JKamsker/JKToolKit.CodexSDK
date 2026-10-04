using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using Xunit.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CollectorPayloadFuzzTests(ITestOutputHelper output)
{
    private static readonly string[] Keys = ["id", "type", "text", "phase", "content", "items", "status", "error",
        "output", "exitCode", "durationMs", "command", "cwd", "changes", "path", "kind", "diff", "tool", "server",
        "arguments", "result", "last", "total", "totalTokens", "inputTokens", "outputTokens", "modelContextWindow"];
    private static readonly string[] Types = ["agentMessage", "userMessage", "reasoning", "commandExecution",
        "fileChange", "mcpToolCall", "dynamicToolCall", "webSearch", "imageView", "imageGeneration", "collabAgentToolCall",
        "enteredReviewMode", "exitedReviewMode", "contextCompaction", "functionCallOutput", "futureUnknownType"];

    [Theory]
    [InlineData(1)]
    [InlineData(97)]
    [InlineData(43121)]
    [InlineData(8675309)]
    public async Task StructuredMalformedPayloads_KeepCollectionLiveAndRepeatable(int seed)
    {
        var random = new Random(seed);
        var count = TurnStateFuzzTests.Cases * 10;
        for (var iteration = 0; iteration < count; iteration++)
        {
            await using var handle = TurnStateFuzzTests.NewHandle();
            object? Shape(int depth) => random.Next(depth >= 4 ? 6 : 9) switch
            {
                0 => null,
                1 => random.Next(2) == 0,
                2 => random.NextInt64(),
                3 => random.Next(2) == 0 ? 1e100 : -1e100,
                4 => "\ud83d\ude80\n\"\\",
                5 => "",
                6 => Enumerable.Range(0, random.Next(5)).Select(_ => Shape(depth + 1)).ToArray(),
                _ => Keys.OrderBy(_ => random.Next()).Take(random.Next(8)).ToDictionary(key => key, _ => Shape(depth + 1))
            };
            var item = Keys.OrderBy(_ => random.Next()).Take(random.Next(12)).ToDictionary(key => key, _ => Shape(0));
            item["type"] = Types[random.Next(Types.Length)];
            if (random.Next(2) == 0) item["id"] = $"i{random.Next(3)}";
            var payload = JsonSerializer.SerializeToElement(new { threadId = "t", turnId = "u", item, tokenUsage = Shape(0), diff = Shape(0) });
            var context = $"seed={seed}, case={iteration}, payload={payload.GetRawText()}";
            var failure = await Record.ExceptionAsync(async () =>
            {
                foreach (var method in new[] { "item/completed", "thread/tokenUsage/updated", "turn/diff/updated" })
                    handle.Observe(AppServerNotificationMapper.Map(method, payload));
                var terminal = JsonSerializer.SerializeToElement(new { id = "u", status = "completed", items = new[] { item }, error = Shape(0) });
                handle.Complete(new TurnCompletedNotification("t", terminal, default));
                var result = await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(2));
                Assert.Equal("completed", result.Status);
                Assert.Same(result, await handle.RunAsync());
                Assert.Equal("u", result.TurnId);
            });
            Assert.True(failure is null, context + "; " + failure);
        }
        output.WriteLine($"seed={seed}; structured payload cases={count}");
    }
}
