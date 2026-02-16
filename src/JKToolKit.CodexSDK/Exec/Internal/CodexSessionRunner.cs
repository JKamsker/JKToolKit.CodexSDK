using System.Diagnostics;
using System.Text;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal sealed class CodexSessionRunner
{
    private readonly CodexClientOptions _clientOptions;
    private readonly ICodexProcessLauncher _processLauncher;
    private readonly ICodexSessionLocator _sessionLocator;
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<CodexClient> _logger;

    internal CodexSessionRunner(
        CodexClientOptions clientOptions,
        ICodexProcessLauncher processLauncher,
        ICodexSessionLocator sessionLocator,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        ICodexPathProvider pathProvider,
        ILoggerFactory loggerFactory,
        ILogger<CodexClient> logger)
    {
        _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _sessionLocator = sessionLocator ?? throw new ArgumentNullException(nameof(sessionLocator));
        _tailer = tailer ?? throw new ArgumentNullException(nameof(tailer));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        }

        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);
        var logPath = await _sessionLocator.FindSessionLogAsync(sessionId, sessionsRoot, cancellationToken).ConfigureAwait(false);
        return await CreateHandleFromLogAsync(logPath, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();

        var validatedPath = await _sessionLocator.ValidateLogFileAsync(logFilePath, cancellationToken).ConfigureAwait(false);
        return await CreateHandleFromLogAsync(validatedPath, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();
        options.Validate();

        var (effectiveOptions, tempFiles) = MaterializeOutputSchemaIfNeeded(options);
        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);

        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Starting Codex session at {StartTime} using sessions root {SessionsRoot}", startTime, sessionsRoot);

        Process? process = null;
        Task<string>? newSessionFileTask = null;
        try
        {
            if (_clientOptions.EnableUncorrelatedNewSessionFileDiscovery)
            {
                try
                {
                    newSessionFileTask = _sessionLocator.WaitForNewSessionFileAsync(
                        sessionsRoot,
                        startTime,
                        _clientOptions.StartTimeout,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    newSessionFileTask = Task.FromException<string>(ex);
                }

                _ = newSessionFileTask.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }

            var sw = Stopwatch.StartNew();
            process = await _processLauncher.StartSessionAsync(effectiveOptions, _clientOptions, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Codex process started with PID {Pid} after {ElapsedMilliseconds} ms", process.Id, sw.ElapsedMilliseconds);
            sw.Restart();

            var captureTimeout = TimeSpan.FromSeconds(Math.Max(10, _clientOptions.StartTimeout.TotalSeconds));
            var (sessionIdCaptureTask, getStartStdoutDiag, getStartStderrDiag) = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, _logger, cancellationToken);

            string logPath;
            SessionId? capturedId = null;
            Exception? captureException = null;
            try
            {
                capturedId = await CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(sessionIdCaptureTask, captureTimeout, cancellationToken).ConfigureAwait(false);
                if (capturedId is not null)
                {
                    _logger.LogDebug("Captured session id {SessionId} from process output after {ElapsedMilliseconds} ms", capturedId, sw.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                captureException = ex;
                _logger.LogDebug(ex, "Failed to capture session id from process output after {ElapsedMilliseconds} ms; falling back to filesystem session discovery.", sw.ElapsedMilliseconds);
            }

            if (capturedId is { } sid)
            {
                try
                {
                    logPath = await _sessionLocator.WaitForSessionLogByIdAsync(
                            sid,
                            sessionsRoot,
                            _clientOptions.StartTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (_clientOptions.EnableUncorrelatedNewSessionFileDiscovery)
                    {
                        _logger.LogDebug(ex, "Session log by id not found in time; falling back to uncorrelated session file discovery.");
                    }
                    else
                    {
                        _logger.LogDebug(ex, "Session log by id not found in time; uncorrelated session file discovery is disabled.");
                    }

                    if (newSessionFileTask is null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        throw new InvalidOperationException(
                            CodexSessionDiagnostics.BuildStartFailureMessage(
                                _clientOptions,
                                "Failed to locate the session log file by session id.",
                                "Uncorrelated session file discovery is disabled; enable CodexClientOptions.EnableUncorrelatedNewSessionFileDiscovery to allow time-based discovery of any new session log file.",
                                getStartStdoutDiag,
                                getStartStderrDiag),
                            ex);
                    }

                    logPath = await newSessionFileTask.ConfigureAwait(false);
                }
            }
            else
            {
                if (newSessionFileTask is null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    throw new InvalidOperationException(
                        CodexSessionDiagnostics.BuildStartFailureMessage(
                            _clientOptions,
                            "Failed to locate Codex session log file.",
                            "Codex did not emit a recognizable session id, and uncorrelated session file discovery is disabled. Enable CodexClientOptions.EnableUncorrelatedNewSessionFileDiscovery to allow time-based discovery of any new session log file.",
                            getStartStdoutDiag,
                            getStartStderrDiag),
                        captureException);
                }

                try
                {
                    logPath = await newSessionFileTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    throw new InvalidOperationException(
                        CodexSessionDiagnostics.BuildStartFailureMessage(
                            _clientOptions,
                            "Failed to locate Codex session log file.",
                            "Codex did not emit a recognizable session id and no new JSONL session file was discovered.",
                            getStartStdoutDiag,
                            getStartStderrDiag),
                        captureException ?? ex);
                }
            }

            if (string.IsNullOrWhiteSpace(logPath))
            {
                throw new InvalidOperationException("Codex session log path was empty; cannot attach to session.");
            }

            var sessionMeta = await WaitForSessionMetaAsync(process, logPath, cancellationToken).ConfigureAwait(false);

            var sessionInfo = new CodexSessionInfo(
                Id: sessionMeta.SessionId,
                LogPath: logPath,
                CreatedAt: sessionMeta.Timestamp,
                WorkingDirectory: sessionMeta.Cwd,
                Model: null);

            return new CodexSessionHandle(
                sessionInfo,
                _tailer,
                _parser,
                process,
                _processLauncher,
                _clientOptions.ProcessExitTimeout,
                effectiveOptions.IdleTimeout,
                _loggerFactory.CreateLogger<CodexSessionHandle>(),
                tempFilesToDeleteOnDispose: tempFiles.Count == 0 ? null : tempFiles);
        }
        catch
        {
            DeleteTempFilesBestEffort(tempFiles);
            if (process != null)
            {
                await SafeTerminateAsync(process, CancellationToken.None).ConfigureAwait(false);
                TryKillProcessTreeBestEffort(process);
                try { process.Dispose(); } catch { /* ignore */ }
            }

            throw;
        }
    }

    internal async Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);

        _clientOptions.Validate();
        options.Validate();

        var (effectiveOptions, tempFiles) = MaterializeOutputSchemaIfNeeded(options);

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        }

        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);
        var logPath = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);
        await _sessionLocator.ValidateLogFileAsync(logPath, cancellationToken).ConfigureAwait(false);

        Process? process = null;
        try
        {
            process = await _processLauncher
                .ResumeSessionAsync(sessionId, effectiveOptions, _clientOptions, cancellationToken)
                .ConfigureAwait(false);

            var captureTimeout = TimeSpan.FromMilliseconds(Math.Min(250, _clientOptions.StartTimeout.TotalMilliseconds / 4));
            var (sessionIdCaptureTask, _, _) = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, _logger, cancellationToken);
            try
            {
                var captured = await CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(sessionIdCaptureTask, captureTimeout, cancellationToken).ConfigureAwait(false);
                if (captured != null && !captured.Value.Equals(sessionId))
                {
                    _logger.LogDebug("Captured session id {CapturedId} differs from requested {RequestedId}", captured, sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to capture session id during resume; continuing.");
            }

            var sessionMeta = await ReadSessionMetaAsync(logPath, cancellationToken).ConfigureAwait(false);

            var sessionInfo = new CodexSessionInfo(
                Id: sessionMeta.SessionId,
                LogPath: logPath,
                CreatedAt: sessionMeta.Timestamp,
                WorkingDirectory: sessionMeta.Cwd,
                Model: null);

            return new CodexSessionHandle(
                sessionInfo,
                _tailer,
                _parser,
                process,
                _processLauncher,
                _clientOptions.ProcessExitTimeout,
                effectiveOptions.IdleTimeout,
                _loggerFactory.CreateLogger<CodexSessionHandle>(),
                tempFilesToDeleteOnDispose: tempFiles.Count == 0 ? null : tempFiles);
        }
        catch
        {
            DeleteTempFilesBestEffort(tempFiles);
            if (process != null)
            {
                await SafeTerminateAsync(process, CancellationToken.None).ConfigureAwait(false);
                TryKillProcessTreeBestEffort(process);
                try { process.Dispose(); } catch { /* ignore */ }
            }

            throw;
        }
    }

    private async Task<SessionMetaEvent> WaitForSessionMetaAsync(Process? process, string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_clientOptions.StartTimeout);

        var metaTask = ReadSessionMetaAsync(logPath, timeoutCts.Token);

        if (process != null)
        {
            var exitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(metaTask, exitTask).ConfigureAwait(false);

            if (completed == exitTask)
            {
                timeoutCts.Cancel();
                _ = metaTask.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new InvalidOperationException($"Codex process exited with code {process.ExitCode} before session_meta was received.");
            }
        }

        try
        {
            return await metaTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for session_meta event during start.");
        }
    }

    private async Task<SessionMetaEvent> ReadSessionMetaAsync(string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lines = _tailer.TailAsync(logPath, EventStreamOptions.Default, cancellationToken);
        var events = _parser.ParseAsync(lines, cancellationToken);

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is SessionMetaEvent meta)
            {
                return meta;
            }
        }

        throw new InvalidOperationException("Session stream ended before session_meta was received.");
    }

    private async Task SafeTerminateAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await _processLauncher.TerminateProcessAsync(process, _clientOptions.ProcessExitTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to terminate Codex process after start failure.");
        }
    }

    private void TryKillProcessTreeBestEffort(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error killing Codex process after start failure.");
        }
    }

    private async Task<ICodexSessionHandle> CreateHandleFromLogAsync(string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var meta = await ReadSessionMetaAsync(logPath, cancellationToken).ConfigureAwait(false);

        var sessionInfo = new CodexSessionInfo(
            Id: meta.SessionId,
            LogPath: logPath,
            CreatedAt: meta.Timestamp,
            WorkingDirectory: meta.Cwd,
            Model: null);

        return new CodexSessionHandle(
            sessionInfo,
            _tailer,
            _parser,
            process: null,
            _processLauncher,
            _clientOptions.ProcessExitTimeout,
            idleTimeout: null,
            _loggerFactory.CreateLogger<CodexSessionHandle>(),
            tempFilesToDeleteOnDispose: null);
    }

    private static (CodexSessionOptions Effective, List<string> TempFiles) MaterializeOutputSchemaIfNeeded(CodexSessionOptions options)
    {
        if (options.OutputSchema is not { Kind: CodexOutputSchemaKind.Json, Json: { } jsonSchema })
        {
            return (options, new List<string>());
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"codex-output-schema-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, jsonSchema.GetRawText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var effective = options.Clone();
        effective.OutputSchema = CodexOutputSchema.FromFile(tempPath);

        return (effective, new List<string> { tempPath });
    }

    private static void DeleteTempFilesBestEffort(IReadOnlyList<string> tempFiles)
    {
        if (tempFiles.Count == 0)
        {
            return;
        }

        foreach (var path in tempFiles)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best-effort.
            }
        }
    }
}
