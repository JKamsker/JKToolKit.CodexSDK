using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.StructuredOutputs.Internal;

internal static class StructuredOutputExecCapture
{
    internal static Task<string> CaptureExecFinalTextAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        CancellationToken ct) =>
        CaptureExecFinalTextAsync(session, streamOptions, onEvent: null, ct);

    internal static async Task<string> CaptureExecFinalTextAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        Action<CodexEvent>? onEvent,
        CancellationToken ct)
    {
        // For live sessions, rely on process exit rather than a specific event name like `task_complete`.
        // Codex CLI output formats can evolve; the most robust completion signal is the process exiting.
        if (session.IsLive && streamOptions.Follow)
        {
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var progressTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var evt in session.GetEventsAsync(streamOptions, progressCts.Token).ConfigureAwait(false))
                    {
                        onEvent?.Invoke(evt);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected during shutdown
                }
            }, CancellationToken.None);

            try
            {
                await session.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                progressCts.Cancel();
                try { await progressTask.ConfigureAwait(false); } catch { /* best-effort */ }
            }

            // After exit, re-scan the log without following to deterministically capture the final message.
            return await ReadFinalExecTextFromStreamAsync(session, streamOptions with { Follow = false }, onEvent: null, ct).ConfigureAwait(false);
        }

        // Non-live sessions (or callers who disabled follow) can just read to EOF.
        var effective = streamOptions with { Follow = false };
        return await ReadFinalExecTextFromStreamAsync(session, effective, onEvent, ct).ConfigureAwait(false);
    }

    private static async Task<string> ReadFinalExecTextFromStreamAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        Action<CodexEvent>? onEvent,
        CancellationToken ct)
    {
        string? raw = null;
        var isDone = false;
        await foreach (var evt in session.GetEventsAsync(streamOptions, ct).ConfigureAwait(false))
        {
            onEvent?.Invoke(evt);
            switch (evt)
            {
                case AgentMessageEvent msg:
                    raw = msg.Text;
                    break;
                case TaskCompleteEvent complete:
                    raw = complete.LastAgentMessage ?? raw;
                    isDone = true;
                    break;
            }

            if (isDone)
            {
                break;
            }
        }

        raw ??= string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new CodexStructuredOutputParseException(
                message: "Codex did not emit a final message to parse as structured output.",
                rawText: raw,
                extractedJson: null,
                innerException: new InvalidOperationException("No final message."));
        }

        return raw;
    }
}
