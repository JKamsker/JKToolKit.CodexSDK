using System.Diagnostics;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal sealed partial class CodexSessionRunner
{
    private async Task<SessionMetaEvent> WaitForSessionMetaAsync(Process? process, string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_clientOptions.StartTimeout);

        var metaTask = ReadSessionMetaAsync(logPath, timeoutCts.Token);

        if (process != null)
        {
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(metaTask, exitTask).ConfigureAwait(false);

            if (completed == exitTask)
            {
                timeoutCts.Cancel();
                _ = metaTask.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

                if (exitTask.IsCompletedSuccessfully || process.HasExited)
                {
                    throw new InvalidOperationException($"Codex process exited with code {process.ExitCode} before session_meta was received.");
                }

                await exitTask.ConfigureAwait(false);
                throw new InvalidOperationException("Unreachable.");
            }
        }

        try
        {
            return await metaTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for session_meta event during start.");
        }
    }

    private async Task<SessionMetaEvent> ReadSessionMetaAsync(string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = _tailer.TailAsync(logPath, EventStreamOptions.Default, cancellationToken);
        var events = _parser.ParseAsync(lines, cancellationToken);

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is SessionMetaEvent meta)
            {
                return meta;
            }
        }

        throw new InvalidOperationException("Session stream ended before session_meta was received.");
    }

    private async Task SafeTerminateAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await _processLauncher.TerminateProcessAsync(process, _clientOptions.ProcessExitTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to terminate Codex process after start failure.");
        }
    }

    private void TryKillProcessTreeBestEffort(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error killing Codex process after start failure.");
        }
    }
}
