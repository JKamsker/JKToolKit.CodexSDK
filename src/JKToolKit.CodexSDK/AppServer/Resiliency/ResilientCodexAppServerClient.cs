using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// A resilient wrapper around <see cref="CodexAppServerClient"/> that can auto-restart the underlying
/// <c>codex app-server</c> subprocess and (optionally) retry operations based on a user-provided policy.
/// </summary>
public sealed class ResilientCodexAppServerClient : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<ICodexAppServerClientAdapter>> _startInner;
    private readonly CodexAppServerResilienceOptions _options;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _restartLock = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Queue<DateTimeOffset> _restartTimes = new();

    private ICodexAppServerClientAdapter? _inner;
    private long _innerVersion;
    private int _restartCount;
    private volatile CodexAppServerConnectionState _state = CodexAppServerConnectionState.Connected;
    private volatile Exception? _fault;
    private CodexAppServerDisconnectedException? _lastDisconnect;
    private Task? _exitMonitorTask;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public CodexAppServerConnectionState State => _state;

    /// <summary>
    /// Gets the last restart event, when available.
    /// </summary>
    public CodexAppServerRestartEvent? LastRestart { get; private set; }

    /// <summary>
    /// Gets the number of restarts performed during this client's lifetime.
    /// </summary>
    public int RestartCount => Volatile.Read(ref _restartCount);

    internal ResilientCodexAppServerClient(
        Func<CancellationToken, Task<ICodexAppServerClientAdapter>> startInner,
        CodexAppServerResilienceOptions options,
        ILogger logger)
    {
        _startInner = startInner ?? throw new ArgumentNullException(nameof(startInner));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a new resilient client using an underlying <see cref="ICodexAppServerClientFactory"/>.
    /// </summary>
    public static async Task<ResilientCodexAppServerClient> StartAsync(
        ICodexAppServerClientFactory factory,
        CodexAppServerResilienceOptions? options = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var effectiveOptions = options ?? new CodexAppServerResilienceOptions();
        var lf = loggerFactory ?? NullLoggerFactory.Instance;
        var logger = lf.CreateLogger<ResilientCodexAppServerClient>();

        var client = new ResilientCodexAppServerClient(
            startInner: async c =>
            {
                var inner = await factory.StartAsync(c).ConfigureAwait(false);
                return new CodexAppServerClientAdapter(inner);
            },
            options: effectiveOptions,
            logger: logger);

        await client.EnsureConnectedAsync(ct).ConfigureAwait(false);
        return client;
    }

    /// <summary>
    /// Returns a notification stream. When enabled, the stream continues across restarts.
    /// </summary>
    public async IAsyncEnumerable<AppServerNotification> Notifications([EnumeratorCancellation] CancellationToken ct = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        var linkedToken = linkedCts.Token;

        while (true)
        {
            linkedToken.ThrowIfCancellationRequested();

            var inner = await EnsureConnectedAsync(linkedToken).ConfigureAwait(false);
            var version = Volatile.Read(ref _innerVersion);

            Exception? terminal = null;
            var enumerator = inner.Notifications(linkedToken).GetAsyncEnumerator(linkedToken);
            try
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        terminal = ex;
                        break;
                    }

                    if (!moved)
                    {
                        terminal = new CodexAppServerDisconnectedException(
                            message: "Codex app-server notifications stream ended unexpectedly.",
                            processId: null,
                            exitCode: null,
                            stderrTail: Array.Empty<string>());
                        break;
                    }

                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            if (terminal is null)
            {
                yield break;
            }

            if (!IsDisconnect(terminal))
            {
                throw terminal;
            }

            RememberDisconnect(terminal);

            if (!_options.AutoRestart || !_options.NotificationsContinueAcrossRestarts)
            {
                throw terminal;
            }

            await EnsureRestartedAsync(version, terminal, reason: "notifications-disconnected", linkedToken).ConfigureAwait(false);

            if (_options.EmitRestartMarkerNotifications && LastRestart is not null)
            {
                var e = LastRestart;
                yield return new ClientRestartedNotification(
                    restartCount: e.RestartCount,
                    previousExitCode: e.PreviousExitCode,
                    timestamp: e.Timestamp,
                    reason: e.Reason,
                    previousStderrTail: e.PreviousStderrTail);
            }
        }
    }

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        ExecuteWithPolicyAsync(CodexAppServerOperationKind.Call, (c, token) => c.CallAsync(method, @params, token), ct);

    /// <summary>
    /// Starts a new thread.
    /// </summary>
    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default) =>
        ExecuteWithPolicyAsync(CodexAppServerOperationKind.StartThread, (c, token) => c.StartThreadAsync(options, token), ct);

    /// <summary>
    /// Resumes an existing thread by ID.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteWithPolicyAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(threadId, token), ct);

    /// <summary>
    /// Resumes an existing thread using the provided options.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default) =>
        ExecuteWithPolicyAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(options, token), ct);

    /// <summary>
    /// Starts a new turn within the specified thread.
    /// </summary>
    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default) =>
        ExecuteWithPolicyAsync(CodexAppServerOperationKind.StartTurn, (c, token) => c.StartTurnAsync(threadId, options, token), ct);

    /// <summary>
    /// Forces a restart of the underlying app-server subprocess.
    /// </summary>
    public async Task RestartAsync(CancellationToken ct = default)
    {
        var version = Volatile.Read(ref _innerVersion);
        await EnsureRestartedAsync(version, trigger: null, reason: "manual-restart", ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
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

    private async Task<T> ExecuteWithPolicyAsync<T>(
        CodexAppServerOperationKind kind,
        Func<ICodexAppServerClientAdapter, CancellationToken, Task<T>> op,
        CancellationToken ct)
    {
        ThrowIfFaultedOrDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        var linkedToken = linkedCts.Token;

        var attempt = 0;
        while (true)
        {
            linkedToken.ThrowIfCancellationRequested();
            attempt++;

            var inner = await EnsureConnectedAsync(linkedToken).ConfigureAwait(false);
            var version = Volatile.Read(ref _innerVersion);

            try
            {
                return await op(inner, linkedToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsDisconnect(ex))
            {
                RememberDisconnect(ex);

                if (_options.AutoRestart)
                {
                    await EnsureRestartedAsync(version, ex, reason: kind.ToString(), linkedToken).ConfigureAwait(false);
                }

                ThrowIfFaultedOrDisposed();

                var decision = await _options.RetryPolicy(new CodexAppServerRetryContext
                {
                    OperationKind = kind,
                    Attempt = attempt,
                    Exception = ex,
                    CancellationToken = linkedToken,
                    EnsureRestartedAsync = c => EnsureRestartedAsync(version, ex, reason: "policy-ensure-restarted", c)
                }).ConfigureAwait(false);

                if (!decision.ShouldRetry)
                {
                    throw;
                }

                if (decision.Delay is { } delay && delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, linkedToken).ConfigureAwait(false);
                }

                if (decision.BeforeRetryAsync is not null)
                {
                    await decision.BeforeRetryAsync(linkedToken).ConfigureAwait(false);
                }

                continue;
            }
        }
    }

    private async Task<ICodexAppServerClientAdapter> EnsureConnectedAsync(CancellationToken ct)
    {
        ThrowIfFaultedOrDisposed();

        var existing = _inner;
        if (existing is not null)
        {
            return existing;
        }

        await _restartLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfFaultedOrDisposed();

            if (_inner is not null)
            {
                return _inner;
            }

            _state = CodexAppServerConnectionState.Restarting;
            var created = await _startInner(ct).ConfigureAwait(false);
            SetInner(created);
            _state = CodexAppServerConnectionState.Connected;
            return created;
        }
        finally
        {
            _restartLock.Release();
        }
    }

    private async Task EnsureRestartedAsync(long expectedVersion, Exception? trigger, string? reason, CancellationToken ct)
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

    private void ThrowIfFaultedOrDisposed()
    {
        if (_state == CodexAppServerConnectionState.Disposed)
        {
            throw new ObjectDisposedException(nameof(ResilientCodexAppServerClient));
        }

        var fault = _fault;
        if (fault is not null)
        {
            ExceptionDispatchInfo.Capture(fault).Throw();
            throw new InvalidOperationException("Unreachable.");
        }
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
        return TimeSpan.FromMilliseconds(Math.Max(0, ms));
    }

    private static bool IsDisconnect(Exception ex) =>
        ex is CodexAppServerDisconnectedException ||
        ex is JsonRpcConnectionClosedException ||
        (ex is JsonRpcProtocolException p && p.InnerException is JsonRpcConnectionClosedException);

    private void RememberDisconnect(Exception ex)
    {
        if (ex is CodexAppServerDisconnectedException d)
        {
            _lastDisconnect = d;
        }
    }

}
