using System.Collections.Concurrent;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>Starts and resumes high-level threads on a shared SDK-owned connection.</summary>
public sealed class CodexThreads
{
    private readonly CodexAppServerFacade _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IReadOnlyList<ICodexTurnMiddleware> _middleware;
    private CodexAppServerClient? _client;
    private bool _disposed;
    private readonly ConcurrentDictionary<string, ExecutionState> _states = new(StringComparer.Ordinal);
    internal ExecutionState GetState(string threadId) => _states.GetOrAdd(threadId, _ => new());
    internal sealed class ExecutionState
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal CodexTurnHandle? Active;
    }

    internal CodexThreads(CodexAppServerFacade factory, IReadOnlyList<ICodexTurnMiddleware>? middleware = null)
    {
        _factory = factory;
        _middleware = middleware ?? Array.Empty<ICodexTurnMiddleware>();
    }

    internal async Task<CodexAppServerClient> GetClientAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _client ??= await _factory.StartAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Starts a thread. Its connection is disposed with the owning SDK.</summary>
    public async Task<CodexThreadSession> StartAsync(ThreadStartOptions? options = null, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct).ConfigureAwait(false);
        var thread = await client.StartThreadAsync(options ?? new ThreadStartOptions(), ct).ConfigureAwait(false);
        return new CodexThreadSession(this, thread, _middleware);
    }

    /// <summary>Resumes a persisted thread using its identifier.</summary>
    public async Task<CodexThreadSession> ResumeAsync(string threadId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        var client = await GetClientAsync(ct).ConfigureAwait(false);
        return new CodexThreadSession(this, await client.ResumeThreadAsync(threadId, ct).ConfigureAwait(false), _middleware);
    }

    internal async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_client is not null) await _client.DisposeAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
