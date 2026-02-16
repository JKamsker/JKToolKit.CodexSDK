using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Represents a handle to a Codex session, providing access to metadata, event streaming,
/// and lifecycle operations for live or resumed sessions.
/// </summary>
public sealed class CodexSessionHandle : ICodexSessionHandle
{
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly Process? _process;
    private readonly ICodexProcessLauncher? _processLauncher;
    private readonly TimeSpan _processExitTimeout;
    private readonly TimeSpan? _idleTimeout;
    private readonly ILogger<CodexSessionHandle> _logger;
    private readonly IReadOnlyList<string>? _tempFilesToDeleteOnDispose;
    private bool _disposed;
    private readonly object _exitSync = new();
    private bool _exitSignaled;
    private int _exitCode;
    private SessionExitReason _exitReason = SessionExitReason.Unknown;
    private List<Action<int>> _exitCallbacks = new();
    private int _idleTerminationStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexSessionHandle"/> class.
    /// </summary>
    /// <param name="info">Session metadata for this handle.</param>
    /// <param name="tailer">JSONL tailer used to read session logs.</param>
    /// <param name="parser">Event parser used to map lines to strongly-typed events.</param>
    /// <param name="process">Optional running Codex process for live sessions.</param>
    /// <param name="processLauncher">Process launcher used for graceful termination of live sessions.</param>
    /// <param name="processExitTimeout">Timeout for graceful process termination.</param>
    /// <param name="idleTimeout">Optional idle timeout after which the Codex process will be terminated. Null disables idle termination.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tempFilesToDeleteOnDispose">Optional temp files to delete when the handle is disposed.</param>
    public CodexSessionHandle(
        CodexSessionInfo info,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        Process? process,
        ICodexProcessLauncher? processLauncher,
        TimeSpan processExitTimeout,
        TimeSpan? idleTimeout,
        ILogger<CodexSessionHandle> logger,
        IReadOnlyList<string>? tempFilesToDeleteOnDispose = null)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _tailer = tailer ?? throw new ArgumentNullException(nameof(tailer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _process = process;
        _processLauncher = processLauncher;
        _processExitTimeout = processExitTimeout;
        _idleTimeout = idleTimeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tempFilesToDeleteOnDispose = tempFilesToDeleteOnDispose;

        if (_process != null)
        {
            try
            {
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to subscribe to process exit for session {SessionId}", Info.Id);
            }
        }
    }

    /// <inheritdoc />
    public CodexSessionInfo Info { get; }

    /// <inheritdoc />
    public SessionExitReason ExitReason => _exitReason;

    /// <inheritdoc />
    public bool IsLive => !_disposed && _process is { HasExited: false };

    /// <inheritdoc />
    public IAsyncEnumerable<CodexEvent> GetEventsAsync(
        EventStreamOptions? options,
        CancellationToken cancellationToken) =>
        GetEventsInternalAsync(options, cancellationToken);

    private async IAsyncEnumerable<CodexEvent> GetEventsInternalAsync(
        EventStreamOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? EventStreamOptions.Default;
        var shouldFollow = effectiveOptions.Follow && IsLive;
        if (effectiveOptions.Follow != shouldFollow)
        {
            effectiveOptions = effectiveOptions with { Follow = shouldFollow };
        }

        using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var lineStream = _tailer.TailAsync(Info.LogPath, effectiveOptions, pipelineCts.Token);
        var parsedStream = _parser.ParseAsync(lineStream, pipelineCts.Token);
        var filteredStream = ApplyTimestampFilter(parsedStream, effectiveOptions, pipelineCts.Token);

        var finalStream = (_idleTimeout.HasValue && shouldFollow && _process != null && _processLauncher != null)
            ? ApplyIdleTimeout(filteredStream, _idleTimeout.Value, pipelineCts, cancellationToken)
            : filteredStream;

        await foreach (var evt in finalStream)
        {
            yield return evt;
        }
    }

    /// <inheritdoc />
    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_process == null)
        {
            throw new InvalidOperationException("Cannot wait for exit on a non-live session.");
        }

