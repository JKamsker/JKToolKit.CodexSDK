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
            await WriteExactTextAndCloseStdinAsync(process, prompt, logger, "prompt", cancellationToken).ConfigureAwait(false);
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
            await WriteExactTextAndCloseStdinAsync(process, prompt, logger, "optional prompt", cancellationToken).ConfigureAwait(false);
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

    internal static async Task WriteOptionalStdinPayloadAndCloseStdinAsync(
        Process process,
        string? payload,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await WriteExactTextAndCloseStdinAsync(process, payload, logger, "stdin payload", cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error writing stdin payload to process {ProcessId}", process.Id);
            throw new InvalidOperationException("Failed to write stdin payload to Codex process stdin.", ex);
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

    private static async Task WriteExactTextAndCloseStdinAsync(
        Process process,
        string? text,
        ILogger logger,
        string contentDescription,
        CancellationToken cancellationToken)
    {
        if (text is not null)
        {
            await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        process.StandardInput.Close();
        logger.LogTrace("Wrote {ContentDescription} to process {ProcessId} and closed stdin", contentDescription, process.Id);
    }
}

