using System.Text.Json;
using System.Threading.Channels;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a running (or completed) turn on a Codex app-server thread.
/// </summary>
public sealed class CodexTurnHandle : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _interrupt;
    private readonly Func<IReadOnlyList<TurnInputItem>, CancellationToken, Task<string>>? _steer;
    private readonly Func<IReadOnlyList<TurnInputItem>, CancellationToken, Task<TurnSteerResult>>? _steerRaw;
    private readonly Action _onDispose;
    private int _disposed;
    private readonly object _observersLock = new();
    private readonly Internal.CodexTurnCollector _collector = new();
    private readonly List<CodexTurnSubscription> _observers = [];
    private readonly TaskCompletionSource<CodexTurnResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _observersCompleted;
    private Exception? _observerError;
    private readonly TaskCompletionSource _serverFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task ServerCompletion => _serverFinished.Task;
    internal bool KeepRegisteredUntilTerminal { get; set; }

    /// <summary>Collects the terminal result without consuming events. Cancellation only stops waiting.</summary>
    public Task<CodexTurnResult> RunAsync(CancellationToken ct = default) => _result.Task.WaitAsync(ct);

    /// <summary>Subscribes immediately to future events with an independent drop-oldest queue; no replay.</summary>
    public CodexTurnSubscription Subscribe(int capacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        lock (_observersLock)
        {
            var subscription = new CodexTurnSubscription(capacity, RemoveObserver);
            if (_observersCompleted) subscription.Complete(_observerError);
            else _observers.Add(subscription);
            return subscription;
        }
    }

    private void RemoveObserver(CodexTurnSubscription observer)
    {
        lock (_observersLock) _observers.Remove(observer);
    }

    internal void Observe(AppServerNotification notification)
    {
        lock (_observersLock)
        {
            if (_observersCompleted) return;
            _collector.Observe(notification);
            foreach (var observer in _observers) observer.Publish(notification);
        }
    }

    internal void Observe(AppServerNotification notification, AppServerRpcNotification raw,
        ref long droppedTyped, ref long droppedRaw)
    {
        lock (_observersLock)
        {
            if (_observersCompleted) return;
            Observe(notification);
            // Publish atomically with observation termination. A closed bounded queue must not
            // be treated as a full queue: draining it would erase events already promised to readers.
            Internal.CodexAppServerClientCore.TryWriteDroppingOldest(EventsChannel, notification, ref droppedTyped);
            Internal.CodexAppServerClientCore.TryWriteDroppingOldest(RawEventsChannel, raw, ref droppedRaw);
        }
    }

    internal void MarkPartial()
    {
        lock (_observersLock) _collector.IsPartial = true;
    }

    internal void Complete(TurnCompletedNotification completed)
    {
        _serverFinished.TrySetResult();
        lock (_observersLock)
        {
            if (_observersCompleted) return;
            _result.TrySetResult(_collector.Complete(ThreadId, TurnId, completed));
            CompletionTcs.TrySetResult(completed);
            CompleteObservers(null);
        }
        EventsChannel.Writer.TryComplete();
        RawEventsChannel.Writer.TryComplete();
    }

    internal void Terminate(Exception? error = null)
    {
        _serverFinished.TrySetResult();
        TerminateObservation(error);
    }

    private void TerminateObservation(Exception? error = null)
    {
        lock (_observersLock)
        {
            if (_observersCompleted) return;
            if (error is null)
            {
                _result.TrySetCanceled();
                CompletionTcs.TrySetCanceled();
            }
            else
            {
                _result.TrySetException(error);
                CompletionTcs.TrySetException(error);
            }
            CompleteObservers(error);
        }
        EventsChannel.Writer.TryComplete(error);
        RawEventsChannel.Writer.TryComplete(error);
    }

    private void CompleteObservers(Exception? error)
    {
        _observersCompleted = true;
        _observerError = error;
        foreach (var observer in _observers) observer.Complete(error);
        _observers.Clear();
    }

    internal Channel<AppServerNotification> EventsChannel { get; }
    internal Channel<AppServerRpcNotification> RawEventsChannel { get; }
    internal TaskCompletionSource<TurnCompletedNotification> CompletionTcs { get; }

    /// <summary>
    /// Gets the owning thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the raw JSON response returned by <c>turn/start</c>.
    /// Contains the full upstream <c>Turn</c> payload including <c>status</c> and <c>error</c>.
    /// </summary>
    public JsonElement? RawStartResponse { get; }

    /// <summary>
    /// Gets a task that completes when the server reports the turn completion.
    /// </summary>
    public Task<TurnCompletedNotification> Completion => CompletionTcs.Task;

    internal CodexTurnHandle(
        string threadId,
        string turnId,
        Func<CancellationToken, Task> interrupt,
        Func<IReadOnlyList<TurnInputItem>, CancellationToken, Task<string>>? steer,
        Func<IReadOnlyList<TurnInputItem>, CancellationToken, Task<TurnSteerResult>>? steerRaw,
        Action onDispose,
        int bufferCapacity,
        JsonElement? rawStartResponse = null)
    {
        ThreadId = threadId;
        TurnId = turnId;
        RawStartResponse = rawStartResponse;
        _interrupt = interrupt;
        _steer = steer;
        _steerRaw = steerRaw;
        _onDispose = onDispose;

        EventsChannel = System.Threading.Channels.Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(bufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        RawEventsChannel = System.Threading.Channels.Channel.CreateBounded<AppServerRpcNotification>(new BoundedChannelOptions(bufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

        CompletionTcs = new TaskCompletionSource<TurnCompletedNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Subscribes to this turn's event stream.
    /// </summary>
    /// <remarks>
    /// This stream is backed by a bounded, drop-oldest queue. Each event is delivered to at most one consumer.
    /// If you enumerate this stream multiple times concurrently, events will be distributed across readers
    /// (queue semantics), not broadcast (pub-sub).
    /// </remarks>
    public IAsyncEnumerable<AppServerNotification> Events(CancellationToken ct = default) =>
        EventsChannel.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Subscribes to this turn's raw JSON-RPC notification stream (method + params).
    /// </summary>
    /// <remarks>
    /// This stream is backed by a bounded, drop-oldest queue. Each event is delivered to at most one consumer.
    /// If you enumerate this stream multiple times concurrently, events will be distributed across readers
    /// (queue semantics), not broadcast (pub-sub).
    /// </remarks>
    public IAsyncEnumerable<AppServerRpcNotification> EventsRaw(CancellationToken ct = default) =>
        RawEventsChannel.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Requests that the server interrupt the turn.
    /// </summary>
    public Task InterruptAsync(CancellationToken ct = default) => _interrupt(ct);

    /// <summary>
    /// Sends additional input to an in-progress turn via <c>turn/steer</c>.
    /// </summary>
    public Task<string> SteerAsync(IReadOnlyList<TurnInputItem> input, CancellationToken ct = default)
    {
        if (_steer is null)
        {
            throw new NotSupportedException("This turn handle does not support steering.");
        }

        ArgumentNullException.ThrowIfNull(input);
        return _steer(input, ct);
    }

    /// <summary>
    /// Sends additional input to an in-progress turn via <c>turn/steer</c> and returns the raw JSON result payload.
    /// </summary>
    /// <remarks>
    /// Steering is best-effort and may race with turn completion. Cancellation stops waiting for the response but does
    /// not guarantee the server did not apply the steer request.
    /// </remarks>
    public Task<TurnSteerResult> SteerRawAsync(IReadOnlyList<TurnInputItem> input, CancellationToken ct = default)
    {
        if (_steerRaw is null)
        {
            throw new NotSupportedException("This turn handle does not support raw steering results.");
        }

        ArgumentNullException.ThrowIfNull(input);
        return _steerRaw(input, ct);
    }

    /// <summary>
    /// Disposes the handle and completes the event stream.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return ValueTask.CompletedTask;
        }

        if (!KeepRegisteredUntilTerminal || ServerCompletion.IsCompleted)
        {
            _onDispose();
            _serverFinished.TrySetResult();
        }
        TerminateObservation();

        return ValueTask.CompletedTask;
    }
}
