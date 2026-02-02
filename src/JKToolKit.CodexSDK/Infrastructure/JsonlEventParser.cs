using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Globalization;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Public.Models;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Default implementation of JSONL event parser.
/// </summary>
/// <remarks>
/// Parses newline-delimited JSON (JSONL) events from Codex session logs,
/// mapping known event types to strongly-typed classes and preserving
/// unknown event types for forward compatibility.
/// </remarks>
public sealed class JsonlEventParser : IJsonlEventParser
{
    private readonly ILogger<JsonlEventParser> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonlEventParser"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public JsonlEventParser(ILogger<JsonlEventParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<CodexEvent> ParseAsync(
        IAsyncEnumerable<string> lines,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (lines == null)
            throw new ArgumentNullException(nameof(lines));

        await using var enumerator = lines.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            bool hasNext;
            try
            {
                hasNext = await enumerator.MoveNextAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                hasNext = false;
            }

            if (!hasNext)
            {
                yield break;
            }

            var line = enumerator.Current;

            if (string.IsNullOrWhiteSpace(line))
            {
                _logger.LogTrace("Skipping empty line");
                continue;
            }

            CodexEvent? evt = null;

            try
            {
                evt = ParseLine(line);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed JSON line, skipping: {Line}", line);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing line, skipping: {Line}", line);
                continue;
            }

            if (evt != null)
            {
                yield return evt;
            }
        }
    }

    /// <summary>
    /// Parses a single JSONL line into a CodexEvent.
    /// </summary>
    /// <param name="line">The JSONL line to parse.</param>
    /// <returns>A parsed CodexEvent instance, or null if the line cannot be parsed.</returns>
    private CodexEvent? ParseLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        // Extract common fields
        if (!root.TryGetProperty("timestamp", out var timestampElement))
        {
            _logger.LogWarning("Event missing 'timestamp' field, skipping: {Line}", line);
            return null;
        }

        if (!root.TryGetProperty("type", out var typeElement))
        {
            _logger.LogWarning("Event missing 'type' field, skipping: {Line}", line);
            return null;
        }

        var timestamp = timestampElement.GetDateTimeOffset();
        var type = typeElement.GetString();

        if (string.IsNullOrWhiteSpace(type))
        {
            _logger.LogWarning("Event has empty 'type' field, skipping: {Line}", line);
            return null;
        }

        // Clone the entire JSON for RawPayload
        var rawPayload = root.Clone();

        // Parse based on event type
        return type switch
        {
            "session_meta" => ParseSessionMetaEvent(root, timestamp, type, rawPayload),
            "user_message" => ParseUserMessageEvent(root, timestamp, type, rawPayload),
            "agent_message" => ParseAgentMessageEvent(root, timestamp, type, rawPayload),
            "agent_reasoning" => ParseAgentReasoningEvent(root, timestamp, type, rawPayload),
            "token_count" => ParseTokenCountEvent(root, timestamp, type, rawPayload),
            "turn_context" => ParseTurnContextEvent(root, timestamp, type, rawPayload),
            "response_item" => ParseResponseItemEvent(root, timestamp, type, rawPayload),
            "event_msg" => ParseEventMsgEvent(root, timestamp, rawPayload),
            _ => ParseUnknownEvent(timestamp, type, rawPayload)
        };
    }

    /// <summary>
    /// Parses an event_msg envelope. Codex sometimes wraps known event payloads under this envelope.
    /// </summary>
    private CodexEvent? ParseEventMsgEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("event_msg missing 'payload' object");
            return null;
        }

        var innerType = payload.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(innerType))
        {
            _logger.LogDebug("event_msg missing inner 'payload.type'; returning unknown event");
            return ParseUnknownEvent(timestamp, "event_msg", rawPayload);
        }

        // Treat the envelope as transparent: surface the inner type, while retaining the full raw payload.
        return innerType switch
        {
            "agent_message" => ParseAgentMessageEvent(root, timestamp, innerType, rawPayload),
            "agent_reasoning" => ParseAgentReasoningEvent(root, timestamp, innerType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(root, timestamp, innerType, rawPayload),
            _ => ParseUnknownEvent(timestamp, innerType, rawPayload)
        };
    }

    /// <summary>
    /// Parses a session_meta event.
    /// </summary>
    private SessionMetaEvent? ParseSessionMetaEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            _logger.LogWarning("session_meta event missing 'payload' field");
            return null;
        }

        if (!payload.TryGetProperty("id", out var idElement))
        {
            _logger.LogWarning("session_meta event missing 'payload.id' field");
            return null;
        }

        var idString = idElement.GetString();
        if (string.IsNullOrWhiteSpace(idString))
        {
            _logger.LogWarning("session_meta event has empty 'payload.id' field");
            return null;
        }

        var sessionId = SessionId.Parse(idString);
        var cwd = payload.TryGetProperty("cwd", out var cwdElement)
            ? cwdElement.GetString()
            : null;

        return new SessionMetaEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            SessionId = sessionId,
            Cwd = cwd
        };
    }

    /// <summary>
    /// Parses a user_message event.
    /// </summary>
    private UserMessageEvent? ParseUserMessageEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            _logger.LogWarning("user_message event missing 'payload' field");
            return null;
        }

        if (!payload.TryGetProperty("message", out var messageElement))
        {
            _logger.LogWarning("user_message event missing 'payload.message' field");
            return null;
        }

        var text = messageElement.GetString();
        if (text == null)
        {
            _logger.LogWarning("user_message event has null 'payload.message' field");
            return null;
        }

        return new UserMessageEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = text
        };
    }

    /// <summary>
    /// Parses an agent_message event.
    /// </summary>
    private AgentMessageEvent? ParseAgentMessageEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            _logger.LogWarning("agent_message event missing 'payload' field");
            return null;
        }

        if (!payload.TryGetProperty("message", out var messageElement))
        {
            _logger.LogWarning("agent_message event missing 'payload.message' field");
            return null;
        }

        var text = messageElement.GetString();
        if (text == null)
        {
            _logger.LogWarning("agent_message event has null 'payload.message' field");
            return null;
        }

        return new AgentMessageEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = text
        };
    }

    /// <summary>
    /// Parses an agent_reasoning event.
    /// </summary>
    private AgentReasoningEvent? ParseAgentReasoningEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            _logger.LogWarning("agent_reasoning event missing 'payload' field");
            return null;
        }

        if (!payload.TryGetProperty("text", out var textElement))
        {
            _logger.LogWarning("agent_reasoning event missing 'payload.text' field");
            return null;
        }

        var text = textElement.GetString();
        if (text == null)
        {
            _logger.LogWarning("agent_reasoning event has null 'payload.text' field");
            return null;
        }

        return new AgentReasoningEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = text
        };
    }

    /// <summary>
    /// Parses a token_count event.
    /// </summary>
    private TokenCountEvent ParseTokenCountEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        int? inputTokens = null;
        int? outputTokens = null;
        int? reasoningTokens = null;
        RateLimits? rateLimits = null;

        if (root.TryGetProperty("payload", out var payload))
        {
            if (payload.TryGetProperty("input_tokens", out var inputElement))
            {
                inputTokens = inputElement.GetInt32();
            }

            if (payload.TryGetProperty("output_tokens", out var outputElement))
            {
                outputTokens = outputElement.GetInt32();
            }

            if (payload.TryGetProperty("reasoning_output_tokens", out var reasoningElement))
            {
                reasoningTokens = reasoningElement.GetInt32();
            }

            if (payload.TryGetProperty("rate_limits", out var rateLimitsElement))
            {
                rateLimits = ParseRateLimits(rateLimitsElement);
            }
        }

        return new TokenCountEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            RateLimits = rateLimits
        };
    }

    /// <summary>
    /// Parses a turn_context event.
    /// </summary>
    private TurnContextEvent ParseTurnContextEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        string? approvalPolicy = null;
        string? sandboxPolicyType = null;
        bool? networkAccess = null;

        if (root.TryGetProperty("payload", out var payload))
        {
            if (payload.TryGetProperty("approval_policy", out var approvalElement))
            {
                approvalPolicy = approvalElement.GetString();
            }

            // Codex has produced both `sandbox_policy_type: string` and `sandbox_policy: { type, network_access }`.
            if (payload.TryGetProperty("sandbox_policy_type", out var sandboxElement) && sandboxElement.ValueKind == JsonValueKind.String)
                sandboxPolicyType = sandboxElement.GetString();

            if (payload.TryGetProperty("sandbox_policy", out var sandboxPolicy) && sandboxPolicy.ValueKind == JsonValueKind.Object)
            {
                if (sandboxPolicy.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                    sandboxPolicyType = typeEl.GetString();

                if (sandboxPolicy.TryGetProperty("network_access", out var netEl) &&
                    (netEl.ValueKind == JsonValueKind.True || netEl.ValueKind == JsonValueKind.False))
                {
                    networkAccess = netEl.GetBoolean();
                }
            }
        }

        return new TurnContextEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            ApprovalPolicy = approvalPolicy,
            SandboxPolicyType = sandboxPolicyType,
            NetworkAccess = networkAccess
        };
    }

    private ExitedReviewModeEvent? ParseExitedReviewModeEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("{Type} event missing 'payload' field", type);
            return null;
        }

        if (!payload.TryGetProperty("review_output", out var reviewOutputEl) || reviewOutputEl.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("{Type} event missing 'payload.review_output' object", type);
            return null;
        }

        return new ExitedReviewModeEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            ReviewOutput = ParseReviewOutput(reviewOutputEl)
        };
    }

    private static ReviewOutput ParseReviewOutput(JsonElement el)
    {
        var overallCorrectness = TryGetString(el, "overall_correctness");
        var overallExplanation = TryGetString(el, "overall_explanation");
        var overallConfidenceScore = TryGetDouble(el, "overall_confidence_score");

        var findings = new List<ReviewFinding>();
        if (el.TryGetProperty("findings", out var findingsEl) && findingsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in findingsEl.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.Object)
                    continue;

                findings.Add(new ReviewFinding(
                    Priority: TryGetInt(f, "priority"),
                    ConfidenceScore: TryGetDouble(f, "confidence_score"),
                    Title: TryGetString(f, "title"),
                    Body: TryGetString(f, "body"),
                    CodeLocation: TryParseReviewCodeLocation(f)));
            }
        }

        return new ReviewOutput(overallCorrectness, overallExplanation, overallConfidenceScore, findings);
    }

    private static ReviewCodeLocation? TryParseReviewCodeLocation(JsonElement finding)
    {
        if (!finding.TryGetProperty("code_location", out var loc) || loc.ValueKind != JsonValueKind.Object)
            return null;

        var file = TryGetString(loc, "absolute_file_path");
        ReviewLineRange? range = null;

        if (loc.TryGetProperty("line_range", out var lineRange) && lineRange.ValueKind == JsonValueKind.Object)
        {
            range = new ReviewLineRange(
                Start: TryGetInt(lineRange, "start"),
                End: TryGetInt(lineRange, "end"));
        }

        if (file is null && range is null)
            return null;

        return new ReviewCodeLocation(file, range);
    }

    private static string? TryGetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static double? TryGetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(p.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    private static int? TryGetInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetInt32(out var i) => i,
            JsonValueKind.String when int.TryParse(p.GetString(), out var i) => i,
            _ => null
        };
    }

    private ResponseItemEvent? ParseResponseItemEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            _logger.LogWarning("response_item event missing 'payload' field");
            return null;
        }

        var payloadType = payload.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(payloadType))
        {
            _logger.LogWarning("response_item event missing 'payload.type' field");
            return null;
        }

        var normalized = NormalizeResponseItemPayload(payloadType, payload);

        return new ResponseItemEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            PayloadType = payloadType,
            Payload = normalized
        };
    }

    /// <summary>
    /// Creates an UnknownCodexEvent for unrecognized event types.
    /// </summary>
    private UnknownCodexEvent ParseUnknownEvent(
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        _logger.LogDebug("Encountered unknown event type: {Type}", type);

        return new UnknownCodexEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload
        };
    }

    private RateLimits? ParseRateLimits(JsonElement rateLimitsElement)
    {
        RateLimitScope? ParseScope(string propertyName)
        {
            if (!rateLimitsElement.TryGetProperty(propertyName, out var scope))
            {
                return null;
            }

            double? usedPercent = null;
            int? windowMinutes = null;
            DateTimeOffset? resetsAt = null;

            if (scope.TryGetProperty("used_percent", out var usedPercentEl))
            {
                if (usedPercentEl.ValueKind == JsonValueKind.Number)
                {
                    usedPercent = usedPercentEl.GetDouble();
                }
            }

            if (scope.TryGetProperty("window_minutes", out var windowEl) && windowEl.ValueKind == JsonValueKind.Number)
            {
                windowMinutes = windowEl.GetInt32();
            }

            if (scope.TryGetProperty("resets_at", out var resetsEl))
            {
                long? unixSeconds = resetsEl.ValueKind switch
                {
                    JsonValueKind.Number => resetsEl.GetInt64(),
                    JsonValueKind.String when long.TryParse(resetsEl.GetString(), out var l) => l,
                    _ => null
                };

                if (unixSeconds.HasValue)
                {
                    resetsAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value);
                }
            }
            else if (scope.TryGetProperty("resets_in_seconds", out var resetsInEl) && resetsInEl.ValueKind == JsonValueKind.Number)
            {
                var seconds = resetsInEl.GetDouble();
                resetsAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
            }

            return new RateLimitScope(usedPercent, windowMinutes, resetsAt);
        }

        RateLimitCredits? ParseCredits()
        {
            if (!rateLimitsElement.TryGetProperty("credits", out var credits))
            {
                return null;
            }

            bool? hasCredits = null;
            if (credits.TryGetProperty("has_credits", out var hasCreditsEl) &&
                (hasCreditsEl.ValueKind == JsonValueKind.True || hasCreditsEl.ValueKind == JsonValueKind.False))
            {
                hasCredits = hasCreditsEl.GetBoolean();
            }

            bool? unlimited = null;
            if (credits.TryGetProperty("unlimited", out var unlimitedEl) &&
                (unlimitedEl.ValueKind == JsonValueKind.True || unlimitedEl.ValueKind == JsonValueKind.False))
            {
                unlimited = unlimitedEl.GetBoolean();
            }

            string? balance = credits.TryGetProperty("balance", out var balanceEl) ? balanceEl.GetString() : null;

            return new RateLimitCredits(hasCredits, unlimited, balance);
        }

        var primary = ParseScope("primary");
        var secondary = ParseScope("secondary");
        var credits = ParseCredits();

        if (primary == null && secondary == null && credits == null)
        {
            return null;
        }

        return new RateLimits(primary, secondary, credits);
    }

    private ResponseItemPayload NormalizeResponseItemPayload(string payloadType, JsonElement payload)
    {
        IReadOnlyList<string>? summaries = null;
        string? messageRole = null;
        IReadOnlyList<string>? messageTexts = null;
        FunctionCallInfo? functionCall = null;
        string? ghostCommit = null;

        if (string.Equals(payloadType, "reasoning", StringComparison.OrdinalIgnoreCase))
        {
            if (payload.TryGetProperty("summary", out var summaryArray) && summaryArray.ValueKind == JsonValueKind.Array)
            {
                summaries = summaryArray
                    .EnumerateArray()
                    .Select(s => s.TryGetProperty("text", out var t) ? t.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }
        }
        else if (string.Equals(payloadType, "message", StringComparison.OrdinalIgnoreCase))
        {
            messageRole = payload.TryGetProperty("role", out var roleEl) ? roleEl.GetString() : null;
            if (payload.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
            {
                messageTexts = contentArray
                    .EnumerateArray()
                    .Select(c =>
                        c.ValueKind == JsonValueKind.Object &&
                        c.TryGetProperty("text", out var t)
                            ? t.GetString()
                            : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }
        }
        else if (string.Equals(payloadType, "function_call", StringComparison.OrdinalIgnoreCase))
        {
            var name = payload.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            string? argsJson = null;
            if (payload.TryGetProperty("arguments", out var argsEl))
            {
                argsJson = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString()
                    : argsEl.GetRawText();
            }
            var callId = payload.TryGetProperty("call_id", out var idEl) ? idEl.GetString() : null;
            functionCall = new FunctionCallInfo(name, argsJson, callId);
        }
        else if (string.Equals(payloadType, "ghost_snapshot", StringComparison.OrdinalIgnoreCase))
        {
            if (payload.TryGetProperty("ghost_commit", out var commitEl) &&
                commitEl.ValueKind == JsonValueKind.Object &&
                commitEl.TryGetProperty("id", out var idEl))
            {
                ghostCommit = idEl.GetString();
            }
        }

        return new ResponseItemPayload
        {
            PayloadType = payloadType,
            Raw = payload.Clone(),
            SummaryTexts = summaries,
            MessageRole = messageRole,
            MessageTextParts = messageTexts,
            FunctionCall = functionCall,
            GhostCommitId = ghostCommit
        };
    }
}
