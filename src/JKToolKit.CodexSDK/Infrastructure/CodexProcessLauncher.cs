using System.Diagnostics;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Default implementation of Codex CLI process launcher.
/// </summary>
/// <remarks>
/// This implementation handles the lifecycle of Codex CLI processes, including
/// starting new sessions with appropriate command-line arguments and terminating
/// processes gracefully with forceful fallback.
/// </remarks>
public sealed class CodexProcessLauncher : ICodexProcessLauncher
{
    private const string CodexHomeEnvVar = "CODEX_HOME";
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<CodexProcessLauncher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexProcessLauncher"/> class.
    /// </summary>
    /// <param name="pathProvider">The path provider for locating the Codex executable.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="pathProvider"/> or <paramref name="logger"/> is null.
    /// </exception>
    public CodexProcessLauncher(
        ICodexPathProvider pathProvider,
        ILogger<CodexProcessLauncher> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Process> StartSessionAsync(
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        var startInfo = CreateProcessStartInfo(options, clientOptions);
        var argumentPreview = ProcessStartInfoBuilder.FormatArguments(startInfo);
        _logger.LogDebug(
            "Starting Codex process with executable {Executable} and arguments: {Arguments}",
            startInfo.FileName,
            argumentPreview);

        // Start the process
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Failed to start Codex process. The process may not have started successfully.");
            }

            _logger.LogInformation(
                "Codex process started successfully with PID: {ProcessId}",
                process.Id);

            // Write prompt to stdin and close it
            await CodexProcessLauncherIo.WritePromptAndCloseStdinAsync(process, options.Prompt, _logger, cancellationToken);

            return process;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx)
            {
                _logger.LogTrace(killEx, "Error killing Codex process after cancellation");
            }
            finally
            {
                process.Dispose();
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error starting Codex process");

            var stderr = await CodexProcessLauncherIo.TryReadStandardErrorAsync(process);
            var wrapped = new InvalidOperationException(
                CodexProcessLauncherDiagnostics.CreateDiagnosticMessage(stderr, startInfo.FileName),
                ex);

            // Clean up the process if it was started
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.Dispose();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error during process cleanup after start failure");
            }

