using System.Text;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;

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

        var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.Default, ct).ConfigureAwait(false);
        var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

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

        var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.FromTimestamp(resumeStart, follow: true), ct).ConfigureAwait(false);
        var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

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

        var effective = new TurnStartOptions
        {
            Input = options.Input,
            Cwd = options.Cwd,
            ApprovalPolicy = options.ApprovalPolicy,
            SandboxPolicy = options.SandboxPolicy,
            Model = options.Model,
            Effort = options.Effort,
            Summary = options.Summary,
            Personality = options.Personality,
            CollaborationMode = options.CollaborationMode,
            OutputSchema = schema
        };

        await using var turn = await client.StartTurnAsync(threadId, effective, ct).ConfigureAwait(false);

        var raw = await CaptureAppServerFinalTextAsync(turn, ct).ConfigureAwait(false);
        var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

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

        SessionId? sessionId = null;
        string? logPath = null;
        CodexStructuredOutputParseException? lastParse = null;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (attempt == 1)
                {
                    progress.AttemptStarting?.Invoke(attempt, retry.MaxAttempts, CodexStructuredAttemptKind.Start);

                    await using var session = await client.StartSessionAsync(effectiveBase, ct).ConfigureAwait(false);
                    sessionId = session.Info.Id;
                    logPath = session.Info.LogPath;
                    progress.SessionLocated?.Invoke(session.Info.Id, session.Info.LogPath);

                    var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.Default, progress.EventReceived, ct).ConfigureAwait(false);
                    var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

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

                progress.AttemptStarting?.Invoke(attempt, retry.MaxAttempts, CodexStructuredAttemptKind.Resume);

                var resumeStart = DateTimeOffset.UtcNow;
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

                var resumeOptions = effectiveBase.Clone();
                resumeOptions.Prompt = retryPrompt;

                await using var resumed = await client.ResumeSessionAsync(sessionId.Value, resumeOptions, ct).ConfigureAwait(false);
                logPath = resumed.Info.LogPath;
                progress.SessionLocated?.Invoke(resumed.Info.Id, resumed.Info.LogPath);

                var raw2 = await CaptureExecFinalTextAsync(resumed, EventStreamOptions.FromTimestamp(resumeStart, follow: true), progress.EventReceived, ct).ConfigureAwait(false);
                var (value2, json2) = DeserializeStructured<T>(raw2, structured, serializerOptions);

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
                progress.ParseFailed?.Invoke(attempt, ex);

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

        SessionId? sessionId = null;
        string? logPath = null;
        CodexStructuredOutputParseException? lastParse = null;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (attempt == 1)
                {
                    await using var session = await client.StartSessionAsync(effectiveBase, ct).ConfigureAwait(false);
                    sessionId = session.Info.Id;
                    logPath = session.Info.LogPath;

                    var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.Default, ct).ConfigureAwait(false);
                    var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

                    return new CodexStructuredResult<T>
                    {
                        Value = value,
                        RawJson = json,
                        RawText = raw,
                        SessionId = session.Info.Id.Value,
                        LogPath = session.Info.LogPath
                    };
                }
                else
                {
                    if (sessionId is null)
                    {
                        throw new InvalidOperationException("Retry attempted without a captured session id.");
                    }

                    var resumeStart = DateTimeOffset.UtcNow;
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

                    var resumeOptions = effectiveBase.Clone();
                    resumeOptions.Prompt = retryPrompt;

                    await using var session = await client.ResumeSessionAsync(sessionId.Value, resumeOptions, ct).ConfigureAwait(false);
                    logPath = session.Info.LogPath;

                    var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.FromTimestamp(resumeStart, follow: true), ct).ConfigureAwait(false);
                    var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

                    return new CodexStructuredResult<T>
                    {
                        Value = value,
                        RawJson = json,
                        RawText = raw,
                        SessionId = session.Info.Id.Value,
                        LogPath = session.Info.LogPath
                    };
                }
            }
            catch (CodexStructuredOutputParseException ex)
            {
                lastParse = ex;

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

        string? logPath = null;
        CodexStructuredOutputParseException? lastParse = null;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var resumeStart = DateTimeOffset.UtcNow;
            if (attempt > 1)
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
                effectiveBase.Prompt = retryPrompt;
            }

            try
            {
                progress.AttemptStarting?.Invoke(attempt, retry.MaxAttempts, CodexStructuredAttemptKind.Resume);

                await using var session = await client.ResumeSessionAsync(sessionId, effectiveBase, ct).ConfigureAwait(false);
                logPath = session.Info.LogPath;
                progress.SessionLocated?.Invoke(session.Info.Id, session.Info.LogPath);

                var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.FromTimestamp(resumeStart, follow: true), progress.EventReceived, ct).ConfigureAwait(false);
                var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

                return new CodexStructuredResult<T>
                {
                    Value = value,
                    RawJson = json,
                    RawText = raw,
                    SessionId = session.Info.Id.Value,
                    LogPath = session.Info.LogPath
                };
            }
            catch (CodexStructuredOutputParseException ex)
            {
                lastParse = ex;
                progress.ParseFailed?.Invoke(attempt, ex);

                if (attempt >= retry.MaxAttempts)
                {
                    throw new CodexStructuredOutputRetryFailedException(
                        attempts: attempt,
                        message: $"Failed to parse structured output after {attempt} attempts.",
                        innerException: ex,
                        sessionId: sessionId.Value,
                        logPath);
                }
            }
        }

        throw new CodexStructuredOutputRetryFailedException(
            attempts: retry.MaxAttempts,
            message: $"Failed to parse structured output after {retry.MaxAttempts} attempts.",
            innerException: (Exception?)lastParse ?? new InvalidOperationException("Unknown failure."),
            sessionId: sessionId.Value,
            logPath);
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

        string? logPath = null;
        CodexStructuredOutputParseException? lastParse = null;

        for (var attempt = 1; attempt <= retry.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var resumeStart = DateTimeOffset.UtcNow;
            if (attempt > 1)
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
                effectiveBase.Prompt = retryPrompt;
            }

            try
            {
                await using var session = await client.ResumeSessionAsync(sessionId, effectiveBase, ct).ConfigureAwait(false);
                logPath = session.Info.LogPath;

                var raw = await CaptureExecFinalTextAsync(session, EventStreamOptions.FromTimestamp(resumeStart, follow: true), ct).ConfigureAwait(false);
                var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

                return new CodexStructuredResult<T>
                {
                    Value = value,
                    RawJson = json,
                    RawText = raw,
                    SessionId = session.Info.Id.Value,
                    LogPath = session.Info.LogPath
                };
            }
            catch (CodexStructuredOutputParseException ex)
            {
                lastParse = ex;

                if (attempt >= retry.MaxAttempts)
                {
                    throw new CodexStructuredOutputRetryFailedException(
                        attempts: attempt,
                        message: $"Failed to parse structured output after {attempt} attempts.",
                        innerException: ex,
                        sessionId: sessionId.Value,
                        logPath);
                }
            }
        }

        throw new CodexStructuredOutputRetryFailedException(
            attempts: retry.MaxAttempts,
            message: $"Failed to parse structured output after {retry.MaxAttempts} attempts.",
            innerException: (Exception?)lastParse ?? new InvalidOperationException("Unknown failure."),
            sessionId: sessionId.Value,
            logPath);
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
                    var effective = new TurnStartOptions
                    {
                        Input = options.Input,
                        Cwd = options.Cwd,
                        ApprovalPolicy = options.ApprovalPolicy,
                        SandboxPolicy = options.SandboxPolicy,
                        Model = options.Model,
                        Effort = options.Effort,
                        Summary = options.Summary,
                        Personality = options.Personality,
                        CollaborationMode = options.CollaborationMode,
                        OutputSchema = schema
                    };

                    await using var turn = await client.StartTurnAsync(threadId, effective, ct).ConfigureAwait(false);

                    var raw = await CaptureAppServerFinalTextAsync(turn, ct).ConfigureAwait(false);
                    var (value, json) = DeserializeStructured<T>(raw, structured, serializerOptions);

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

                var next = new TurnStartOptions
                {
                    Input = [TurnInputItem.Text(retryPrompt)],
                    Cwd = options.Cwd,
                    ApprovalPolicy = options.ApprovalPolicy,
                    SandboxPolicy = options.SandboxPolicy,
                    Model = options.Model,
                    Effort = options.Effort,
                    Summary = options.Summary,
                    Personality = options.Personality,
                    CollaborationMode = options.CollaborationMode,
                    OutputSchema = schema
                };

                await using var retryTurn = await client.StartTurnAsync(threadId, next, ct).ConfigureAwait(false);

                var rawRetry = await CaptureAppServerFinalTextAsync(retryTurn, ct).ConfigureAwait(false);
                var (retryValue, retryJson) = DeserializeStructured<T>(rawRetry, structured, serializerOptions);

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

    private static (T Value, string RawJson) DeserializeStructured<T>(string rawText, CodexStructuredOutputOptions structured, JsonSerializerOptions serializerOptions)
    {
        var extractedJson = (string?)null;
        try
        {
            extractedJson = CodexStructuredJsonExtractor.ExtractJson(rawText, structured.TolerantJsonExtraction);
        }
        catch (Exception ex)
        {
            throw new CodexStructuredOutputParseException(
                message: $"Failed to extract JSON for structured output type '{typeof(T).FullName}'.",
                rawText: rawText,
                extractedJson: null,
                innerException: ex);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(extractedJson, serializerOptions);
            if (value is null)
            {
                throw new InvalidOperationException("Deserialized value was null.");
            }

            return (value, extractedJson);
        }
        catch (Exception ex)
        {
            throw new CodexStructuredOutputParseException(
                message: $"Failed to deserialize structured output into '{typeof(T).FullName}'.",
                rawText: rawText,
                extractedJson: extractedJson,
                innerException: ex);
        }
    }

    private static async Task<string> CaptureExecFinalTextAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        CancellationToken ct)
    {
        return await CaptureExecFinalTextAsync(session, streamOptions, onEvent: null, ct).ConfigureAwait(false);
    }

    private static async Task<string> CaptureExecFinalTextAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        Action<CodexEvent>? onEvent,
        CancellationToken ct)
    {
        // For live sessions, rely on process exit rather than a specific event name like `task_complete`.
        // Codex CLI output formats can evolve; the most robust completion signal is the process exiting.
        if (session.IsLive && streamOptions.Follow)
        {
            using var progressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var progressTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var evt in session.GetEventsAsync(streamOptions, progressCts.Token).ConfigureAwait(false))
                    {
                        onEvent?.Invoke(evt);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected during shutdown
                }
            }, CancellationToken.None);

            try
            {
                await session.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                progressCts.Cancel();
                try { await progressTask.ConfigureAwait(false); } catch { /* best-effort */ }
            }

            // After exit, re-scan the log without following to deterministically capture the final message.
            return await ReadFinalExecTextFromStreamAsync(session, streamOptions with { Follow = false }, onEvent: null, ct).ConfigureAwait(false);
        }

        // Non-live sessions (or callers who disabled follow) can just read to EOF.
        var effective = streamOptions with { Follow = false };
        return await ReadFinalExecTextFromStreamAsync(session, effective, onEvent, ct).ConfigureAwait(false);
    }

    private static async Task<string> ReadFinalExecTextFromStreamAsync(
        ICodexSessionHandle session,
        EventStreamOptions streamOptions,
        Action<CodexEvent>? onEvent,
        CancellationToken ct)
    {
        string? raw = null;
        await foreach (var evt in session.GetEventsAsync(streamOptions, ct).ConfigureAwait(false))
        {
            onEvent?.Invoke(evt);
            switch (evt)
            {
                case AgentMessageEvent msg:
                    raw = msg.Text;
                    break;
                case TaskCompleteEvent done:
                    raw = done.LastAgentMessage ?? raw;
                    goto Done;
            }
        }

        Done:
        raw ??= string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new CodexStructuredOutputParseException(
                message: "Codex did not emit a final message to parse as structured output.",
                rawText: raw,
                extractedJson: null,
                innerException: new InvalidOperationException("No final message."));
        }

        return raw;
    }

    private static async Task<string> CaptureAppServerFinalTextAsync(CodexTurnHandle turn, CancellationToken ct)
    {
        var deltas = new StringBuilder();
        string? fullText = null;

        await foreach (var evt in turn.Events(ct).ConfigureAwait(false))
        {
            switch (evt)
            {
                case AgentMessageDeltaNotification d:
                    deltas.Append(d.Delta);
                    break;
                case ItemCompletedNotification ic when string.Equals(ic.ItemType, "agentMessage", StringComparison.Ordinal):
                    if (ic.Item.ValueKind == JsonValueKind.Object &&
                        ic.Item.TryGetProperty("text", out var t) &&
                        t.ValueKind == JsonValueKind.String)
                    {
                        fullText = t.GetString();
                    }
                    break;
                case TurnCompletedNotification:
                    goto Done;
            }
        }

        Done:
        var raw = fullText ?? deltas.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new CodexStructuredOutputParseException(
                message: "Codex did not emit a final agent message to parse as structured output.",
                rawText: raw,
                extractedJson: null,
                innerException: new InvalidOperationException("No final message."));
        }

        return raw;
    }
}
