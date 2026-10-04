using System.Diagnostics;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecDiagnosticsBehaviorTests
{
    [Fact]
    public void FailureDiagnostics_RedactsCredentialsAndEmails_PreservesContext()
    {
        var message = CodexSessionDiagnostics.BuildStartFailureMessage(new() { EnableDiagnosticCapture = true },
            "Start failed.", "Log absent.", () => "Authorization: Bearer synthetic-bearer api_key=synthetic-key developer@example.test",
            () => "sk-aaaaaaaaaaaaaaaaaaaa ghp_bbbbbbbbbbbbbbbbbbbb AKIA1234567890ABCDEF failure-context");
        Assert.StartsWith("Start failed. Log absent.", message);
        Assert.Contains("Authorization: Bearer [REDACTED]", message);
        Assert.Contains("api_key=[REDACTED]", message);
        Assert.Contains("[REDACTED_EMAIL]", message);
        Assert.Contains("sk-[REDACTED]", message);
        Assert.Contains("[REDACTED_TOKEN]", message);
        Assert.Contains("AKIA[REDACTED]", message);
        Assert.Contains("failure-context", message);
        Assert.DoesNotContain("synthetic", message);
        Assert.DoesNotContain("developer@example.test", message);
        Assert.DoesNotContain("aaaaaaaaaaaaaaaaaaaa", message);
        Assert.DoesNotContain("bbbbbbbbbbbbbbbbbbbb", message);
        Assert.DoesNotContain("1234567890ABCDEF", message);
    }

    [Fact]
    public void FailureDiagnostics_DisabledDoesNotReadStreams_AndEmptyCaptureIsSafe()
    {
        var disabled = CodexSessionDiagnostics.BuildStartFailureMessage(new(), "headline", "detail",
            () => throw new InvalidOperationException(), () => throw new InvalidOperationException());
        Assert.Contains("Diagnostic capture is disabled", disabled);
        var enabled = CodexSessionDiagnostics.BuildStartFailureMessage(new() { EnableDiagnosticCapture = true }, "headline", "detail", () => "  ", () => "\n");
        Assert.Contains("redacted): .", enabled);
    }

    [Fact]
    public async Task WaitForResult_HandlesCompletedFaultedDelayedTimeoutAndCallerCancellation()
    {
        Assert.Equal("done", await CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(Task.FromResult<string?>("done"), TimeSpan.FromSeconds(1), default));
        var failure = new IOException("capture failed");
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(Task.FromException<string?>(failure), TimeSpan.FromSeconds(1), default)));
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAsync<TimeoutException>(() => CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(pending.Task, TimeSpan.FromMilliseconds(20), default));
        var wait = CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(pending.Task, TimeSpan.FromSeconds(3), default);
        pending.SetResult("later");
        Assert.Equal("later", await wait);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(new TaskCompletionSource<string?>().Task, TimeSpan.FromSeconds(1), canceled.Token));
    }

    [Fact]
    public async Task Drain_WithNoRedirectedStreams_CompletesWithoutSessionId()
    {
        using var process = new Process();
        var drain = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, NullLogger.Instance);
        Assert.Null(await drain.SessionIdTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Empty(drain.GetStdoutDiag());
        Assert.Empty(drain.GetStderrDiag());
    }
    [Fact]
    public async Task Drain_ClosedOutputStream_IsLoggedAndDoesNotPreventDrainCompletion()
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "-Command" : "-c");
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "[Console]::ReadLine() | Out-Null" : "read ignored");
        using var process = Process.Start(start)!;
        try
        {
            process.StandardOutput.Dispose();
            var logger = new DrainLogger();
            var drain = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, logger);
            process.StandardInput.Dispose();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Null(await drain.SessionIdTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains(logger.Exceptions, exception => exception is ObjectDisposedException);
            Assert.Empty(drain.GetStdoutDiag());
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); }
        }
    }

    private sealed class DrainLogger : Microsoft.Extensions.Logging.ILogger
    {
        public System.Collections.Concurrent.ConcurrentQueue<Exception> Exceptions { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Exceptions.Enqueue(exception);
        }
    }

}
