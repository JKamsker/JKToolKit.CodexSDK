using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexResumeBootstrapMonitor
{
    internal static long TryGetFileLength(string logPath)
    {
        try
        {
            return File.Exists(logPath) ? new FileInfo(logPath).Length : 0L;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0L;
        }
    }

    internal static async Task WaitForLogAdvanceAsync(
        Process process,
        string logPath,
        long baselineLength,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(logPath);
        ArgumentNullException.ThrowIfNull(logger);

        if (TryGetFileLength(logPath) > baselineLength)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            if (TryGetFileLength(logPath) > baselineLength)
            {
                return;
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Codex resume process exited with code {process.ExitCode} before the rollout log advanced.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        logger.LogDebug("Timed out waiting for resume log {LogPath} to grow beyond {BaselineLength} bytes.", logPath, baselineLength);
        throw new TimeoutException("Timed out waiting for resumed Codex session to append to the rollout log.");
    }
}
