using System.Diagnostics;
using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal sealed class CodexSessionHandleEventPipeline
{
    private readonly CodexSessionInfo _info;
    private readonly Process? _process;
    private readonly ICodexProcessLauncher? _processLauncher;
    private readonly TimeSpan _processExitTimeout;
    private readonly ILogger _logger;
    private readonly Action<int, SessionExitReason> _notifyExit;
    private readonly Func<bool> _tryStartIdleTermination;

    public CodexSessionHandleEventPipeline(
        CodexSessionInfo info,
        Process? process,
        ICodexProcessLauncher? processLauncher,
        TimeSpan processExitTimeout,
        ILogger logger,
        Action<int, SessionExitReason> notifyExit,
        Func<bool> tryStartIdleTermination)
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
        _process = process;
        _processLauncher = processLauncher;
        _processExitTimeout = processExitTimeout;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notifyExit = notifyExit ?? throw new ArgumentNullException(nameof(notifyExit));
        _tryStartIdleTermination = tryStartIdleTermination ?? throw new ArgumentNullException(nameof(tryStartIdleTermination));
    }

    public async IAsyncEnumerable<CodexEvent> ApplyTimestampFilter(
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

    public async IAsyncEnumerable<CodexEvent> ApplyIdleTimeout(
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
                _logger.LogDebug(ex, "Idle timeout monitor encountered an error for session {SessionId}", _info.Id);
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
                    _info.Id);

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

        if (!_tryStartIdleTermination())
        {
            return;
        }

        try
        {
            var exitCode = await _processLauncher
                .TerminateProcessAsync(_process, _processExitTimeout, CancellationToken.None)
                .ConfigureAwait(false);

            _notifyExit(exitCode, SessionExitReason.Timeout);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to terminate Codex process for session {SessionId} after idle timeout.",
                _info.Id);
        }
    }
}
