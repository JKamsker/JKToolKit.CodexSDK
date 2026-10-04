using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class ResilientCodexAppServerClientTests
{
    [Theory]
    [InlineData("exec")]
    [InlineData("write")]
    [InlineData("resize")]
    [InlineData("terminate")]
    public async Task CommandOperation_AwaitDoesNotCaptureApplicationContext(string kind)
    {
        var response = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new CodexAppServerCommandExecClient((_, _, _) => response.Task);
        var context = new CountingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        Task request;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            request = kind switch
            {
                "exec" => client.CommandExecAsync(new() { Command = ["echo"] }),
                "write" => client.CommandExecWriteAsync(new() { ProcessId = "process-1", CloseStdin = true }),
                "resize" => client.CommandExecResizeAsync(new() { ProcessId = "process-1", Size = new() { Rows = 24, Columns = 80 } }),
                _ => client.CommandExecTerminateAsync(new() { ProcessId = "process-1" })
            };
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        request.IsCompleted.Should().BeFalse();
        response.SetResult(JsonSerializer.SerializeToElement(new { exitCode = 0, stdout = "", stderr = "" }));
        await request.WaitAsync(TimeSpan.FromSeconds(5));
        context.PostCount.Should().Be(0);
    }
}
