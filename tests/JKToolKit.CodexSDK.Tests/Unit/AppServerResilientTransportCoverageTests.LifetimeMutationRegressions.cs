using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Fact]
    public async Task Disposal_CancelsInFlightApprovalHandlerBeforeTransportCleanup()
    {
        var handler = new PendingApprovalHandler();
        var rpc = new RecordingRpc("{}");
        await using var core = new CodexAppServerClientCore(new() { ApprovalHandler = handler }, new Process(), rpc, NullLogger.Instance, false);
        var request = rpc.OnServerRequest!(new JsonRpcRequest(JsonRpcId.FromNumber(1), "item/tool/requestUserInput", null)).AsTask();
        try
        {
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            rpc.DisposeOverride = () =>
            {
                handler.Token.IsCancellationRequested.Should().BeTrue("pending approval work must be canceled before closing its transport");
                return Task.CompletedTask;
            };
            await core.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            await request.WaitAsync(TimeSpan.FromSeconds(5));
            handler.Token.IsCancellationRequested.Should().BeTrue();
        }
        finally
        {
            handler.Release.TrySetResult();
            await request.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("interrupted")]
    public async Task TerminalNotification_ReleasesRegistrationBeforeHandleDisposal(string status)
    {
        var rpc = new RecordingRpc("{}");
        await using var core = new CodexAppServerClientCore(new(), new Process(), rpc, NullLogger.Instance, false);
        var disposed = false;
        await using var handle = new CodexTurnHandle("thread-1", "turn-1", _ => Task.CompletedTask, null, null,
            () => { disposed = true; core.RemoveTurnHandle("turn-1"); }, 10);
        core.RegisterTurnHandle("turn-1", handle);
        core.TryGetTurnHandle("turn-1", out var registered).Should().BeTrue();
        registered.Should().BeSameAs(handle);
        await rpc.EmitAsync("turn/completed", JsonSerializer.SerializeToElement(new { threadId = "thread-1", turn = new { id = "turn-1", status } }));
        var completion = await handle.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        completion.TurnId.Should().Be("turn-1");
        disposed.Should().BeFalse();
        core.TryGetTurnHandle("turn-1", out _).Should().BeFalse("a long-lived connection must not retain completed turns until callers dispose their handles");
    }

    private sealed class PendingApprovalHandler : IAppServerApprovalHandler
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public async ValueTask<JsonElement> HandleAsync(string method, JsonElement? parameters, CancellationToken ct)
        {
            Token = ct;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return JsonSerializer.SerializeToElement(new { });
        }
    }
}
