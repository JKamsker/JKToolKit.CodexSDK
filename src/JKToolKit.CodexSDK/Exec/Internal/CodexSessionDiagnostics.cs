using System.Diagnostics;
using System.Text;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexSessionDiagnostics
{
    private const int SessionIdScanWindowChars = 32 * 1024;
    private const int SessionStartDiagCaptureChars = 8 * 1024;

    internal static (Task<SessionId?> SessionIdTask, Func<string> GetStdoutDiag, Func<string> GetStderrDiag) StartLiveSessionStdIoDrain(
        Process process,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<SessionId?>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Capture a small prefix of stdout/stderr for diagnostics only.
        // Most useful output is in the JSONL log, but this helps when Codex fails before emitting session_meta.
        var stdoutDiag = new StringBuilder(capacity: SessionStartDiagCaptureChars);
        var stderrDiag = new StringBuilder(capacity: SessionStartDiagCaptureChars);
        var stdoutDiagLock = new object();
        var stderrDiagLock = new object();

        int drainCompleted = 0;

        Task DrainAsync(StreamReader? reader, StringBuilder diag, object diagLock, string streamName) =>
            Task.Run(async () =>
            {
                if (reader is null)
                {
                    if (Interlocked.Increment(ref drainCompleted) == 2)
                        tcs.TrySetResult(null);
                    return;
                }

                var scan = new StringBuilder(capacity: SessionIdScanWindowChars);
                var buffer = new char[4096];

                try
                {
                    while (true)
                    {
                        var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        lock (diagLock)
                        {
                            if (diag.Length < SessionStartDiagCaptureChars)
                            {
                                var remaining = SessionStartDiagCaptureChars - diag.Length;
                                diag.Append(buffer, 0, Math.Min(read, remaining));
                            }
                        }

                        if (!tcs.Task.IsCompleted)
                        {
                            scan.Append(buffer, 0, read);
                            if (scan.Length > SessionIdScanWindowChars)
                            {
                                var tail = scan.ToString()[^SessionIdScanWindowChars..];
                                scan.Clear();
                                scan.Append(tail);
                            }

                            var match = CodexClientRegexes.SessionIdRegex().Match(scan.ToString());
                            if (match.Success && SessionId.TryParse(match.Groups[1].Value, out var sessionId))
                            {
                                tcs.TrySetResult(sessionId);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Caller cancelled; best-effort drain.
                }
                catch (Exception ex)
                {
                    logger.LogTrace(ex, "Error draining Codex process {Stream} during live session start", streamName);
                }
                finally
                {
                    if (Interlocked.Increment(ref drainCompleted) == 2)
                        tcs.TrySetResult(null);
                }
            }, CancellationToken.None);

        _ = DrainAsync(process.StartInfo.RedirectStandardOutput ? process.StandardOutput : null, stdoutDiag, stdoutDiagLock, "stdout");
        _ = DrainAsync(process.StartInfo.RedirectStandardError ? process.StandardError : null, stderrDiag, stderrDiagLock, "stderr");

        string GetStdoutDiag()
        {
            lock (stdoutDiagLock)
            {
                return stdoutDiag.ToString();
            }
        }

        string GetStderrDiag()
        {
            lock (stderrDiagLock)
            {
                return stderrDiag.ToString();
            }
        }

        return (tcs.Task, GetStdoutDiag, GetStderrDiag);
    }

    internal static async Task<T?> WaitForResultOrTimeoutAsync<T>(
        Task<T?> task,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (task.IsCompleted)
            return await task.ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0.###}s.");
        }
    }

    internal static string BuildStartFailureMessage(
        CodexClientOptions clientOptions,
        string headline,
        string detail,
        Func<string> getStdoutDiag,
        Func<string> getStderrDiag)
    {
        if (clientOptions.EnableDiagnosticCapture)
        {
            var stdoutSnippet = SanitizeDiagnostics(getStdoutDiag().TrimEnd());
            var stderrSnippet = SanitizeDiagnostics(getStderrDiag().TrimEnd());

            return $"{headline} {detail} " +
                   $"Captured stdout (first {SessionStartDiagCaptureChars} chars, redacted): {stdoutSnippet}. " +
                   $"Captured stderr (first {SessionStartDiagCaptureChars} chars, redacted): {stderrSnippet}.";
        }

        return $"{headline} {detail} Diagnostic capture is disabled; set CodexClientOptions.EnableDiagnosticCapture=true to include redacted stdout/stderr snippets.";
    }

    private static string SanitizeDiagnostics(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sanitized = input;

        sanitized = CodexClientRegexes.BearerRegex().Replace(sanitized, "$1[REDACTED]");
        sanitized = CodexClientRegexes.KeyValueSecretRegex().Replace(sanitized, m => $"{m.Groups[1].Value}=[REDACTED]");
        sanitized = CodexClientRegexes.OpenAiSkRegex().Replace(sanitized, "sk-[REDACTED]");
        sanitized = CodexClientRegexes.GitHubTokenRegex().Replace(sanitized, "[REDACTED_TOKEN]");
        sanitized = CodexClientRegexes.AwsAccessKeyRegex().Replace(sanitized, "AKIA[REDACTED]");
        sanitized = CodexClientRegexes.EmailRegex().Replace(sanitized, "[REDACTED_EMAIL]");

        return sanitized;
    }
}

