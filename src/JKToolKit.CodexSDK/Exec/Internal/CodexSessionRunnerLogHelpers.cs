using System.Text;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.StructuredOutputs;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static class CodexSessionRunnerLogHelpers
{
    internal static async Task<ICodexSessionHandle> CreateHandleFromLogAsync(
        string logPath,
        CancellationToken cancellationToken,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        ICodexProcessLauncher processLauncher,
        TimeSpan processExitTimeout,
        ILoggerFactory loggerFactory,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(tailer);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(processLauncher);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        cancellationToken.ThrowIfCancellationRequested();

        var meta = await ReadSessionMetaAsync(logPath, cancellationToken, tailer, parser, logger).ConfigureAwait(false);

        var sessionInfo = new CodexSessionInfo(
            Id: meta.SessionId,
            LogPath: logPath,
            CreatedAt: meta.Timestamp,
            WorkingDirectory: meta.Cwd,
            Model: null,
            ModelProvider: meta.ModelProvider);

        return new CodexSessionHandle(
            sessionInfo,
            tailer,
            parser,
            process: null,
            processLauncher,
            processExitTimeout,
            idleTimeout: null,
            loggerFactory.CreateLogger<CodexSessionHandle>(),
            tempFilesToDeleteOnDispose: null);
    }

    internal static async Task<string> ResolveResumeLogPathAsync(
        ICodexSessionLocator sessionLocator,
        CodexSessionInfo? selectedSession,
        SessionId? capturedId,
        string sessionsRoot,
        CodexResumeTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionLocator);
        ArgumentNullException.ThrowIfNull(target);

        if (capturedId is { } resolvedId)
        {
            return await sessionLocator.FindSessionLogAsync(resolvedId, sessionsRoot, cancellationToken).ConfigureAwait(false);
        }

        if (selectedSession is not null)
        {
            return await sessionLocator.ValidateLogFileAsync(selectedSession.LogPath, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Failed to resolve a persisted session log for resume target '{target.Description}'. Codex did not emit a resumable session id.");
    }

    internal static (CodexSessionOptions Effective, List<string> TempFiles) MaterializeOutputSchemaIfNeeded(CodexSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.OutputSchema is not { Kind: CodexOutputSchemaKind.Json, Json: { } jsonSchema })
        {
            return (options, []);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"codex-output-schema-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, jsonSchema.GetRawText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var effective = options.Clone();
            effective.OutputSchema = CodexOutputSchema.FromFile(tempPath);
            return (effective, [tempPath]);
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    internal static void DeleteTempFilesBestEffort(IReadOnlyList<string> tempFiles)
    {
        foreach (var path in tempFiles)
        {
            TryDeleteFile(path);
        }
    }

    private static async Task<SessionMetaEvent> ReadSessionMetaAsync(
        string logPath,
        CancellationToken cancellationToken,
        IJsonlTailer tailer,
        IJsonlEventParser parser,
        ILogger logger)
    {
        var lines = tailer.TailAsync(logPath, EventStreamOptions.Default, cancellationToken);
        var events = parser.ParseAsync(lines, cancellationToken);

        await foreach (var evt in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (evt is SessionMetaEvent meta)
            {
                return meta;
            }
        }

        logger.LogWarning("Log stream ended before session_meta was received for {LogPath}", logPath);
        throw new InvalidOperationException("Session stream ended before session_meta was received.");
    }

    private static void TryDeleteFile(string path)
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
            // Best-effort cleanup only.
        }
    }
}
