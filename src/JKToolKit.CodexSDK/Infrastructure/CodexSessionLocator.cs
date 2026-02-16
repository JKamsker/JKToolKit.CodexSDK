using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Default implementation of Codex session locator.
/// </summary>
/// <remarks>
/// This implementation provides functionality for discovering and locating Codex session files,
/// including polling for new sessions, finding specific session logs, and enumerating
/// sessions with optional filtering.
/// </remarks>
public sealed class CodexSessionLocator : ICodexSessionLocator
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CodexSessionLocator> _logger;

    // Poll interval for waiting for new session files
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    private static readonly Regex SessionFilePattern = CodexSessionFilePattern.Create();

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexSessionLocator"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileSystem"/> or <paramref name="logger"/> is null.
    /// </exception>
    public CodexSessionLocator(
        IFileSystem fileSystem,
        ILogger<CodexSessionLocator> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> WaitForNewSessionFileAsync(
        string sessionsRoot,
        DateTimeOffset startTime,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Timeout must be a positive TimeSpan.",
                nameof(timeout));
        }

        _logger.LogDebug(
            "Waiting for any new session file in {Directory} created after {StartTime} (uncorrelated discovery)",
            sessionsRoot,
            startTime);

        // Snapshot existing files before launch to avoid picking old sessions
        var baseline = CodexSessionLocatorHelpers.CaptureSessionSnapshot(_fileSystem, _logger, sessionsRoot, SessionFilePattern);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startTimeUtc = startTime.UtcDateTime;

        try
        {
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                // Search for .jsonl files created after startTime and not in baseline
                var newSessionFile = CodexSessionLocatorHelpers.FindNewSessionFile(_fileSystem, _logger, sessionsRoot, startTimeUtc, baseline, SessionFilePattern);

                if (newSessionFile != null)
                {
                    _logger.LogInformation(
                        "Found new session file: {Path}",
                        newSessionFile);
                    return newSessionFile;
                }

                // Wait before polling again
                await Task.Delay(DefaultPollInterval, timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            throw new TimeoutException(
                $"No new session file was created within the timeout period of {timeout.TotalSeconds:F1} seconds.");
        }

        // Should not reach here, but just in case
        throw new TimeoutException(
            $"No new session file was created within the timeout period of {timeout.TotalSeconds:F1} seconds.");
    }

    /// <inheritdoc />
    public async Task<string> FindSessionLogAsync(
        SessionId sessionId,
        string sessionsRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException(
                "Session ID cannot be empty or whitespace.",
                nameof(sessionId));
        }

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        _logger.LogDebug(
            "Searching for session log file for session ID: {SessionId} in {Directory}",
            sessionId,
            sessionsRoot);

        // Search for files matching the pattern *-{sessionId}.jsonl
        var searchPattern = $"*-{sessionId.Value}.jsonl";

        try
        {
            var matchingFiles = _fileSystem.GetFiles(sessionsRoot, searchPattern).ToArray();

            if (matchingFiles.Length == 0)
            {
                throw new FileNotFoundException(
                    $"No session log file found for session ID: {sessionId}. Searched in: {sessionsRoot}",
                    searchPattern);
            }

            if (matchingFiles.Length > 1)
            {
                _logger.LogWarning(
                    "Multiple session log files found for session ID {SessionId}. Using the first match: {Path}",
                    sessionId,
                    matchingFiles[0]);
            }

            var logPath = matchingFiles[0];
            _logger.LogDebug("Found session log file: {Path}", logPath);

            var validated = await ValidateLogFileAsync(logPath, cancellationToken).ConfigureAwait(false);
            return validated;
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            _logger.LogError(
                ex,
                "Error searching for session log file for session ID: {SessionId}",
                sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> WaitForSessionLogByIdAsync(
        SessionId sessionId,
        string sessionsRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            try
            {
                var path = await FindSessionLogAsync(sessionId, sessionsRoot, timeoutCts.Token).ConfigureAwait(false);
                return path;
            }
            catch (FileNotFoundException)
            {
                // Not there yet; wait and retry
                try
                {
                    await Task.Delay(DefaultPollInterval, timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        throw new TimeoutException(
            $"No session log file found for session ID: {sessionId} within {timeout.TotalSeconds:F1} seconds.");
    }

    /// <inheritdoc />
    public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(logFilePath);

        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException(
                "Log file path cannot be empty or whitespace.",
                nameof(logFilePath));
        }

        if (!_fileSystem.FileExists(logFilePath))
        {
            throw new FileNotFoundException(
                $"Session log file not found: {logFilePath}",
                logFilePath);
        }

        try
        {
            // Ensure the file can be opened for read access
            using var stream = _fileSystem.OpenRead(logFilePath);
            _ = stream.Length; // touch stream to surface access errors
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
        {
            _logger.LogError(
                ex,
                "Unable to open session log file: {Path}",
                logFilePath);
            throw;
        }

        return Task.FromResult(logFilePath);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
        string sessionsRoot,
        SessionFilter? filter,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        if (!_fileSystem.DirectoryExists(sessionsRoot))
        {
            throw new DirectoryNotFoundException(
                $"Sessions root directory does not exist: {sessionsRoot}");
        }

        _logger.LogDebug(
            "Enumerating sessions in {Directory} with filter: {Filter}",
            sessionsRoot,
            filter?.ToString() ?? "none");

        foreach (var entry in CodexSessionLocatorHelpers.EnumerateSessionFiles(_fileSystem, _logger, sessionsRoot, SessionFilePattern))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = entry.FilePath;
            var createdAtUtc = entry.CreatedAtUtc;

            // Quick pre-filter on creation time if available to avoid opening files that
            // obviously fall outside the requested range.
            if (filter != null && createdAtUtc.HasValue)
            {
                var createdAt = new DateTimeOffset(createdAtUtc.Value, TimeSpan.Zero);

                if (filter.FromDate.HasValue && createdAt < filter.FromDate.Value)
                {
                    _logger.LogTrace("Skipping {FilePath} - created before FromDate", filePath);
                    continue;
                }

                if (filter.ToDate.HasValue && createdAt > filter.ToDate.Value)
                {
                    _logger.LogTrace("Skipping {FilePath} - created after ToDate", filePath);
                    continue;
                }
            }

            // Try to parse session metadata from the file
            CodexSessionInfo? sessionInfo = null;

            try
            {
                sessionInfo = await CodexSessionLocatorHelpers.ParseSessionInfoAsync(_fileSystem, _logger, filePath, createdAtUtc, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error parsing session info from file: {FilePath}, skipping",
                    filePath);
                continue;
            }

            if (sessionInfo == null)
            {
                _logger.LogTrace("No valid session info found in file: {FilePath}, skipping", filePath);
                continue;
            }

            // Apply filter if provided
            if (filter != null && !CodexSessionLocatorHelpers.MatchesFilter(sessionInfo, filter))
            {
                _logger.LogTrace(
                    "Session {SessionId} does not match filter, skipping",
                    sessionInfo.Id);
                continue;
            }

            yield return sessionInfo;
        }
    }
}
