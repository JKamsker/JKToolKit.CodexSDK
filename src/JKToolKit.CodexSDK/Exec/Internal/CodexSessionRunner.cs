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

internal sealed partial class CodexSessionRunner
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
        return await ResumeSessionAsync(CodexResumeTarget.BySelector(sessionId.Value, includeAllSessions: true), workingDirectory: null, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> ResumeSessionAsync(CodexResumeTarget target, string? workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(target);

        _clientOptions.Validate();

        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);
        var modelProvider = CodexModelProviderConfigResolver.ResolveActiveModelProvider(_clientOptions, sessionsRoot);
        var selectedSession = await CodexResumeTargetResolver.TryResolveAsync(
            _sessionLocator,
            sessionsRoot,
            target,
            workingDirectory,
            modelProvider,
            cancellationToken).ConfigureAwait(false);

        if (selectedSession is null)
        {
            if (target.UseMostRecent)
            {
                throw new FileNotFoundException(
                    "No recorded Codex session matched the requested '--last' resume target.",
                    sessionsRoot);
            }

            var logPath = await _sessionLocator.FindSessionLogAsync(
                SessionId.Parse(target.Selector!),
                sessionsRoot,
                cancellationToken).ConfigureAwait(false);

            return await CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync(
                logPath,
                cancellationToken,
                _tailer,
                _parser,
                _processLauncher,
                _clientOptions.ProcessExitTimeout,
                _loggerFactory,
                _logger).ConfigureAwait(false);
        }

        return await CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync(
            selectedSession.LogPath,
            cancellationToken,
            _tailer,
            _parser,
            _processLauncher,
            _clientOptions.ProcessExitTimeout,
            _loggerFactory,
            _logger).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();

        var validatedPath = await _sessionLocator.ValidateLogFileAsync(logFilePath, cancellationToken).ConfigureAwait(false);
        return await CodexSessionRunnerLogHelpers.CreateHandleFromLogAsync(
            validatedPath,
            cancellationToken,
            _tailer,
            _parser,
            _processLauncher,
            _clientOptions.ProcessExitTimeout,
            _loggerFactory,
            _logger).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();
        options.Validate();

        if (options.RequestsEphemeralSession)
        {
            throw new InvalidOperationException(
                "Exec start with '--ephemeral' is not supported by the SDK session handle APIs because there is no persisted JSONL log to attach to.");
        }

        var (effectiveOptions, tempFiles) = CodexSessionRunnerLogHelpers.MaterializeOutputSchemaIfNeeded(options);
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
            var (sessionIdCaptureTask, getStartStdoutDiag, getStartStderrDiag) = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, _logger);

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
                Model: null,
                ModelProvider: sessionMeta.ModelProvider);

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
            CodexSessionRunnerLogHelpers.DeleteTempFilesBestEffort(tempFiles);
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
        return await ResumeSessionAsync(CodexResumeTarget.BySelector(sessionId.Value), options, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ICodexSessionHandle> ResumeSessionAsync(CodexResumeTarget target, CodexSessionOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        _clientOptions.Validate();
        options.Validate();
        target.Validate();

        if (options.RequestsEphemeralSession)
        {
            throw new InvalidOperationException(
                "Exec resume with '--ephemeral' is not supported by the SDK session handle APIs because there is no persisted JSONL log to attach to.");
        }

        var (effectiveOptions, tempFiles) = CodexSessionRunnerLogHelpers.MaterializeOutputSchemaIfNeeded(options);
        effectiveOptions.ResumeTargetOverride = target;

        var sessionsRoot = CodexSessionsRootResolver.GetEffectiveSessionsRootDirectory(_clientOptions, _pathProvider);
        var modelProvider = CodexModelProviderConfigResolver.ResolveActiveModelProvider(_clientOptions, sessionsRoot);
        var selectedSession = await CodexResumeTargetResolver.TryResolveAsync(
            _sessionLocator,
            sessionsRoot,
            target,
            options.WorkingDirectory,
            modelProvider,
            cancellationToken).ConfigureAwait(false);
        var resumeStartTime = DateTimeOffset.UtcNow;
        var newSessionFileTask = CodexSessionRunnerLogHelpers.StartResumeFallbackDiscoveryIfNeeded(
            _sessionLocator,
            selectedSession,
            sessionsRoot,
            resumeStartTime,
            _clientOptions,
            _logger,
            cancellationToken);
        var selectedLogBaselineLength = selectedSession is null
            ? 0L
            : CodexResumeBootstrapMonitor.TryGetFileLength(selectedSession.LogPath);

        Process? process = null;
        try
        {
            var launcherSessionId = selectedSession?.Id ?? SessionId.Parse(target.Selector ?? "resume-last");
            process = await _processLauncher
                .ResumeSessionAsync(launcherSessionId, effectiveOptions, _clientOptions, cancellationToken)
                .ConfigureAwait(false);

            var requiresCapturedSessionSelection = selectedSession is null;
            var captureTimeout = requiresCapturedSessionSelection
                ? TimeSpan.FromMilliseconds(Math.Min(2_000, _clientOptions.StartTimeout.TotalMilliseconds))
                : TimeSpan.FromMilliseconds(Math.Min(250, _clientOptions.StartTimeout.TotalMilliseconds / 4));
            var (sessionIdCaptureTask, _, _) = CodexSessionDiagnostics.StartLiveSessionStdIoDrain(process, _logger);
            SessionId? capturedId = null;
            try
            {
                capturedId = await CodexSessionDiagnostics.WaitForResultOrTimeoutAsync(sessionIdCaptureTask, captureTimeout, cancellationToken).ConfigureAwait(false);
                if (capturedId != null &&
                    selectedSession != null &&
                    !capturedId.Value.Equals(selectedSession.Id))
                {
                    _logger.LogDebug("Captured session id {CapturedId} differs from locally selected {SelectedId}", capturedId, selectedSession.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to capture session id during resume; continuing.");
            }

            var logPath = await CodexSessionRunnerLogHelpers.ResolveResumeLogPathAsync(
                _sessionLocator,
                selectedSession,
                capturedId,
                sessionsRoot,
                newSessionFileTask,
                _clientOptions.StartTimeout,
                target,
                _logger,
                cancellationToken).ConfigureAwait(false);

            if (selectedSession is not null &&
                string.Equals(logPath, selectedSession.LogPath, StringComparison.OrdinalIgnoreCase))
            {
                await CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(
                    process,
                    logPath,
                    selectedLogBaselineLength,
                    _clientOptions.StartTimeout,
                    _logger,
                    cancellationToken).ConfigureAwait(false);
            }

            var sessionMeta = await ReadSessionMetaAsync(logPath, cancellationToken).ConfigureAwait(false);

            var sessionInfo = new CodexSessionInfo(
                Id: sessionMeta.SessionId,
                LogPath: logPath,
                CreatedAt: sessionMeta.Timestamp,
                WorkingDirectory: sessionMeta.Cwd,
                Model: null,
                ModelProvider: sessionMeta.ModelProvider);

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
            CodexSessionRunnerLogHelpers.DeleteTempFilesBestEffort(tempFiles);
            if (process != null)
            {
                await SafeTerminateAsync(process, CancellationToken.None).ConfigureAwait(false);
                TryKillProcessTreeBestEffort(process);
                try { process.Dispose(); } catch { /* ignore */ }
            }

            throw;
        }
    }

}
