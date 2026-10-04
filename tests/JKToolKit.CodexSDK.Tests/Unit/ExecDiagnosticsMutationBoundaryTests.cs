using System.Diagnostics;
using JKToolKit.CodexSDK.Exec.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecDiagnosticsMutationBoundaryTests
{
    [Fact]
    public async Task IrregularOutputReads_KeepOnlyTheDiagnosticPrefix_AndContinueFindingSessionIds()
    {
        using var process = await StartReadyProcessAsync(
            "read next; printf '%s' '" + new string('x', 3000) + "'; read next; printf '%s' '" + new string('y', 6000) + "'; read next; echo; echo 'session id: delayed-id'",
            "[Console]::ReadLine() | Out-Null; [Console]::Out.Write(('x'*3000)); [Console]::Out.Flush(); [Console]::ReadLine() | Out-Null; [Console]::Out.Write(('y'*6000)); [Console]::Out.Flush(); [Console]::ReadLine() | Out-Null; [Console]::WriteLine(); [Console]::WriteLine('session id: delayed-id')");
        try
        {
            var drain = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, NullLogger.Instance);
            await SignalAsync(process);
            await WaitUntilAsync(() => drain.GetStdoutDiag().Length >= 3000);
            Assert.Equal(new string('x', 3000), drain.GetStdoutDiag());
            await SignalAsync(process);
            await WaitUntilAsync(() => drain.GetStdoutDiag().Length >= 8192);
            // The first 3000 chars must not cause the following read to overrun the cap.
            Assert.Equal(new string('x', 3000) + new string('y', 5192), drain.GetStdoutDiag());
            await SignalAsync(process);
            Assert.Equal("delayed-id", (await drain.SessionIdTask.WaitAsync(TimeSpan.FromSeconds(5)))!.Value.Value);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(8192, drain.GetStdoutDiag().Length);
        }
        finally { KillIfRunning(process); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OneUnavailableStream_DoesNotFinishCaptureBeforeOtherStreamProducesItsId(bool disposeRedirectedOutput)
    {
        using var process = await StartReadyProcessAsync("read next; echo 'session id: stderr-id' >&2",
            "[Console]::ReadLine() | Out-Null; [Console]::Error.WriteLine('session id: stderr-id')", redirectOutput: disposeRedirectedOutput, readyOnError: true);
        try
        {
            if (disposeRedirectedOutput) process.StandardOutput.Dispose();
            var drain = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, NullLogger.Instance);
            // Wait for the absent/closed stdout drain to finish; stderr remains intentionally open.
            await Task.Delay(100);
            Assert.False(drain.SessionIdTask.IsCompleted);
            await SignalAsync(process);
            Assert.Equal("stderr-id", (await drain.SessionIdTask.WaitAsync(TimeSpan.FromSeconds(5)))!.Value.Value);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { KillIfRunning(process); }
    }

    private static async Task<Process> StartReadyProcessAsync(string unixScript, string windowsScript, bool redirectOutput = true, bool readyOnError = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh", UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = redirectOutput, RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows()) start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add(OperatingSystem.IsWindows() ? "-Command" : "-c");
        start.ArgumentList.Add(OperatingSystem.IsWindows()
            ? (readyOnError ? "[Console]::Error.WriteLine('ready'); " : "[Console]::WriteLine('ready'); ") + windowsScript
            : (readyOnError ? "echo ready >&2; " : "echo ready; ") + unixScript);
        var process = Process.Start(start)!;
        try
        {
            Assert.Equal("ready", await (readyOnError ? process.StandardError : process.StandardOutput).ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)));
            return process;
        }
        catch { KillIfRunning(process); process.Dispose(); throw; }
    }

    private static async Task SignalAsync(Process process)
    {
        await process.StandardInput.WriteLineAsync("continue");
        await process.StandardInput.FlushAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, deadline.Token);
    }

    private static void KillIfRunning(Process process)
    {
        if (!process.HasExited) { process.Kill(true); process.WaitForExit(5000); }
    }
}
