using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>A runnable thread backed by an SDK-owned app-server connection.</summary>
public sealed class CodexThreadSession
{
    private readonly CodexThreads _owner;
    private readonly IReadOnlyList<ICodexTurnMiddleware> _middleware;
    private readonly CodexThreads.ExecutionState _state;

    internal CodexThreadSession(CodexThreads owner, CodexThread thread, IReadOnlyList<ICodexTurnMiddleware> middleware)
    { _owner = owner; Thread = thread; _middleware = middleware; _state = owner.GetState(thread.Id); }

    /// <summary>Gets the thread identifier.</summary>
    public string Id => Thread.Id;
    /// <summary>Gets the initial thread lifecycle response.</summary>
    public CodexThread Thread { get; }

    /// <summary>Runs user text and collects its terminal result.</summary>
    public Task<CodexTurnResult> RunAsync(string prompt, CancellationToken ct = default) => RunAsync(CodexInput.Text(prompt), ct);
    /// <summary>Runs trust-labelled input and collects its terminal result.</summary>
    public Task<CodexTurnResult> RunAsync(CodexInput input, CancellationToken ct = default)
    { ArgumentNullException.ThrowIfNull(input); return RunAsync(input.ToOptions(), ct); }
    /// <summary>Runs configured input and collects its result. Cancellation requests interruption before disposing the handle.</summary>
    public async Task<CodexTurnResult> RunAsync(TurnStartOptions options, CancellationToken ct = default)
    {
        await using var turn = await RunStreamedAsync(options, ct).ConfigureAwait(false);
        try { return await turn.RunAsync(ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await turn.InterruptAsync(timeout.Token).ConfigureAwait(false); }
            catch { /* Preserve the caller's cancellation if the connection is already gone. */ }
            throw;
        }
    }

    /// <summary>Starts user text and returns the turn handle for streaming, collection, or interruption.</summary>
    public Task<CodexTurnHandle> RunStreamedAsync(string prompt, CancellationToken ct = default) => RunStreamedAsync(CodexInput.Text(prompt), ct);
    /// <summary>Starts trust-labelled input and returns a turn handle.</summary>
    public Task<CodexTurnHandle> RunStreamedAsync(CodexInput input, CancellationToken ct = default)
    { ArgumentNullException.ThrowIfNull(input); return RunStreamedAsync(input.ToOptions(), ct); }
    /// <summary>Starts a configured turn. An overlapping run on this session is rejected; use SteerAsync explicitly.</summary>
    public async Task<CodexTurnHandle> RunStreamedAsync(TurnStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await _state.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state.Active is { Completion.IsCompleted: false })
                throw new InvalidOperationException("This thread already has an active turn. Await completion or steer the existing turn.");
            var client = await _owner.GetClientAsync(ct).ConfigureAwait(false);
            var context = new CodexTurnContext(Id, options);
            Func<CancellationToken, Task<CodexTurnHandle>> next = token => client.StartTurnAsync(Id, options, token);
            foreach (var middleware in _middleware.Reverse())
            {
                var continuation = next;
                next = token => middleware.StartAsync(context, continuation, token);
            }
            _state.Active = await next(ct).ConfigureAwait(false);
            return _state.Active;
        }
        finally { _state.Gate.Release(); }
    }
}
