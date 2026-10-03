using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade.Internal;

// Reservations outlive a canceled caller while an accepted turn is being reconciled.
internal sealed class CodexThreadExecutionState
{
    private readonly object _sync = new();
    private CodexOwnedTurn? _current;

    internal CodexOwnedTurn Reserve()
    {
        lock (_sync)
        {
            if (_current is not null && !_current.IsServerFinished)
                throw new InvalidOperationException("This thread already has an active or starting turn. Await completion or steer the existing turn.");
            return _current = new CodexOwnedTurn(this);
        }
    }

    internal void Release(CodexOwnedTurn turn)
    {
        lock (_sync)
            if (ReferenceEquals(_current, turn)) _current = null;
    }
}

internal sealed class CodexOwnedTurn(CodexThreadExecutionState owner)
{
    private readonly object _sync = new();
    private readonly TaskCompletionSource<CodexTurnHandle> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _startInvoked;
    private bool _abandoned;

    internal async Task<CodexTurnHandle> StartAsync(Func<Task<CodexTurnHandle>> start, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_abandoned) throw new OperationCanceledException("Turn startup was abandoned.");
            if (_startInvoked) throw new InvalidOperationException("Turn middleware must call next exactly once.");
            _startInvoked = true;
        }

        _ = ReconcileStartupAsync(start);
        try
        {
            return await _started.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            Abandon();
            throw;
        }
    }

    private async Task ReconcileStartupAsync(Func<Task<CodexTurnHandle>> start)
    {
        try
        {
            // Caller cancellation must not drop an accepted turn/start response and its turn ID.
            var handle = await start().ConfigureAwait(false);
            handle.KeepRegisteredUntilTerminal = true;
            _started.TrySetResult(handle);
            _ = ReleaseOnCompletionAsync(handle);
        }
        catch (Exception ex)
        {
            _started.TrySetException(ex);
            _ = _started.Task.Exception; // The pipeline also propagates this failure to its caller.
            owner.Release(this);
        }
    }

    internal bool IsServerFinished => _started.Task.IsCompletedSuccessfully && _started.Task.Result.ServerCompletion.IsCompleted;

    internal void ValidateReturnedHandle(CodexTurnHandle handle)
    {
        if (!_started.Task.IsCompletedSuccessfully || !ReferenceEquals(_started.Task.Result, handle))
            throw new InvalidOperationException("Turn middleware must return the handle from next.");
    }

    internal void Abandon()
    {
        lock (_sync)
        {
            if (_abandoned) return;
            _abandoned = true;
            if (!_startInvoked)
            {
                owner.Release(this);
                return;
            }
        }
        _ = CleanupAsync();
    }

    private async Task CleanupAsync()
    {
        try
        {
            var handle = await _started.Task.ConfigureAwait(false);
            if (!handle.ServerCompletion.IsCompleted)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await handle.InterruptAsync(timeout.Token).ConfigureAwait(false); }
                catch { /* Keep the reservation until terminal notification or connection shutdown. */ }
            }
            await handle.DisposeAsync().ConfigureAwait(false);
        }
        catch { /* Startup failure already releases the reservation and reaches the pipeline. */ }
    }

    private async Task ReleaseOnCompletionAsync(CodexTurnHandle handle)
    {
        await handle.ServerCompletion.ConfigureAwait(false);
        owner.Release(this); // Do not retain the completed handle/result in the shared SDK.
    }
}
