using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Public.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Public;

/// <summary>
/// Default implementation of the Codex client.
/// </summary>
public sealed class CodexClient : ICodexClient, IAsyncDisposable
{
    private const int SessionIdScanWindowChars = 32 * 1024;
    private const int SessionStartDiagCaptureChars = 8 * 1024;

    private readonly CodexClientOptions _clientOptions;
    private readonly ICodexProcessLauncher _processLauncher;
    private readonly ICodexSessionLocator _sessionLocator;
    private readonly IJsonlTailer _tailer;
    private readonly IJsonlEventParser _parser;
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<CodexClient> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private RateLimits? _cachedRateLimits;
    private DateTimeOffset? _cachedRateLimitsTimestamp;

    /// <summary>
    /// Creates a CodexClient with default infrastructure implementations.
    /// </summary>
    public CodexClient()
        : this
        (
            Options.Create(new CodexClientOptions()),
            null,
            null,
            null,
            null,
            null,
            NullLoggerFactory.Instance.CreateLogger<CodexClient>(),
            NullLoggerFactory.Instance
        )
    {
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        }

        var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);

        var logPath = await _sessionLocator.FindSessionLogAsync(sessionId, sessionsRoot, cancellationToken).ConfigureAwait(false);

        return await CreateHandleFromLogAsync(logPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();

        var validatedPath = await _sessionLocator.ValidateLogFileAsync(logFilePath, cancellationToken).ConfigureAwait(false);

        return await CreateHandleFromLogAsync(validatedPath, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default)
    {
        _clientOptions.Validate();
        var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);

        return _sessionLocator.ListSessionsAsync(sessionsRoot, filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default)
    {
        return await ReviewAsync(options, standardOutputWriter: null, standardErrorWriter: null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a non-interactive code review and optionally mirrors Codex output as it is produced.
    /// </summary>
    /// <param name="options">Review configuration including scope and optional instructions.</param>
    /// <param name="standardOutputWriter">Optional writer that receives stdout as it is emitted.</param>
    /// <param name="standardErrorWriter">Optional writer that receives stderr as it is emitted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CodexReviewResult"/> containing captured stdout/stderr and exit code.</returns>
    public async Task<CodexReviewResult> ReviewAsync(
        CodexReviewOptions options,
        TextWriter? standardOutputWriter,
        TextWriter? standardErrorWriter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();
        options.Validate();

        using var process = await _processLauncher.StartReviewAsync(options, _clientOptions, cancellationToken).ConfigureAwait(false);

        var stdoutCapture = new StringBuilder();
        var stderrCapture = new StringBuilder();

        var pumpStdoutTask = PumpStreamAsync(process.StandardOutput, standardOutputWriter, stdoutCapture, cancellationToken);
        var pumpStderrTask = PumpStreamAsync(process.StandardError, standardErrorWriter, stderrCapture, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(pumpStdoutTask, pumpStderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            try
            {
                await Task.WhenAll(pumpStdoutTask, pumpStderrTask).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort cleanup.
            }

            throw;
        }

        var stdoutText = stdoutCapture.ToString().TrimEnd();
        var stderrText = stderrCapture.ToString().TrimEnd();

        SessionId? sessionId = null;
        if (SessionIdRegex.Match(stdoutText) is { Success: true } stdoutMatch &&
            SessionId.TryParse(stdoutMatch.Groups[1].Value, out var stdoutId))
        {
            sessionId = stdoutId;
        }
        else if (SessionIdRegex.Match(stderrText) is { Success: true } stderrMatch &&
                 SessionId.TryParse(stderrMatch.Groups[1].Value, out var stderrId))
        {
            sessionId = stderrId;
        }

        string? logPath = null;
        if (sessionId is { } sid)
        {
            try
            {
                var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);
                logPath = await _sessionLocator.FindSessionLogAsync(sid, sessionsRoot, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve session log path for review session id {SessionId}", sid);
            }
        }

        return new CodexReviewResult(process.ExitCode, stdoutText, stderrText)
        {
            SessionId = sessionId,
            LogPath = logPath
        };
    }

    private static async Task PumpStreamAsync(
        StreamReader reader,
        TextWriter? mirror,
        StringBuilder capture,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            capture.Append(buffer, 0, read);

            if (mirror is not null)
            {
                await mirror.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                await mirror.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
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
            // Best-effort.
        }
    }

    /// <summary>
    /// Creates a CodexClient with explicit client options and default infrastructure implementations.
    /// </summary>
    public CodexClient
    (
        CodexClientOptions options,
        ICodexProcessLauncher? processLauncher = null,
        ICodexSessionLocator? sessionLocator = null,
        IJsonlTailer? tailer = null,
        IJsonlEventParser? parser = null,
        ICodexPathProvider? pathProvider = null,
        ILogger<CodexClient>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
        : this
        (
            options: Options.Create(options ?? throw new ArgumentNullException(nameof(options))),
            processLauncher,
            sessionLocator,
            tailer,
            parser,
            pathProvider,
            logger,
            loggerFactory
        )
    {
    }

    /// <summary>
    /// Primary constructor for dependency injection.
    /// </summary>
    public CodexClient
    (
        IOptions<CodexClientOptions> options,
        ICodexProcessLauncher? processLauncher = null,
        ICodexSessionLocator? sessionLocator = null,
        IJsonlTailer? tailer = null,
        IJsonlEventParser? parser = null,
        ICodexPathProvider? pathProvider = null,
        ILogger<CodexClient>? logger = null,
        ILoggerFactory? loggerFactory = null
    )
    {
        _clientOptions = (options ?? throw new ArgumentNullException(nameof(options))).Value ?? throw new ArgumentNullException(nameof(options));

        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = logger ?? _loggerFactory.CreateLogger<CodexClient>();

        var fileSystem = new RealFileSystem();

        _pathProvider = pathProvider ?? new DefaultCodexPathProvider(fileSystem, _loggerFactory.CreateLogger<DefaultCodexPathProvider>());
        _processLauncher = processLauncher ?? new CodexProcessLauncher(_pathProvider, _loggerFactory.CreateLogger<CodexProcessLauncher>());
        _sessionLocator = sessionLocator ?? new CodexSessionLocator(fileSystem, _loggerFactory.CreateLogger<CodexSessionLocator>());
        _tailer = tailer ?? new JsonlTailer(fileSystem, _loggerFactory.CreateLogger<JsonlTailer>(), Options.Create(_clientOptions));
        _parser = parser ?? new JsonlEventParser(_loggerFactory.CreateLogger<JsonlEventParser>());
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> StartSessionAsync
    (
        CodexSessionOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        _clientOptions.Validate();
        options.Validate();

        // Resolve paths
        var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);

        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Starting Codex session at {StartTime} using sessions root {SessionsRoot}", startTime, sessionsRoot);

        Process? process = null;
        try
        {
            var sw = Stopwatch.StartNew();
            process = await _processLauncher.StartSessionAsync(options, _clientOptions, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Codex process started with PID {Pid} after {ElapsedMilliseconds} ms", process.Id, sw.ElapsedMilliseconds);
            sw.Restart();

            var captureTimeout = TimeSpan.FromSeconds(Math.Max(10, _clientOptions.StartTimeout.TotalSeconds));
            var (sessionIdCaptureTask, getStartStdoutDiag, getStartStderrDiag) = StartLiveSessionStdIoDrain(process, cancellationToken);

            // Locate the new session log file
            string logPath;
            SessionId? capturedId = null;
            try
            {
                capturedId = await WaitForResultOrTimeoutAsync(sessionIdCaptureTask, captureTimeout, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Captured session id {SessionId} from process output after {ElapsedMilliseconds} ms", capturedId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                var stdoutSnippet = getStartStdoutDiag().TrimEnd();
                var stderrSnippet = getStartStderrDiag().TrimEnd();

                _logger.LogDebug(ex, "Failed to capture session id from process output after {ElapsedMilliseconds} ms.", sw.ElapsedMilliseconds);

                throw new InvalidOperationException(
                    "Failed to capture session id from Codex process output; cannot locate session log. " +
                    "This may indicate the Codex CLI did not start correctly or its stdout/stderr were not readable. " +
                    $"Captured stdout (first {SessionStartDiagCaptureChars} chars): {stdoutSnippet}. " +
                    $"Captured stderr (first {SessionStartDiagCaptureChars} chars): {stderrSnippet}.",
                    ex);
            }

            if (capturedId is { } sid)
            {
                try
                {
                    logPath = await _sessionLocator.WaitForSessionLogByIdAsync
                        (
                            sid,
                            sessionsRoot,
                            _clientOptions.StartTimeout,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Session log by id not found in time; falling back to time-based locator.");
                    logPath = await _sessionLocator.WaitForNewSessionFileAsync
                        (
                            sessionsRoot,
                            startTime,
                            _clientOptions.StartTimeout,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
            }
            else
            {
                // Disable for now - produces race conditions in prod
                // logPath = await _sessionLocator.WaitForNewSessionFileAsync(
                //     sessionsRoot,
                //     startTime,
                //     _clientOptions.StartTimeout,
                //     cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("Failed to capture session id from process output; cannot locate session log.");
            }

            // Extract session_meta
            var sessionMeta = await WaitForSessionMetaAsync(process, logPath, cancellationToken).ConfigureAwait(false);

            var sessionInfo = new CodexSessionInfo
            (
                Id: sessionMeta.SessionId,
                LogPath: logPath,
                CreatedAt: sessionMeta.Timestamp,
                WorkingDirectory: sessionMeta.Cwd,
                Model: null
            );

            return new CodexSessionHandle
            (
                sessionInfo,
                _tailer,
                _parser,
                process,
                _processLauncher,
                _clientOptions.ProcessExitTimeout,
                options.IdleTimeout,
                _loggerFactory.CreateLogger<CodexSessionHandle>()
            );
        }
        catch
        {
            if (process != null)
            {
                await SafeTerminateAsync(process, cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ICodexSessionHandle> ResumeSessionAsync
    (
        SessionId sessionId,
        CodexSessionOptions options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(options);

        _clientOptions.Validate();
        options.Validate();

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        }

        var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);
        var logPath = _pathProvider.ResolveSessionLogPath(sessionId, sessionsRoot);
        await _sessionLocator.ValidateLogFileAsync(logPath, cancellationToken).ConfigureAwait(false);

        Process? process = null;
        try
        {
            process = await _processLauncher
                .ResumeSessionAsync(sessionId, options, _clientOptions, cancellationToken)
                .ConfigureAwait(false);

            var captureTimeout = TimeSpan.FromMilliseconds(Math.Min(250, _clientOptions.StartTimeout.TotalMilliseconds / 4));
            var (sessionIdCaptureTask, _, _) = StartLiveSessionStdIoDrain(process, cancellationToken);
            try
            {
                var captured = await WaitForResultOrTimeoutAsync(sessionIdCaptureTask, captureTimeout, cancellationToken).ConfigureAwait(false);
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

            var sessionInfo = new CodexSessionInfo
            (
                Id: sessionMeta.SessionId,
                LogPath: logPath,
                CreatedAt: sessionMeta.Timestamp,
                WorkingDirectory: sessionMeta.Cwd,
                Model: null
            );

            return new CodexSessionHandle
            (
                sessionInfo,
                _tailer,
                _parser,
                process,
                _processLauncher,
                _clientOptions.ProcessExitTimeout,
                options.IdleTimeout,
                _loggerFactory.CreateLogger<CodexSessionHandle>()
            );
        }
        catch
        {
            if (process != null)
            {
                await SafeTerminateAsync(process, cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task<SessionMetaEvent> WaitForSessionMetaAsync
    (
        Process? process,
        string logPath,
        CancellationToken cancellationToken
    )
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

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to dispose; kept for compatibility with interface
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<ICodexSessionHandle> CreateHandleFromLogAsync(string logPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var meta = await ReadSessionMetaAsync(logPath, cancellationToken).ConfigureAwait(false);

        var sessionInfo = new CodexSessionInfo
        (
            Id: meta.SessionId,
            LogPath: logPath,
            CreatedAt: meta.Timestamp,
            WorkingDirectory: meta.Cwd,
            Model: null
        );

        return new CodexSessionHandle
        (
            sessionInfo,
            _tailer,
            _parser,
            process: null,
            _processLauncher,
            _clientOptions.ProcessExitTimeout,
            idleTimeout: null,
            _loggerFactory.CreateLogger<CodexSessionHandle>()
        );
    }

    /// <summary>
    /// Retrieves the most recent rate limit snapshot emitted by Codex.
    /// </summary>
    /// <param name="noCache">When true, forces reading the latest session logs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RateLimits if found; otherwise null.</returns>
    public async Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default)
    {
        if (!noCache && _cachedRateLimits is not null && _cachedRateLimitsTimestamp.HasValue
            && (DateTimeOffset.UtcNow - _cachedRateLimitsTimestamp.Value) < TimeSpan.FromMinutes(5))
        {
            return _cachedRateLimits;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _clientOptions.Validate();

        var sessionsRoot = _pathProvider.GetSessionsRootDirectory(_clientOptions.SessionsRootDirectory);

        // Collect sessions and scan newest first for a token_count event with rate limits
        var sessions = new List<CodexSessionInfo>();
        await foreach (var session in _sessionLocator.ListSessionsAsync(sessionsRoot, filter: null, cancellationToken))
        {
            sessions.Add(session);
        }

        foreach (var session in sessions.OrderByDescending(s => s.CreatedAt))
        {
            var limits = await ReadLastRateLimitsAsync(session.LogPath, cancellationToken).ConfigureAwait(false);
            if (limits != null)
            {
                _cachedRateLimits = limits;
                _cachedRateLimitsTimestamp = DateTimeOffset.UtcNow;
                return limits;
            }
        }

        return null;
    }

    private async Task<RateLimits?> ReadLastRateLimitsAsync(string logPath, CancellationToken cancellationToken)
    {
        var options = new EventStreamOptions(FromBeginning: true, AfterTimestamp: null, FromByteOffset: null, Follow: false);
        RateLimits? last = null;

        var lines = _tailer.TailAsync(logPath, options, cancellationToken);
        var events = _parser.ParseAsync(lines, cancellationToken);

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            if (evt is TokenCountEvent token && token.RateLimits is not null)
            {
                last = token.RateLimits;
            }
        }

        return last;
    }

    private (Task<SessionId?> SessionIdTask, Func<string> GetStdoutDiag, Func<string> GetStderrDiag) StartLiveSessionStdIoDrain(
        Process process,
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

                            var match = SessionIdRegex.Match(scan.ToString());
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
                    _logger.LogTrace(ex, "Error draining Codex process {Stream} during live session start", streamName);
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

    private static async Task<T?> WaitForResultOrTimeoutAsync<T>(
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

    private static readonly Regex SessionIdRegex = new(@"session id\s*[:=]\s*([0-9a-fA-F\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
