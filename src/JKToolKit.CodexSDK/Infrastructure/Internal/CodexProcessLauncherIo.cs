using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexProcessLauncherIo
{
    internal static async Task WritePromptAndCloseStdinAsync(
        Process process,
        string prompt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteLineAsync(prompt.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();

            logger.LogDebug("Wrote prompt to process {ProcessId} and closed stdin", process.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing prompt to process {ProcessId}", process.Id);
            throw new InvalidOperationException("Failed to write prompt to Codex process stdin.", ex);
        }
    }

    internal static async Task WriteOptionalPromptAndCloseStdinAsync(
        Process process,
        string? prompt,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                await process.StandardInput.WriteLineAsync(prompt.AsMemory(), cancellationToken).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            process.StandardInput.Close();
            logger.LogTrace("Closed stdin for process {ProcessId}", process.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing optional prompt to process {ProcessId}", process.Id);
            throw new InvalidOperationException("Failed to write optional prompt to Codex process stdin.", ex);
        }
    }

    internal static async Task<string?> TryReadStandardErrorAsync(Process process)
    {
        if (!process.StartInfo.RedirectStandardError)
        {
            return null;
        }

        try
        {
            var readTask = process.StandardError.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(200)).ConfigureAwait(false);
            if (completed == readTask)
            {
                return (await readTask.ConfigureAwait(false)).Trim();
            }
        }
        catch
        {
            // Best-effort diagnostic.
        }

        return null;
    }
}

