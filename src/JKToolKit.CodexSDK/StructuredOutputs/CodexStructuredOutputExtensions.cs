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

        string? raw = null;
        await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, ct).ConfigureAwait(false))
        {
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
            throw new InvalidOperationException("Codex did not emit a final message to parse as structured output.");
        }

        var json = CodexStructuredJsonExtractor.ExtractJson(raw, structured.TolerantJsonExtraction);

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, serializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize Codex structured output into '{typeof(T).FullName}'. Raw JSON: {json}",
                ex);
        }

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Codex structured output deserialized to null for '{typeof(T).FullName}'. Raw JSON: {json}");
        }

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

        await using var session = await client.ResumeSessionAsync(sessionId, effective, ct).ConfigureAwait(false);

        string? raw = null;
        await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, ct).ConfigureAwait(false))
        {
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
            throw new InvalidOperationException("Codex did not emit a final message to parse as structured output.");
        }

        var json = CodexStructuredJsonExtractor.ExtractJson(raw, structured.TolerantJsonExtraction);

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, serializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize Codex structured output into '{typeof(T).FullName}'. Raw JSON: {json}",
                ex);
        }

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Codex structured output deserialized to null for '{typeof(T).FullName}'. Raw JSON: {json}");
        }

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
            throw new InvalidOperationException("Codex did not emit a final agent message to parse as structured output.");
        }

        var json = CodexStructuredJsonExtractor.ExtractJson(raw, structured.TolerantJsonExtraction);

        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, serializerOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize Codex structured output into '{typeof(T).FullName}'. Raw JSON: {json}",
                ex);
        }

        if (value is null)
        {
            throw new InvalidOperationException(
                $"Codex structured output deserialized to null for '{typeof(T).FullName}'. Raw JSON: {json}");
        }

        return new CodexStructuredResult<T>
        {
            Value = value,
            RawJson = json,
            RawText = raw,
            ThreadId = turn.ThreadId,
            TurnId = turn.TurnId
        };
    }
}
