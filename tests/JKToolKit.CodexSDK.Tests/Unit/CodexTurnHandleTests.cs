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
            steerRaw: null,
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
            steerRaw: null,
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
            steerRaw: null,
            onDispose: () => disposed = true,
            bufferCapacity: 10);

        await handle.DisposeAsync();

        disposed.Should().BeTrue();
        handle.Completion.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task SteerRawAsync_Throws_WhenNotSupported()
    {
        await using var handle = new CodexTurnHandle(
            threadId: "t",
            turnId: "u",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            steerRaw: null,
            onDispose: () => { },
            bufferCapacity: 10);

        var act = async () => await handle.SteerRawAsync([TurnInputItem.Text("x")]);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task SteerRawAsync_InvokesSteerRawDelegate()
    {
        var called = false;

        await using var handle = new CodexTurnHandle(
            threadId: "t",
            turnId: "u",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            steerRaw: (input, _) =>
            {
                called = true;
                input.Should().HaveCount(1);
                return Task.FromResult(new TurnSteerResult
                {
                    TurnId = "u",
                    Raw = default
                });
            },
            onDispose: () => { },
            bufferCapacity: 10);

        var result = await handle.SteerRawAsync([TurnInputItem.Text("x")]);
        called.Should().BeTrue();
        result.TurnId.Should().Be("u");
    }
}

