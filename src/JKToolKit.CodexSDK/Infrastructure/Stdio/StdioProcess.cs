using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal sealed class StdioProcess : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly TimeSpan _shutdownTimeout;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _stderrDrainTask;
    private int _disposed;

    public Process Process { get; }
    public StreamWriter Stdin { get; }
    public StreamReader Stdout { get; }
    public StreamReader Stderr { get; }

    public Task Completion { get; }

    private StdioProcess(Process process, TimeSpan shutdownTimeout, ILogger logger)
    {
        Process = process;
        _shutdownTimeout = shutdownTimeout;
        _logger = logger;

        Stdin = process.StandardInput;
        Stdout = process.StandardOutput;
        Stderr = process.StandardError;

        Completion = process.WaitForExitAsync();

        _stderrDrainTask = Task.Run(() => DrainStderrAsync(_shutdownCts.Token));
    }

    public static async Task<StdioProcess> StartAsync(
        ProcessLaunchOptions launchOptions,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(launchOptions);
        ArgumentNullException.ThrowIfNull(logger);

        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        startupCts.CancelAfter(launchOptions.StartupTimeout);

        var startInfo = StdioProcessStartInfoBuilder.Create(launchOptions);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start process.");
            }
        }
        catch (Exception ex)
        {
            process.Dispose();
            throw new InvalidOperationException(
                $"Failed to start process '{startInfo.FileName}'.",
                ex);
        }

        var stdio = new StdioProcess(process, launchOptions.ShutdownTimeout, logger);

        // Best-effort: ensure the process hasn't immediately exited.
        // (The protocol handshake will provide stronger readiness guarantees.)
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), startupCts.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await stdio.DisposeAsync();
            throw;
        }
        catch (OperationCanceledException)
        {
            await stdio.DisposeAsync();
            throw new TimeoutException($"Process start timed out after {launchOptions.StartupTimeout}.");
        }

        if (process.HasExited)
        {
            var code = process.ExitCode;
            await stdio.DisposeAsync();
            throw new InvalidOperationException($"Process exited prematurely with code {code}.");
        }

        return stdio;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _shutdownCts.Cancel();
        }
        catch
        {
            // ignore
        }

        try
        {
            // Closing stdin is the clean shutdown signal for many stdio servers.
            await Stdin.FlushAsync();
            Stdin.Close();
        }
        catch
        {
            // ignore
        }

        try
        {
            using var cts = new CancellationTokenSource(_shutdownTimeout);
            await Completion.WaitAsync(cts.Token);
        }
        catch
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            await _stderrDrainTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // ignore
        }

        Process.Dispose();
        _shutdownCts.Dispose();
    }

    private async Task DrainStderrAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await Stderr.ReadLineAsync(ct);
                if (line is null) break;
                if (line.Length == 0) continue;

                _logger.LogDebug("codex stderr: {Line}", line);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error draining stderr.");
        }
    }
}