            throw wrapped;
        }
    }

    /// <inheritdoc />
    public async Task<int> TerminateProcessAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timeout must be a positive TimeSpan.",
                nameof(timeout));
        }

        // Check if the process has already exited
        if (process.HasExited)
        {
            _logger.LogDebug(
                "Process {ProcessId} has already exited with code {ExitCode}",
                process.Id,
                process.ExitCode);
            return process.ExitCode;
        }

        var processId = process.Id;
        _logger.LogDebug("Terminating process {ProcessId}", processId);

        try
        {
            // Close stdin if not already closed
            if (process.StandardInput.BaseStream.CanWrite)
            {
                try
                {
                    await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
                    process.StandardInput.Close();
                    _logger.LogTrace("Closed stdin for process {ProcessId}", processId);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error closing stdin for process {ProcessId}", processId);
                }
            }

            // Wait for graceful exit
            _logger.LogDebug(
                "Waiting up to {Timeout}ms for process {ProcessId} to exit gracefully",
                timeout.TotalMilliseconds,
                processId);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);

                _logger.LogInformation(
                    "Process {ProcessId} exited gracefully with code {ExitCode}",
                    processId,
                    process.ExitCode);

                return process.ExitCode;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred, need to forcefully kill
                _logger.LogWarning(
                    "Process {ProcessId} did not exit within timeout, killing forcefully",
                    processId);

                try
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogInformation(
                        "Process {ProcessId} was forcefully terminated",
                        processId);

                    // Wait a bit for the kill to take effect
                    using var killTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(killTimeoutCts.Token);

                    return process.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    // Process has already exited
                    _logger.LogDebug(
                        "Process {ProcessId} exited before kill could execute",
                        processId);
                    return process.ExitCode;
                }
                catch (Exception killEx)
                {
                    _logger.LogError(
                        killEx,
                        "Error forcefully killing process {ProcessId}",
                        processId);
                    return -1;
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            // Process has already exited or is in an invalid state
            _logger.LogWarning(
                ex,
                "Process {ProcessId} is in an invalid state during termination",
                processId);

            if (process.HasExited)
            {
                return process.ExitCode;
            }

            throw new InvalidOperationException(
                $"Process {processId} is in an invalid state and cannot be terminated.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task<Process> ResumeSessionAsync(
        SessionId sessionId,
        CodexSessionOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        var startInfo = CreateResumeStartInfo(sessionId, options, clientOptions);
        var argumentPreview = ProcessStartInfoBuilder.FormatArguments(startInfo);
        _logger.LogDebug(
            "Resuming Codex process with executable {Executable} and arguments: {Arguments}",
            startInfo.FileName,
            argumentPreview);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    $"Failed to start Codex process for resume. The process may not have started successfully.");
            }

            _logger.LogInformation(
                "Codex resume process started successfully with PID: {ProcessId}",
                process.Id);

            await CodexProcessLauncherIo.WritePromptAndCloseStdinAsync(process, options.Prompt, _logger, cancellationToken);

            return process;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx)
            {
                _logger.LogTrace(killEx, "Error killing Codex resume process after cancellation");
            }
            finally
            {
                process.Dispose();
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error starting Codex resume process");

            var stderr = await CodexProcessLauncherIo.TryReadStandardErrorAsync(process);
            var wrapped = new InvalidOperationException(
                CodexProcessLauncherDiagnostics.CreateDiagnosticMessage(stderr, startInfo.FileName),
                ex);

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.Dispose();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error during process cleanup after resume failure");
            }

            throw wrapped;
        }
    }

    /// <summary>
    /// Builds a <see cref="ProcessStartInfo"/> for launching Codex without starting it.
    /// Exposed internally for unit testing of argument construction.
    /// </summary>
    internal ProcessStartInfo CreateProcessStartInfo(
        CodexSessionOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();

        var codexPath = _pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        _logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.Create(codexPath, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    internal ProcessStartInfo CreateResumeStartInfo(
        SessionId sessionId,
        CodexSessionOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();

        var codexPath = _pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        _logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.CreateResume(codexPath, sessionId, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    /// <inheritdoc />
    public async Task<Process> StartReviewAsync(
        CodexReviewOptions options,
        CodexClientOptions clientOptions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        var startInfo = CreateReviewStartInfo(options, clientOptions);
        var argumentPreview = ProcessStartInfoBuilder.FormatArguments(startInfo);
        _logger.LogDebug(
            "Starting Codex review process with executable {Executable} and arguments: {Arguments}",
            startInfo.FileName,
            argumentPreview);

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Codex review process. The process may not have started successfully.");
            }

            _logger.LogInformation(
                "Codex review process started successfully with PID: {ProcessId}",
                process.Id);

            await CodexProcessLauncherIo.WriteOptionalPromptAndCloseStdinAsync(process, options.Prompt, _logger, cancellationToken).ConfigureAwait(false);

            return process;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception killEx)
            {
                _logger.LogTrace(killEx, "Error killing Codex review process after cancellation");
            }
            finally
            {
                process.Dispose();
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error starting Codex review process");

            var stderr = await CodexProcessLauncherIo.TryReadStandardErrorAsync(process).ConfigureAwait(false);
            var wrapped = new InvalidOperationException(
                CodexProcessLauncherDiagnostics.CreateDiagnosticMessage(stderr, startInfo.FileName),
                ex);

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                process.Dispose();
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Error during process cleanup after review start failure");
            }

            throw wrapped;
        }
    }

    internal ProcessStartInfo CreateReviewStartInfo(
        CodexReviewOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();

        var codexPath = _pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        _logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.CreateReview(codexPath, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    private static void ApplyCodexHome(ProcessStartInfo startInfo, CodexClientOptions clientOptions)
    {
        if (string.IsNullOrWhiteSpace(clientOptions.CodexHomeDirectory))
        {
            return;
        }

        startInfo.Environment[CodexHomeEnvVar] = clientOptions.CodexHomeDirectory;
    }
}
