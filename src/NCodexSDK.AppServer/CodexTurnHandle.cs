using System.Text.Json;
using System.Threading.Channels;
using NCodexSDK.AppServer.Notifications;

namespace NCodexSDK.AppServer;

public sealed class CodexTurnHandle : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _interrupt;
    private readonly Action _onDispose;
    private int _disposed;

    internal Channel<AppServerNotification> EventsChannel { get; }
    internal TaskCompletionSource<TurnCompletedNotification> CompletionTcs { get; }

    public string ThreadId { get; }
    public string TurnId { get; }

    public Task<TurnCompletedNotification> Completion => CompletionTcs.Task;

    internal CodexTurnHandle(
        string threadId,
        string turnId,
        Func<CancellationToken, Task> interrupt,
        Action onDispose,
        int bufferCapacity)
    {
        ThreadId = threadId;
        TurnId = turnId;
        _interrupt = interrupt;
        _onDispose = onDispose;

        EventsChannel = System.Threading.Channels.Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(bufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        CompletionTcs = new TaskCompletionSource<TurnCompletedNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public IAsyncEnumerable<AppServerNotification> Events(CancellationToken ct = default) =>
        EventsChannel.Reader.ReadAllAsync(ct);

    public Task InterruptAsync(CancellationToken ct = default) => _interrupt(ct);

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        _onDispose();
        EventsChannel.Writer.TryComplete();
        CompletionTcs.TrySetCanceled();

        return ValueTask.CompletedTask;
    }
}
