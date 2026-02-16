using System.Diagnostics;
using System.Text;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal sealed class CodexReviewRunner
{
    private const string CodexHomeEnvVar = "CODEX_HOME";
    private readonly CodexClientOptions _clientOptions;
    private readonly ICodexProcessLauncher _processLauncher;
    private readonly ICodexSessionLocator _sessionLocator;
    private readonly ICodexPathProvider _pathProvider;
    private readonly ILogger<CodexClient> _logger;

    internal CodexReviewRunner(
        CodexClientOptions clientOptions,
        ICodexProcessLauncher processLauncher,
        ICodexSessionLocator sessionLocator,
        ICodexPathProvider pathProvider,
        ILogger<CodexClient> logger)
    {
        _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _sessionLocator = sessionLocator ?? throw new ArgumentNullException(nameof(sessionLocator));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal async Task<CodexReviewResult> ReviewAsync(
        CodexReviewOptions options,
        TextWriter? standardOutputWriter,
        TextWriter? standardErrorWriter,
        CancellationToken cancellationToken)
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
        if (CodexClientRegexes.SessionIdRegex().Match(stdoutText) is { Success: true } stdoutMatch &&
            SessionId.TryParse(stdoutMatch.Groups[1].Value, out var stdoutId))
        {
            sessionId = stdoutId;
        }
        else if (CodexClientRegexes.SessionIdRegex().Match(stderrText) is { Success: true } stderrMatch &&
                 SessionId.TryParse(stderrMatch.Groups[1].Value, out var stderrId))
        {
            sessionId = stderrId;
        }

        string? logPath = null;
        if (sessionId is { } sid)
        {
            try
            {
                var sessionsRoot = GetEffectiveSessionsRootDirectory();
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

    private string GetEffectiveSessionsRootDirectory()
    {
        var overrideDirectory = _clientOptions.SessionsRootDirectory;
        if (string.IsNullOrWhiteSpace(overrideDirectory))
        {
            var home =
                _clientOptions.CodexHomeDirectory ??
                Environment.GetEnvironmentVariable(CodexHomeEnvVar);

            if (!string.IsNullOrWhiteSpace(home))
            {
                overrideDirectory = Path.Combine(home, "sessions");
            }
        }

        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            Directory.CreateDirectory(overrideDirectory);
        }

        return _pathProvider.GetSessionsRootDirectory(overrideDirectory);
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
}
