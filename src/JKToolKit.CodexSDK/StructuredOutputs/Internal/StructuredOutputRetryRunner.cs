using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.StructuredOutputs.Internal;

internal static class StructuredOutputRetryRunner
{
    internal static (
        CodexSessionOptions EffectiveBase,
        CodexStructuredRetryOptions Retry,
        CodexStructuredOutputOptions Structured,
        JsonSerializerOptions SerializerOptions) PrepareExecStructuredRetry<T>(
        CodexSessionOptions options,
        CodexStructuredRetryOptions? retry,
        CodexStructuredOutputOptions? structured)
    {
        retry ??= new CodexStructuredRetryOptions();
        structured ??= new CodexStructuredOutputOptions();

        if (retry.MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retry.MaxAttempts), retry.MaxAttempts, "MaxAttempts must be greater than zero.");
        }

        var serializerOptions = structured.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var schema = CodexJsonSchemaGenerator.Generate<T>(serializerOptions);

        var effectiveBase = options.Clone();
        effectiveBase.OutputSchema = CodexOutputSchema.FromJson(schema);

        return (effectiveBase, retry, structured, serializerOptions);
    }

    internal static async Task<CodexStructuredResult<T>> RunExecStructuredWithRetryCoreAsync<T>(
        ICodexClient client,
        SessionId? initialSessionId,
        CodexSessionOptions effectiveBase,
        CodexStructuredRetryOptions retry,
        CodexStructuredOutputOptions structured,
        JsonSerializerOptions serializerOptions,
        CodexStructuredRunProgress? progress,
        CancellationToken ct)
    {
        SessionId? sessionId = initialSessionId;
        string? logPath = null;
        CodexStructuredOutputParseException? lastParse = null;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var isStartAttempt = attempt == 1 && sessionId is null;
            var attemptKind = isStartAttempt ? CodexStructuredAttemptKind.Start : CodexStructuredAttemptKind.Resume;
            progress?.AttemptStarting?.Invoke(attempt, retry.MaxAttempts, attemptKind);

            try
            {
                if (isStartAttempt)
                {
                    await using var session = await client.StartSessionAsync(effectiveBase, ct).ConfigureAwait(false);
                    sessionId = session.Info.Id;
                    logPath = session.Info.LogPath;
                    progress?.SessionLocated?.Invoke(session.Info.Id, session.Info.LogPath);

                    var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(session, EventStreamOptions.Default, progress?.EventReceived, ct).ConfigureAwait(false);
                    var (value, json) = StructuredOutputDeserializer.DeserializeStructured<T>(raw, structured, serializerOptions);

                    return new CodexStructuredResult<T>
                    {
                        Value = value,
                        RawJson = json,
                        RawText = raw,
                        SessionId = session.Info.Id.Value,
                        LogPath = session.Info.LogPath
                    };
                }

                if (sessionId is null)
                {
                    throw new InvalidOperationException("Retry attempted without a captured session id.");
                }

                var resumeStart = DateTimeOffset.UtcNow;
                var resumeOptions = effectiveBase;
                if (!(attempt == 1 && initialSessionId is not null))
                {
                    var retryPrompt = retry.BuildRetryPrompt(new CodexStructuredRetryContext
                    {
                        Attempt = attempt - 1,
                        MaxAttempts = retry.MaxAttempts,
                        RawText = lastParse?.RawText ?? string.Empty,
                        ExtractedJson = lastParse?.ExtractedJson,
                        Exception = (Exception?)lastParse ?? new InvalidOperationException("Unknown parse failure."),
                        SessionId = sessionId.Value,
                        LogPath = logPath
                    });

                    resumeOptions = effectiveBase.Clone();
                    resumeOptions.Prompt = retryPrompt;
                }

                await using var resumed = await client.ResumeSessionAsync(sessionId.Value, resumeOptions, ct).ConfigureAwait(false);
                logPath = resumed.Info.LogPath;
                progress?.SessionLocated?.Invoke(resumed.Info.Id, resumed.Info.LogPath);

                var raw2 = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(
                    resumed,
                    EventStreamOptions.FromTimestamp(resumeStart, follow: true),
                    progress?.EventReceived,
                    ct).ConfigureAwait(false);
                var (value2, json2) = StructuredOutputDeserializer.DeserializeStructured<T>(raw2, structured, serializerOptions);

                return new CodexStructuredResult<T>
                {
                    Value = value2,
                    RawJson = json2,
                    RawText = raw2,
                    SessionId = resumed.Info.Id.Value,
                    LogPath = resumed.Info.LogPath
                };
            }
            catch (CodexStructuredOutputParseException ex)
            {
                lastParse = ex;
                progress?.ParseFailed?.Invoke(attempt, ex);

                if (attempt >= retry.MaxAttempts)
                {
                    throw new CodexStructuredOutputRetryFailedException(
                        attempts: attempt,
                        message: $"Failed to parse structured output after {attempt} attempts.",
                        innerException: ex,
                        sessionId: sessionId?.Value,
                        logPath);
                }

                // Next loop iteration will resume with retry prompt.
            }
        }

        throw new CodexStructuredOutputRetryFailedException(
            attempts: retry.MaxAttempts,
            message: $"Failed to parse structured output after {retry.MaxAttempts} attempts.",
            innerException: (Exception?)lastParse ?? new InvalidOperationException("Unknown failure."),
            sessionId: sessionId?.Value,
            logPath);
    }
}