        await _process.WaitForExitAsync(cancellationToken);
        NotifyExitSafe(_process.ExitCode, SessionExitReason.Success);
        return _process.ExitCode;
    }

    /// <inheritdoc />
    public async Task<int> ExitAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_process == null)
        {
            throw new InvalidOperationException("Cannot exit a non-live session.");
        }

        if (_process.HasExited)
        {
            // Ensure callbacks are notified if not yet
            NotifyExitSafe(_process.ExitCode, SessionExitReason.Success);
            return _process.ExitCode;
        }

        if (_processLauncher == null)
        {
            throw new InvalidOperationException("Process launcher is not available to terminate the process.");
        }

        var code = await _processLauncher
            .TerminateProcessAsync(_process, _processExitTimeout, cancellationToken)
            .ConfigureAwait(false);

        // Ensure exit callbacks are fired (guarded for idempotency)
        NotifyExitSafe(code, SessionExitReason.Custom);
        return code;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_process != null)
            {
                if (!_process.HasExited && _processLauncher != null)
                {
                    try
                    {
                        await _processLauncher.TerminateProcessAsync(
                            _process,
                            _processExitTimeout,
                            CancellationToken.None).ConfigureAwait(false);
                        // Make sure callbacks run even if Exited didn't fire for any reason
                        NotifyExitSafe(_process.HasExited ? _process.ExitCode : -1, SessionExitReason.Custom);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error terminating process for session {SessionId}", Info.Id);
                    }
                }

                try
                {
                    _process.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error disposing process for session {SessionId}", Info.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error disposing process for session {SessionId}", Info.Id);
        }

        DeleteTempFilesBestEffort();

        await Task.CompletedTask;
    }

    private void DeleteTempFilesBestEffort()
    {
        if (_tempFilesToDeleteOnDispose is null || _tempFilesToDeleteOnDispose.Count == 0)
        {
            return;
        }

        foreach (var path in _tempFilesToDeleteOnDispose)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to delete temp file '{TempFile}' for session {SessionId}", path, Info.Id);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CodexSessionHandle));
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        try
        {
            var code = 0;
            try
            {
                code = _process?.ExitCode ?? 0;
            }
            catch
            {
                code = -1;
            }

            NotifyExitSafe(code, SessionExitReason.Success);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error while handling process exit for session {SessionId}", Info.Id);
        }
    }

    private void NotifyExitSafe(int code, SessionExitReason reason)
    {
        List<Action<int>>? callbacksToRun = null;
        lock (_exitSync)
        {
            if (_exitSignaled)
            {
                return;
            }

            _exitSignaled = true;
            _exitCode = code;
            _exitReason = reason;
            callbacksToRun = _exitCallbacks;
            _exitCallbacks = new List<Action<int>>();
        }

        if (callbacksToRun is { Count: > 0 })
        {
            foreach (var cb in callbacksToRun)
            {
                _ = Task.Run(() =>
                {
                    try { cb(code); }
                    catch (Exception ex) { _logger.LogDebug(ex, "OnExit callback threw for session {SessionId}", Info.Id); }
                });
            }
        }
    }

    /// <inheritdoc />
    public IDisposable OnExit(Action<int> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        ThrowIfDisposed();

        if (_process == null)
        {
            throw new InvalidOperationException("Cannot register exit callback on a non-live session.");
        }

        lock (_exitSync)
        {
            if (_exitSignaled)
            {
                var code = _exitCode;
                // Invoke outside lock
                Task.Run(() =>
                {
                    try { callback(code); }
                    catch (Exception ex) { _logger.LogDebug(ex, "OnExit callback threw for session {SessionId}", Info.Id); }
                });
                return NoopDisposable.Instance;
            }

            _exitCallbacks.Add(callback);
            return new Unsubscriber(this, callback);
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();
        public void Dispose() { }
    }

    private sealed class Unsubscriber : IDisposable
    {
        private readonly CodexSessionHandle _owner;
        private readonly Action<int> _callback;
        private bool _disposed;

        public Unsubscriber(CodexSessionHandle owner, Action<int> callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_owner._exitSync)
            {
                if (!_owner._exitSignaled)
                {
                    _owner._exitCallbacks.Remove(_callback);
                }
            }
        }
    }

    private async IAsyncEnumerable<CodexEvent> ApplyIdleTimeout(
        IAsyncEnumerable<CodexEvent> events,
        TimeSpan idleTimeout,
        CancellationTokenSource pipelineCts,
        [EnumeratorCancellation] CancellationToken userCancellationToken)
    {
        if (idleTimeout <= TimeSpan.Zero)
        {
            await foreach (var evt in events.WithCancellation(userCancellationToken))
            {
                yield return evt;
            }

            yield break;
        }

        long lastMessageTicks = 0;
        int idleTriggered = 0;
        using var monitorCts = CancellationTokenSource.CreateLinkedTokenSource(userCancellationToken, pipelineCts.Token);
        var monitorTask = MonitorIdleAsync(
            idleTimeout,
            () => Interlocked.Read(ref lastMessageTicks),
            pipelineCts,
            monitorCts.Token,
            () => Interlocked.Exchange(ref idleTriggered, 1));

        await using var enumerator = events.WithCancellation(monitorCts.Token).GetAsyncEnumerator();

        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (OperationCanceledException) when (Volatile.Read(ref idleTriggered) == 1 && !userCancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                if (!hasNext)
                {
                    yield break;
                }

                Interlocked.Exchange(ref lastMessageTicks, DateTimeOffset.UtcNow.UtcTicks);
                yield return enumerator.Current;
            }
        }
        finally
        {
            monitorCts.Cancel();
            try
            {
                await monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected if cancellation/token triggered
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Idle timeout monitor encountered an error for session {SessionId}", Info.Id);
            }
        }
    }

    private async Task MonitorIdleAsync(
        TimeSpan idleTimeout,
        Func<long> lastMessageTicksProvider,
        CancellationTokenSource pipelineCts,
        CancellationToken cancellationToken,
        Action onIdleTriggered)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var lastTicks = lastMessageTicksProvider();
            if (lastTicks == 0)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            var last = new DateTimeOffset(lastTicks, TimeSpan.Zero);
            var elapsed = DateTimeOffset.UtcNow - last;
            var remaining = idleTimeout - elapsed;

            if (remaining <= TimeSpan.Zero)
            {
                _logger.LogInformation(
                    "Idle timeout of {IdleTimeout} elapsed for session {SessionId}; terminating Codex process.",
                    idleTimeout,
                    Info.Id);

                onIdleTriggered();
                await TriggerIdleTerminationAsync().ConfigureAwait(false);
                pipelineCts.Cancel();
                return;
            }

            try
            {
                await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // cancellation or pipeline shut down
                break;
            }
        }
    }

    private async Task TriggerIdleTerminationAsync()
    {
        if (_process == null || _processLauncher == null)
        {
            return;
        }

        if (Interlocked.Exchange(ref _idleTerminationStarted, 1) != 0)
        {
            return;
        }

        try
        {
            var exitCode = await _processLauncher
                .TerminateProcessAsync(_process, _processExitTimeout, CancellationToken.None)
                .ConfigureAwait(false);

            NotifyExitSafe(exitCode, SessionExitReason.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to terminate Codex process for session {SessionId} after idle timeout.",
                Info.Id);
        }
    }

    private async IAsyncEnumerable<CodexEvent> ApplyTimestampFilter(
        IAsyncEnumerable<CodexEvent> events,
        EventStreamOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (options.AfterTimestamp.HasValue)
            {
                if (evt.Timestamp <= options.AfterTimestamp.Value)
                {
                    continue;
                }
            }

            yield return evt;
        }
    }
}
