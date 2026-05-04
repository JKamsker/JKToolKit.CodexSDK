using System.Diagnostics;
using System.Text;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.AppServer;

internal interface IRemoteProcessRunner
{
    Task<RemoteProcessResult> RunAsync(CodexLaunch launch, TimeSpan timeout, CancellationToken ct);

    Task<IAsyncDisposableProcess> StartAsync(CodexLaunch launch, CancellationToken ct);
}

internal interface IAsyncDisposableProcess : IAsyncDisposable
{
    Task Completion { get; }
}

internal sealed record RemoteProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class RemoteProcessRunner : IRemoteProcessRunner
{
    private readonly ILogger _logger;

    public RemoteProcessRunner(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RemoteProcessResult> RunAsync(CodexLaunch launch, TimeSpan timeout, CancellationToken ct)
    {
        using var process = StartProcess(launch);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutCts.CancelAfter(timeout);
        }

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Remote command timed out after {timeout}.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new RemoteProcessResult(process.ExitCode, stdout, stderr);
    }

    public Task<IAsyncDisposableProcess> StartAsync(CodexLaunch launch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var process = StartProcess(launch);
        return Task.FromResult<IAsyncDisposableProcess>(new AsyncDisposableProcess(process));
    }

    private Process StartProcess(CodexLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (string.IsNullOrWhiteSpace(launch.FileName))
        {
            throw new ArgumentException("Remote process launches require an explicit file name.", nameof(launch));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = launch.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        startInfo.StandardOutputEncoding = utf8;
        startInfo.StandardErrorEncoding = utf8;
        foreach (var arg in launch.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in launch.Environment)
        {
            startInfo.Environment[key] = value;
        }

        if (!string.IsNullOrWhiteSpace(launch.WorkingDirectory))
        {
            startInfo.WorkingDirectory = launch.WorkingDirectory;
        }

        _logger.LogDebug("Starting remote process: {FileName} {Arguments}", startInfo.FileName, string.Join(" ", launch.Arguments));
        var process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException($"Failed to start process '{startInfo.FileName}'.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }

    private sealed class AsyncDisposableProcess : IAsyncDisposableProcess
    {
        private readonly Process _process;
        private int _disposed;

        public AsyncDisposableProcess(Process process)
        {
            _process = process;
            Completion = process.WaitForExitAsync();
        }

        public Task Completion { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            TryKill(_process);
            try { await Completion.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch { }
            _process.Dispose();
        }
    }
}
