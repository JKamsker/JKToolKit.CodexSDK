using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexTurnHandleTests
{
    [Fact]
    public async Task SteerAsync_Throws_WhenNotSupported()
    {
        await using var handle = new CodexTurnHandle(
            threadId: "t",
            turnId: "u",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            onDispose: () => { },
            bufferCapacity: 10);

        var act = async () => await handle.SteerAsync([TurnInputItem.Text("x")]);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task SteerAsync_InvokesSteerDelegate()
    {
        var called = false;

        await using var handle = new CodexTurnHandle(
            threadId: "t",
            turnId: "u",
            interrupt: _ => Task.CompletedTask,
            steer: (input, _) =>
            {
                called = true;
                input.Should().HaveCount(1);
                return Task.FromResult("u");
            },
            onDispose: () => { },
            bufferCapacity: 10);

        var turnId = await handle.SteerAsync([TurnInputItem.Text("x")]);
        called.Should().BeTrue();
        turnId.Should().Be("u");
    }

    [Fact]
    public async Task DisposeAsync_CancelsCompletion()
    {
        var disposed = false;

        await using var handle = new CodexTurnHandle(
            threadId: "t",
            turnId: "u",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            onDispose: () => disposed = true,
            bufferCapacity: 10);

        await handle.DisposeAsync();

        disposed.Should().BeTrue();
        handle.Completion.IsCanceled.Should().BeTrue();
    }
}

