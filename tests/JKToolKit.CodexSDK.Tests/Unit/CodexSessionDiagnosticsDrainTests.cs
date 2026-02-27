using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSessionDiagnosticsDrainTests
{
    [Fact]
    public async Task StartLiveSessionStdIoDrain_ContinuesDraining_WhenCallerCancelsWait()
    {
        using var process = CreateLargeOutputProcess();
        var (sessionIdTask, _, _) = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, NullLogger.Instance);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var wait = async () =>
            await CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(
                sessionIdTask,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancelled.Token);

        await wait.Should().ThrowAsync<OperationCanceledException>();

        using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(exitCts.Token);
        }
        catch (OperationCanceledException) when (exitCts.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch
                {
                    // Best-effort: this is only to avoid orphaning a hung child process in CI.
                }
            }

            throw;
        }
        process.ExitCode.Should().Be(0); // If draining stopped, the child can hang on full stdout/stderr pipes or fail with broken-pipe; exit code 0 verifies the drain kept consuming output.
    }

    private static Process CreateLargeOutputProcess()
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            // Write several MB to stdout+stderr. If the parent stops draining pipes, this can hang.
            startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -Command \"$chunk='X'*4096; for($i=0; $i -lt 2000; $i++){ [Console]::Out.Write($chunk); [Console]::Error.Write($chunk) }\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-c \"(yes X | head -c 8000000) & (yes Y | head -c 8000000 1>&2) & wait\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start test process.");
    }
}
