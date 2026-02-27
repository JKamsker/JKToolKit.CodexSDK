using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerClientCore
{
    private static readonly TimeSpan TurnNotificationBufferTtl = TimeSpan.FromSeconds(30);

    private void BufferTurnNotification(string turnId, AppServerNotification mapped, AppServerRpcNotification raw)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        lock (_turnsLock)
        {
            PruneStaleTurnBuffers(nowUtc);

            if (!_bufferedTurnNotificationsById.TryGetValue(turnId, out var buffer))
            {
                buffer = new TurnNotificationBuffer(nowUtc);
                _bufferedTurnNotificationsById[turnId] = buffer;
            }

            buffer.LastUpdatedUtc = nowUtc;
            var dropped = buffer.Enqueue(mapped, raw, _turnNotificationBufferCapacity);
            if (dropped > 0)
            {
                Interlocked.Add(ref _droppedBufferedTurnNotificationsCapacity, dropped);
            }
        }
    }

    private void PruneStaleTurnBuffers(DateTimeOffset nowUtc)
    {
        if (_bufferedTurnNotificationsById.Count == 0)
            return;

        var cutoff = nowUtc - TurnNotificationBufferTtl;

        List<string>? staleKeys = null;
        foreach (var (key, value) in _bufferedTurnNotificationsById)
        {
            if (value.LastUpdatedUtc < cutoff)
            {
                staleKeys ??= new List<string>();
                staleKeys.Add(key);
            }
        }

        if (staleKeys is null)
            return;

        foreach (var key in staleKeys)
        {
            if (_bufferedTurnNotificationsById.Remove(key, out var removed))
            {
                Interlocked.Add(ref _droppedBufferedTurnNotificationsTtl, removed.Items.Count);
            }
        }
    }

    private void FlushBufferedTurnNotifications(string turnId, CodexTurnHandle handle, TurnNotificationBuffer buffered)
    {
        foreach (var (mapped, raw) in buffered.Items)
        {
            TryWriteDroppingOldest(handle.EventsChannel, mapped, ref _droppedTurnNotifications);
            TryWriteDroppingOldest(handle.RawEventsChannel, raw, ref _droppedTurnRawNotifications);

            if (mapped is TurnCompletedNotification completed)
            {
                handle.CompletionTcs.TrySetResult(completed);
                handle.EventsChannel.Writer.TryComplete();
                handle.RawEventsChannel.Writer.TryComplete();
                RemoveTurnHandleLocked(turnId);
                break;
            }
        }
    }

    private sealed class TurnNotificationBuffer
    {
        public TurnNotificationBuffer(DateTimeOffset createdUtc)
        {
            LastUpdatedUtc = createdUtc;
        }

        public DateTimeOffset LastUpdatedUtc { get; set; }

        public List<(AppServerNotification Mapped, AppServerRpcNotification Raw)> Items { get; } = new();

        public int Enqueue(AppServerNotification mapped, AppServerRpcNotification raw, int capacity)
        {
            var dropped = 0;
            if (Items.Count >= capacity)
            {
                Items.RemoveAt(0);
                dropped++;
            }

            Items.Add((mapped, raw));
            return dropped;
        }
    }
}
