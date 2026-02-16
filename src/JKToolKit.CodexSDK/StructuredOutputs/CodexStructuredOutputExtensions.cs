using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs.Internal;

namespace JKToolKit.CodexSDK.StructuredOutputs;

/// <summary>
/// Convenience helpers for running Codex with an output schema and parsing the final result into a DTO.
/// </summary>
public static class CodexStructuredOutputExtensions
{
    /// <summary>
    /// Runs <c>codex exec</c> with a generated JSON schema for <typeparamref name="T"/> and deserializes the final output.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredAsync<T>(
        this ICodexClient client,
        CodexSessionOptions options,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        structured ??= new CodexStructuredOutputOptions();
        var serializerOptions = structured.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var schema = CodexJsonSchemaGenerator.Generate<T>(serializerOptions);

        var effective = options.Clone();
        effective.OutputSchema = CodexOutputSchema.FromJson(schema);

        await using var session = await client.StartSessionAsync(effective, ct).ConfigureAwait(false);

        var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(session, EventStreamOptions.Default, ct).ConfigureAwait(false);
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

    /// <summary>
    /// Runs <c>codex exec resume</c> with a generated JSON schema for <typeparamref name="T"/> and deserializes the final output.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredAsync<T>(
        this ICodexClient client,
        SessionId sessionId,
        CodexSessionOptions options,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        }

        structured ??= new CodexStructuredOutputOptions();
        var serializerOptions = structured.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var schema = CodexJsonSchemaGenerator.Generate<T>(serializerOptions);

        var effective = options.Clone();
        effective.OutputSchema = CodexOutputSchema.FromJson(schema);

        // When resuming, the session log already contains historical events (including prior TaskCompleteEvent).
        // Use timestamp filtering to ensure we only consume events from the resumed run.
        var resumeStart = DateTimeOffset.UtcNow;

        await using var session = await client.ResumeSessionAsync(sessionId, effective, ct).ConfigureAwait(false);

        var raw = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(session, EventStreamOptions.FromTimestamp(resumeStart, follow: true), ct).ConfigureAwait(false);
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

    /// <summary>
    /// Runs <c>turn/start</c> with a generated JSON schema for <typeparamref name="T"/> and deserializes the final output.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunTurnStructuredAsync<T>(
        this CodexAppServerClient client,
        string threadId,
        TurnStartOptions options,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        }

        ArgumentNullException.ThrowIfNull(options);

        structured ??= new CodexStructuredOutputOptions();
        var serializerOptions = structured.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var schema = CodexJsonSchemaGenerator.Generate<T>(serializerOptions);

        var effective = options.Clone();
        effective.OutputSchema = schema;

        await using var turn = await client.StartTurnAsync(threadId, effective, ct).ConfigureAwait(false);

        var raw = await StructuredOutputAppServerCapture.CaptureAppServerFinalTextAsync(turn, ct).ConfigureAwait(false);
        var (value, json) = StructuredOutputDeserializer.DeserializeStructured<T>(raw, structured, serializerOptions);

        return new CodexStructuredResult<T>
        {
            Value = value,
            RawJson = json,
            RawText = raw,
            ThreadId = turn.ThreadId,
            TurnId = turn.TurnId
        };
    }

    /// <summary>
    /// Runs <c>codex exec</c> with a generated output schema and automatically retries (via <c>resume</c>) if parsing fails,
    /// while reporting progress callbacks.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredWithRetryAsync<T>(
        this ICodexClient client,
        CodexSessionOptions options,
        CodexStructuredRunProgress progress,
        CodexStructuredRetryOptions? retry = null,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);

        var prepared = StructuredOutputRetryRunner.PrepareExecStructuredRetry<T>(options, retry, structured);

        return await StructuredOutputRetryRunner.RunExecStructuredWithRetryCoreAsync<T>(
            client,
            initialSessionId: null,
            prepared.EffectiveBase,
            prepared.Retry,
            prepared.Structured,
            prepared.SerializerOptions,
            progress,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <c>codex exec</c> with a generated output schema and automatically retries (via <c>resume</c>) if parsing fails.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredWithRetryAsync<T>(
        this ICodexClient client,
        CodexSessionOptions options,
        CodexStructuredRetryOptions? retry = null,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        var prepared = StructuredOutputRetryRunner.PrepareExecStructuredRetry<T>(options, retry, structured);

        return await StructuredOutputRetryRunner.RunExecStructuredWithRetryCoreAsync<T>(
            client,
            initialSessionId: null,
            prepared.EffectiveBase,
            prepared.Retry,
            prepared.Structured,
            prepared.SerializerOptions,
            progress: null,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <c>codex exec resume</c> with a generated output schema and automatically retries (via additional resumes) if parsing fails,
    /// while reporting progress callbacks.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredWithRetryAsync<T>(
        this ICodexClient client,
        SessionId sessionId,
        CodexSessionOptions options,
        CodexStructuredRunProgress progress,
        CodexStructuredRetryOptions? retry = null,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(progress);
        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        }

        var prepared = StructuredOutputRetryRunner.PrepareExecStructuredRetry<T>(options, retry, structured);

        return await StructuredOutputRetryRunner.RunExecStructuredWithRetryCoreAsync<T>(
            client,
            sessionId,
            prepared.EffectiveBase,
            prepared.Retry,
            prepared.Structured,
            prepared.SerializerOptions,
            progress,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <c>codex exec resume</c> with a generated output schema and automatically retries (via additional resumes) if parsing fails.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunStructuredWithRetryAsync<T>(
        this ICodexClient client,
        SessionId sessionId,
        CodexSessionOptions options,
        CodexStructuredRetryOptions? retry = null,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        }

        var prepared = StructuredOutputRetryRunner.PrepareExecStructuredRetry<T>(options, retry, structured);

        return await StructuredOutputRetryRunner.RunExecStructuredWithRetryCoreAsync<T>(
            client,
            sessionId,
            prepared.EffectiveBase,
            prepared.Retry,
            prepared.Structured,
            prepared.SerializerOptions,
            progress: null,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <c>turn/start</c> with a generated output schema and automatically retries (by starting new turns) if parsing fails.
    /// </summary>
    public static async Task<CodexStructuredResult<T>> RunTurnStructuredWithRetryAsync<T>(
        this CodexAppServerClient client,
        string threadId,
        TurnStartOptions options,
        CodexStructuredRetryOptions? retry = null,
        CodexStructuredOutputOptions? structured = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        }
        ArgumentNullException.ThrowIfNull(options);

        retry ??= new CodexStructuredRetryOptions();
        structured ??= new CodexStructuredOutputOptions();

        if (retry.MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retry.MaxAttempts), retry.MaxAttempts, "MaxAttempts must be greater than zero.");
        }

        var serializerOptions = structured.SerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var schema = CodexJsonSchemaGenerator.Generate<T>(serializerOptions);

        CodexStructuredOutputParseException? lastParse = null;
        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (attempt == 1)
                {
                    var effective = options.Clone();
                    effective.OutputSchema = schema;

                    await using var turn = await client.StartTurnAsync(threadId, effective, ct).ConfigureAwait(false);

                    var raw = await StructuredOutputAppServerCapture.CaptureAppServerFinalTextAsync(turn, ct).ConfigureAwait(false);
                    var (value, json) = StructuredOutputDeserializer.DeserializeStructured<T>(raw, structured, serializerOptions);

                    return new CodexStructuredResult<T>
                    {
                        Value = value,
                        RawJson = json,
                        RawText = raw,
                        ThreadId = turn.ThreadId,
                        TurnId = turn.TurnId
                    };
                }

                var retryPrompt = retry.BuildRetryPrompt(new CodexStructuredRetryContext
                {
                    Attempt = attempt - 1,
                    MaxAttempts = retry.MaxAttempts,
                    RawText = lastParse?.RawText ?? string.Empty,
                    ExtractedJson = lastParse?.ExtractedJson,
                    Exception = (Exception?)lastParse ?? new InvalidOperationException("Unknown parse failure."),
                    ThreadId = threadId
                });

                var next = options.Clone();
                next.Input = [TurnInputItem.Text(retryPrompt)];
                next.OutputSchema = schema;

                await using var retryTurn = await client.StartTurnAsync(threadId, next, ct).ConfigureAwait(false);

                var rawRetry = await StructuredOutputAppServerCapture.CaptureAppServerFinalTextAsync(retryTurn, ct).ConfigureAwait(false);
                var (retryValue, retryJson) = StructuredOutputDeserializer.DeserializeStructured<T>(rawRetry, structured, serializerOptions);

                return new CodexStructuredResult<T>
                {
                    Value = retryValue,
                    RawJson = retryJson,
                    RawText = rawRetry,
                    ThreadId = retryTurn.ThreadId,
                    TurnId = retryTurn.TurnId
                };
            }
            catch (CodexStructuredOutputParseException ex)
            {
                lastParse = ex;
                if (attempt >= retry.MaxAttempts)
                {
                    throw;
                }
            }
        }

        throw (Exception?)lastParse ?? new InvalidOperationException("Unknown structured output retry failure.");
    }

}
