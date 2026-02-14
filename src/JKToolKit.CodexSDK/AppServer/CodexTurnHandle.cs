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

    internal Channel<AppServerNotification> EventsChannel { get; }
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
        int bufferCapacity)
    {
        ThreadId = threadId;
        TurnId = turnId;
        _interrupt = interrupt;
        _steer = steer;
        _steerRaw = steerRaw;
        _onDispose = onDispose;

        EventsChannel = System.Threading.Channels.Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(bufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        CompletionTcs = new TaskCompletionSource<TurnCompletedNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Subscribes to this turn's event stream.
    /// </summary>
    public IAsyncEnumerable<AppServerNotification> Events(CancellationToken ct = default) =>
        EventsChannel.Reader.ReadAllAsync(ct);

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

        _onDispose();
        EventsChannel.Writer.TryComplete();
        CompletionTcs.TrySetCanceled();

        return ValueTask.CompletedTask;
    }
}
