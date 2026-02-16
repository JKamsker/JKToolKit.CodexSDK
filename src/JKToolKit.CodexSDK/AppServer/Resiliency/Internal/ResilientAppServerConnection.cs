using System.Runtime.ExceptionServices;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.AppServer.Resiliency.Internal;

internal sealed class ResilientAppServerConnection : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<ICodexAppServerClientAdapter>> _startInner;
    private readonly CodexAppServerResilienceOptions _options;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Queue<DateTimeOffset> _restartTimes = new();

    private volatile ICodexAppServerClientAdapter? _inner;
    private long _innerVersion;
    private int _restartCount;
    private volatile CodexAppServerConnectionState _state = CodexAppServerConnectionState.Connected;
    private volatile Exception? _fault;
    private CodexAppServerDisconnectedException? _lastDisconnect;
    private Task? _exitMonitorTask;

    public ResilientAppServerConnection(
        Func<CancellationToken, Task<ICodexAppServerClientAdapter>> startInner,
        CodexAppServerResilienceOptions options,
        ILogger logger)
    {
        _startInner = startInner ?? throw new ArgumentNullException(nameof(startInner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public CodexAppServerConnectionState State => _state;

    public CodexAppServerRestartEvent? LastRestart { get; private set; }

    public int RestartCount => Volatile.Read(ref _restartCount);

    public CancellationToken DisposeToken => _disposeCts.Token;

    public void ThrowIfFaultedOrDisposed()
    {
        if (_state == CodexAppServerConnectionState.Disposed)
        {
            throw new ObjectDisposedException(nameof(ResilientAppServerConnection));
        }

        var fault = _fault;
        if (fault is not null)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
            throw new InvalidOperationException("Unreachable.");
        }
    }

    public void RememberDisconnect(Exception ex)
    {
        if (ex is CodexAppServerDisconnectedException d)
        {
            _lastDisconnect = d;
        }
    }

    public static bool IsDisconnect(Exception ex) =>
        ex is CodexAppServerDisconnectedException ||
        ex is JsonRpcConnectionClosedException ||
        (ex is JsonRpcProtocolException p && p.InnerException is JsonRpcConnectionClosedException);

    public async Task EnsureConnectedAsync(CancellationToken ct) =>
        _ = await EnsureConnectedWithVersionAsync(ct).ConfigureAwait(false);

    public async Task<(ICodexAppServerClientAdapter Inner, long Version)> EnsureConnectedWithVersionAsync(CancellationToken ct)
    {
        ThrowIfFaultedOrDisposed();

        var existing = _inner;
        if (existing is not null)
        {
            return (existing, Volatile.Read(ref _innerVersion));
        }

        await _restartLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfFaultedOrDisposed();

            if (_inner is not null)
            {
                return (_inner, Volatile.Read(ref _innerVersion));
            }

            _state = CodexAppServerConnectionState.Restarting;
            var created = await _startInner(ct).ConfigureAwait(false);
            SetInner(created);
            _state = CodexAppServerConnectionState.Connected;
            return (created, Volatile.Read(ref _innerVersion));
        }
        finally
        {
            _restartLock.Release();
        }
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        var version = Volatile.Read(ref _innerVersion);
        await EnsureRestartedAsync(version, trigger: null, reason: "manual-restart", ct).ConfigureAwait(false);
    }

    public async Task EnsureRestartedAsync(long expectedVersion, Exception? trigger, string? reason, CancellationToken ct)
    {
        ThrowIfFaultedOrDisposed();
        ct.ThrowIfCancellationRequested();

        await _restartLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfFaultedOrDisposed();

            if (expectedVersion != Volatile.Read(ref _innerVersion))
            {
                return;
            }

            if (!_options.AutoRestart)
            {
                return;
            }

            _state = CodexAppServerConnectionState.Restarting;

            var previousDisconnect = trigger as CodexAppServerDisconnectedException ?? _lastDisconnect;
            var prevExitCode = previousDisconnect?.ExitCode;
            var prevStderrTail = previousDisconnect?.StderrTail ?? Array.Empty<string>();

            var policy = _options.RestartPolicy ?? CodexAppServerRestartPolicy.Default;

            var previous = _inner;
            _inner = null;

            if (previous is not null)
            {
                try { await previous.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
            }

            Exception? lastStartFailure = null;
            var consecutiveFailures = 0;
            while (true)
            {
                // enforce restart window (sliding) over SUCCESSFUL restarts only
                var now = DateTimeOffset.UtcNow;
                while (_restartTimes.Count > 0 && (now - _restartTimes.Peek()) > policy.Window)
                {
                    _restartTimes.Dequeue();
                }

                if (_restartTimes.Count >= policy.MaxRestarts)
                {
                    var ex = new CodexAppServerUnavailableException(
                        $"App-server restart limit reached ({policy.MaxRestarts} restarts within {policy.Window}).",
                        innerException: lastStartFailure ?? trigger);
                    Fault(ex);
                    throw ex;
                }

                var windowAttempt = _restartTimes.Count + 1;
                var delay = ComputeBackoff(policy, windowAttempt);
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogDebug(
                        "Delaying app-server restart by {Delay} (planned successful restart #{Attempt} in current window; max {Max}).",
                        delay,
                        windowAttempt,
                        policy.MaxRestarts);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }

                try
                {
                    var created = await _startInner(ct).ConfigureAwait(false);
                    SetInner(created);

                    // Only count successful restarts toward MaxRestarts.
                    var successNow = DateTimeOffset.UtcNow;
                    while (_restartTimes.Count > 0 && (successNow - _restartTimes.Peek()) > policy.Window)
                    {
                        _restartTimes.Dequeue();
                    }
                    _restartTimes.Enqueue(successNow);

                    consecutiveFailures = 0;
                    break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastStartFailure = ex;
                    consecutiveFailures++;
                    _logger.LogWarning(
                        ex,
                        "Failed to restart codex app-server (planned successful restart #{Attempt} in current window; max {Max}).",
                        windowAttempt,
                        policy.MaxRestarts);

                    if (consecutiveFailures >= policy.MaxRestarts)
                    {
                        var fail = new CodexAppServerUnavailableException(
                            $"Failed to restart codex app-server after {consecutiveFailures} consecutive attempts.",
                            innerException: lastStartFailure ?? trigger);
                        Fault(fail);
                        throw fail;
                    }
                }
            }

            var restartCount = Interlocked.Increment(ref _restartCount);
            var evt = new CodexAppServerRestartEvent
            {
                RestartCount = restartCount,
                Timestamp = DateTimeOffset.UtcNow,
                PreviousExitCode = prevExitCode,
                Reason = reason,
                PreviousStderrTail = prevStderrTail
            };
            LastRestart = evt;

            try { _options.OnRestart?.Invoke(evt); } catch { /* ignore */ }

            _logger.LogInformation("Restarted codex app-server (restart #{RestartCount}).", restartCount);
            _state = CodexAppServerConnectionState.Connected;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var fail = ex is CodexAppServerUnavailableException
                ? ex
                : new CodexAppServerUnavailableException("Failed to restart codex app-server.", ex);
            Fault(fail);
            throw fail;
        }
        finally
        {
            _restartLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _state = CodexAppServerConnectionState.Disposed;
        _disposeCts.Cancel();

        ICodexAppServerClientAdapter? inner;
        await _restartLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            inner = _inner;
            _inner = null;
        }
        finally
        {
            _restartLock.Release();
        }

        if (inner is not null)
        {
            try { await inner.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
        }

        try { _disposeCts.Dispose(); } catch { /* ignore */ }
        _restartLock.Dispose();
    }

    private void SetInner(ICodexAppServerClientAdapter created)
    {
        _inner = created;
        var newVersion = Interlocked.Increment(ref _innerVersion);

        _exitMonitorTask = Task.Run(async () =>
        {
            try
            {
                await created.ExitTask.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            if (_disposeCts.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await EnsureRestartedAsync(newVersion, trigger: _lastDisconnect, reason: "process-exited", _disposeCts.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
                // swallow - operations will surface fault if needed
            }
        });
    }

    private void Fault(Exception ex)
    {
        _fault = ex;
        _state = CodexAppServerConnectionState.Faulted;
    }

    private static TimeSpan ComputeBackoff(CodexAppServerRestartPolicy policy, int windowAttempt)
    {
        if (windowAttempt <= 1)
        {
            return TimeSpan.Zero;
        }

        var exponent = windowAttempt - 2;
        var factor = Math.Pow(2, Math.Min(exponent, 10));
        var raw = TimeSpan.FromMilliseconds(policy.InitialBackoff.TotalMilliseconds * factor);
        var capped = raw <= policy.MaxBackoff ? raw : policy.MaxBackoff;

        var jitter = Math.Clamp(policy.JitterFraction, 0, 1);
        if (jitter <= 0)
        {
            return capped;
        }

        var delta = capped.TotalMilliseconds * jitter;
        var min = capped.TotalMilliseconds - delta;
        var max = capped.TotalMilliseconds + delta;
        var ms = min + (Random.Shared.NextDouble() * (max - min));
        return TimeSpan.FromMilliseconds(Math.Clamp(ms, 0, policy.MaxBackoff.TotalMilliseconds));
    }
}

