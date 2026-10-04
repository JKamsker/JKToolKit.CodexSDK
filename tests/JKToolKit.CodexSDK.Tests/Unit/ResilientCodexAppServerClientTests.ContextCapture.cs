using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class ResilientCodexAppServerClientTests
{
    [Theory]
    [InlineData("connect")]
    [InlineData("operation")]
    [InlineData("restart")]
    [InlineData("policy")]
    [InlineData("delay")]
    [InlineData("hook")]
    public async Task AsyncExecution_DoesNotRequireCallerSynchronizationContext(string suspensionPoint)
    {
        var startGate = new TaskCompletionSource<ICodexAppServerClientAdapter>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operationGate = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var policyGate = new TaskCompletionSource<CodexAppServerRetryDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hookGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var starts = 0;
        var adapter = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                calls++;
                if (calls == 1 && suspensionPoint is "restart" or "policy" or "delay" or "hook")
                {
                    Exception failure = suspensionPoint == "restart"
                        ? Disconnect("restart", exitCode: 42)
                        : new JsonRpcRemoteException(new JsonRpcError(-32001, "server overloaded", null));
                    return Task.FromException<JsonElement>(failure);
                }
                return suspensionPoint == "operation" ? operationGate.Task : Task.FromResult(EmptyJson());
            }
        };
        var replacement = new FakeAdapter { CallAsyncImpl = (_, _, _) => Task.FromResult(EmptyJson()) };
        await using var client = new ResilientCodexAppServerClient(_ =>
        {
            starts++;
            return suspensionPoint == "connect" || (suspensionPoint == "restart" && starts == 2)
                ? startGate.Task
                : Task.FromResult<ICodexAppServerClientAdapter>(adapter);
        }, new()
        {
            RetryPolicy = _ => suspensionPoint == "policy"
                ? new ValueTask<CodexAppServerRetryDecision>(policyGate.Task)
                : ValueTask.FromResult(CodexAppServerRetryDecision.Retry(
                    delay: suspensionPoint == "delay" ? TimeSpan.FromMilliseconds(50) : null,
                    beforeRetryAsync: suspensionPoint == "hook" ? _ => hookGate.Task : null))
        }, NullLogger.Instance);
        if (suspensionPoint != "connect") await client.EnsureConnectedAsync();

        var context = new CountingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        Task<JsonElement> request;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            request = client.CallAsync("custom/read", null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        startGate.TrySetResult(suspensionPoint == "restart" ? replacement : adapter);
        operationGate.TrySetResult(EmptyJson());
        policyGate.TrySetResult(CodexAppServerRetryDecision.Retry());
        hookGate.TrySetResult();
        (await request.WaitAsync(TimeSpan.FromSeconds(5))).ValueKind.Should().Be(JsonValueKind.Object);
        context.PostCount.Should().Be(0, $"the {suspensionPoint} await must not depend on a UI or application synchronization context");
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;
        public int PostCount => Volatile.Read(ref _postCount);
        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
    }
}
