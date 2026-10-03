using System.Threading.Channels;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>An independent, bounded observer of future turn events. Dispose to unsubscribe.</summary>
public sealed class CodexTurnSubscription : IAsyncDisposable
{
    private readonly Channel<AppServerNotification> _channel;
    private readonly Action<CodexTurnSubscription> _remove;
    private long _dropped;
    private int _disposed;

    internal CodexTurnSubscription(int capacity, Action<CodexTurnSubscription> remove)
    {
        _remove = remove;
        _channel = Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        }, _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary>Gets the number of notifications dropped because this observer was too slow.</summary>
    public long DroppedEvents => Interlocked.Read(ref _dropped);

    /// <summary>Reads this subscription's queue. Use one reader per subscription.</summary>
    public IAsyncEnumerable<AppServerNotification> Events(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);

    internal void Publish(AppServerNotification notification) => _channel.Writer.TryWrite(notification);
    internal void Complete(Exception? error = null) => _channel.Writer.TryComplete(error);

    /// <summary>Unsubscribes and completes this observer's queue.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _remove(this);
            Complete();
        }
        return ValueTask.CompletedTask;
    }
}
