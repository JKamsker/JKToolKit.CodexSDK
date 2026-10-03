using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerClientCore
{
    private static readonly TimeSpan TurnNotificationBufferTtl = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, int> _pendingTurnStarts = new(StringComparer.Ordinal);

    internal IDisposable TrackTurnStart(string threadId)
    {
        lock (_turnsLock)
        {
            _pendingTurnStarts.TryGetValue(threadId, out var count);
            _pendingTurnStarts[threadId] = count + 1;
        }
        return new PendingTurnStart(this, threadId);
    }

    private sealed class PendingTurnStart(CodexAppServerClientCore owner, string threadId) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._turnsLock)
            {
                var count = owner._pendingTurnStarts[threadId];
                if (count == 1) owner._pendingTurnStarts.Remove(threadId);
                else owner._pendingTurnStarts[threadId] = count - 1;
            }
        }
    }

    private void BufferTurnNotification(string turnId, AppServerNotification mapped, AppServerRpcNotification raw)
    {
        var nowUtc = DateTimeOffset.UtcNow;

        lock (_turnsLock)
        {
            PruneStaleTurnBuffers(nowUtc);

            if (!_bufferedTurnNotificationsById.TryGetValue(turnId, out var buffer))
            {
                var threadId = raw.Params.ValueKind == JsonValueKind.Object &&
                    raw.Params.TryGetProperty("threadId", out var thread) && thread.ValueKind == JsonValueKind.String
                    ? thread.GetString() : null;
                buffer = new TurnNotificationBuffer(nowUtc, threadId);
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
            // Keep early data (including terminal completion) until its startup response is reconciled.
            // Missing thread metadata is treated conservatively while any startup is outstanding.
            if (_pendingTurnStarts.ContainsKey(string.Empty) ||
                (value.ThreadId is null ? _pendingTurnStarts.Count > 0 : _pendingTurnStarts.ContainsKey(value.ThreadId)))
                continue;
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
        if (buffered.Dropped) handle.MarkPartial();
        foreach (var (mapped, raw) in buffered.Items)
        {
            handle.Observe(mapped);
            TryWriteDroppingOldest(handle.EventsChannel, mapped, ref _droppedTurnNotifications);
            TryWriteDroppingOldest(handle.RawEventsChannel, raw, ref _droppedTurnRawNotifications);

            var completed = mapped as TurnCompletedNotification;
            if (completed is null && raw.Method == "turn/completed")
                completed = SafeMap(raw.Method, raw.Params) as TurnCompletedNotification;
            if (completed is not null)
            {
                handle.Complete(completed);
                RemoveTurnHandleLocked(turnId);
                break;
            }
        }
    }

    private sealed class TurnNotificationBuffer
    {
        public TurnNotificationBuffer(DateTimeOffset createdUtc, string? threadId)
        {
            LastUpdatedUtc = createdUtc;
            ThreadId = threadId;
        }

        public string? ThreadId { get; }

        public bool Dropped { get; private set; }

        public DateTimeOffset LastUpdatedUtc { get; set; }

        public List<(AppServerNotification Mapped, AppServerRpcNotification Raw)> Items { get; } = new();

        public int Enqueue(AppServerNotification mapped, AppServerRpcNotification raw, int capacity)
        {
            var dropped = 0;
            if (Items.Count >= capacity)
            {
                Dropped = true;
                Items.RemoveAt(0);
                dropped++;
            }

            Items.Add((mapped, raw));
            return dropped;
        }
    }
}
