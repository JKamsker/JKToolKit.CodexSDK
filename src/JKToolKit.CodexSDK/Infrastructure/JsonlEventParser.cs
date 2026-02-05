using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Globalization;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Models;
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

    /// <summary>
    /// Attempts to parse a single JSONL line into a <see cref="CodexEvent"/> without throwing.
    /// </summary>
    public bool TryParseLine(string line, out CodexEvent? evt, out string? error)
    {
        evt = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "Line is empty/whitespace.";
            return false;
        }

        try
        {
            evt = ParseLineCore(line);
            if (evt == null)
            {
                error = "Line could not be parsed (missing required fields or unsupported shape).";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
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

            if (!TryParseLine(line, out var evt, out var error))
            {
                _logger.LogWarning("Error parsing line, skipping: {Error}. Line: {Line}", error, line);
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
    private CodexEvent? ParseLineCore(string line)
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
            "event" => ParseEventEnvelopeEvent(root, timestamp, rawPayload),
            "compacted" => ParseCompactedEvent(root, timestamp, type, rawPayload),
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

        // Codex can emit event_msg payloads in two shapes:
        // 1) { type, payload: { ... } }  (nested payload)
        // 2) { type, ... }              (payload fields at this level)
        var innerRoot = payload.TryGetProperty("payload", out var innerPayload) && innerPayload.ValueKind == JsonValueKind.Object
            ? innerPayload
            : payload;

        return innerType switch
        {
            "agent_message" => ParseAgentMessageEvent(innerRoot, timestamp, innerType, rawPayload),
            "agent_reasoning" => ParseAgentReasoningEvent(innerRoot, timestamp, innerType, rawPayload),
            "user_message" => ParseUserMessageEvent(innerRoot, timestamp, innerType, rawPayload),
            "token_count" => ParseTokenCountEvent(innerRoot, timestamp, innerType, rawPayload),
            "context_compacted" => new ContextCompactedEvent { Timestamp = timestamp, Type = innerType, RawPayload = rawPayload },
            "turn_aborted" => ParseTurnAbortedEvent(innerRoot, timestamp, innerType, rawPayload),
            "entered_review_mode" => ParseEnteredReviewModeEvent(innerRoot, timestamp, innerType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(innerRoot, timestamp, innerType, rawPayload),
            _ => ParseUnknownEvent(timestamp, innerType, rawPayload)
        };
    }

    private CodexEvent? ParseEventEnvelopeEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("event missing 'payload' object");
            return null;
        }

        if (!payload.TryGetProperty("msg", out var msg) || msg.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("event missing 'payload.msg' object");
            return null;
        }

        var msgType = msg.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(msgType))
        {
            _logger.LogWarning("event missing 'payload.msg.type'");
            return null;
        }

        return msgType switch
        {
            "agent_message" => ParseAgentMessageEvent(msg, timestamp, msgType, rawPayload),
            "agent_reasoning" => ParseAgentReasoningEvent(msg, timestamp, msgType, rawPayload),
            "agent_reasoning_section_break" => new AgentReasoningSectionBreakEvent { Timestamp = timestamp, Type = msgType, RawPayload = rawPayload },
            "background_event" => ParseBackgroundEvent(msg, timestamp, msgType, rawPayload),
            "compaction_checkpoint_warning" => ParseCompactionCheckpointWarningEvent(msg, timestamp, msgType, rawPayload),
            "entered_review_mode" => ParseEnteredReviewModeEvent(msg, timestamp, msgType, rawPayload),
            "error" => ParseErrorEvent(msg, timestamp, msgType, rawPayload),
            "exited_review_mode" => ParseExitedReviewModeEvent(msg, timestamp, msgType, rawPayload),
            "patch_apply_begin" => ParsePatchApplyBeginEvent(msg, timestamp, msgType, rawPayload),
            "patch_apply_end" => ParsePatchApplyEndEvent(msg, timestamp, msgType, rawPayload),
            "plan_update" => ParsePlanUpdateEvent(msg, timestamp, msgType, rawPayload),
            "task_started" => ParseTaskStartedEvent(msg, timestamp, msgType, rawPayload),
            "task_complete" => ParseTaskCompleteEvent(msg, timestamp, msgType, rawPayload),
            "token_count" => ParseTokenCountEvent(msg, timestamp, msgType, rawPayload),
            "turn_aborted" => ParseTurnAbortedEvent(msg, timestamp, msgType, rawPayload),
            "turn_diff" => ParseTurnDiffEvent(msg, timestamp, msgType, rawPayload),
            _ => ParseUnknownEvent(timestamp, msgType, rawPayload)
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
        var cliVersion = payload.TryGetProperty("cli_version", out var cliVersionEl) ? cliVersionEl.GetString() : null;
        var originator = payload.TryGetProperty("originator", out var originatorEl) ? originatorEl.GetString() : null;
        string? source = null;
        string? sourceSubagent = null;
        if (payload.TryGetProperty("source", out var sourceEl))
        {
            if (sourceEl.ValueKind == JsonValueKind.String)
            {
                source = sourceEl.GetString();
            }
            else if (sourceEl.ValueKind == JsonValueKind.Object)
            {
                var subagent = TryGetString(sourceEl, "subagent");
                if (!string.IsNullOrWhiteSpace(subagent))
                {
                    source = "subagent";
                    sourceSubagent = subagent;
                }
            }
        }
        var modelProvider = payload.TryGetProperty("model_provider", out var modelProviderEl) ? modelProviderEl.GetString() : null;

        return new SessionMetaEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            SessionId = sessionId,
            Cwd = cwd,
            CliVersion = cliVersion,
            Originator = originator,
            Source = source,
            SourceSubagent = sourceSubagent,
            ModelProvider = modelProvider
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
        var payload = GetEventBody(root);

        if (!payload.TryGetProperty("message", out var messageElement) &&
            !payload.TryGetProperty("text", out messageElement))
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
        var payload = GetEventBody(root);

        if (!payload.TryGetProperty("message", out var messageElement) &&
            !payload.TryGetProperty("text", out messageElement))
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
        var payload = GetEventBody(root);

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
        var payload = GetEventBody(root);

        int? inputTokens = null;
        int? outputTokens = null;
        int? reasoningTokens = null;
        int? modelContextWindow = null;
        TokenUsage? lastTokenUsage = null;
        TokenUsage? totalTokenUsage = null;
        RateLimits? rateLimits = null;

        // Newer schema: { info: { total_token_usage, last_token_usage, model_context_window }, rate_limits }
        if (payload.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.Object)
        {
            if (info.TryGetProperty("last_token_usage", out var lastEl) && lastEl.ValueKind == JsonValueKind.Object)
            {
                lastTokenUsage = ParseTokenUsage(lastEl);
            }

            if (info.TryGetProperty("total_token_usage", out var totalEl) && totalEl.ValueKind == JsonValueKind.Object)
            {
                totalTokenUsage = ParseTokenUsage(totalEl);
            }

            if (info.TryGetProperty("model_context_window", out var ctxEl) && ctxEl.ValueKind == JsonValueKind.Number)
            {
                modelContextWindow = ctxEl.GetInt32();
            }
        }

        // Older schema: { input_tokens, output_tokens, reasoning_output_tokens, rate_limits }
        if (payload.TryGetProperty("input_tokens", out var inputElement) && inputElement.ValueKind == JsonValueKind.Number)
        {
            inputTokens = inputElement.GetInt32();
        }

        if (payload.TryGetProperty("output_tokens", out var outputElement) && outputElement.ValueKind == JsonValueKind.Number)
        {
            outputTokens = outputElement.GetInt32();
        }

        if (payload.TryGetProperty("reasoning_output_tokens", out var reasoningElement) && reasoningElement.ValueKind == JsonValueKind.Number)
        {
            reasoningTokens = reasoningElement.GetInt32();
        }

        if (payload.TryGetProperty("rate_limits", out var rateLimitsElement))
        {
            rateLimits = ParseRateLimits(rateLimitsElement);
        }

        if (lastTokenUsage != null)
        {
            inputTokens ??= lastTokenUsage.InputTokens;
            outputTokens ??= lastTokenUsage.OutputTokens;
            reasoningTokens ??= lastTokenUsage.ReasoningOutputTokens;
        }

        if (lastTokenUsage == null && (inputTokens.HasValue || outputTokens.HasValue || reasoningTokens.HasValue))
        {
            lastTokenUsage = new TokenUsage(
                InputTokens: inputTokens,
                CachedInputTokens: null,
                OutputTokens: outputTokens,
                ReasoningOutputTokens: reasoningTokens,
                TotalTokens: null);
        }

        return new TokenCountEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ReasoningTokens = reasoningTokens,
            RateLimits = rateLimits,
            LastTokenUsage = lastTokenUsage,
            TotalTokenUsage = totalTokenUsage,
            ModelContextWindow = modelContextWindow
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
        var payload = GetEventBody(root);

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

        var normalized = ParseResponseItemPayload(payloadType, payload);

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
        if (rateLimitsElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        RateLimitScope? ParseScope(string propertyName)
        {
            if (!rateLimitsElement.TryGetProperty(propertyName, out var scope))
            {
                return null;
            }

            if (scope.ValueKind != JsonValueKind.Object)
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

            if (credits.ValueKind != JsonValueKind.Object)
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

            string? balance = null;
            if (credits.TryGetProperty("balance", out var balanceEl) && balanceEl.ValueKind == JsonValueKind.String)
            {
                balance = balanceEl.GetString();
            }

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

    private CompactedEvent? ParseCompactedEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("compacted event missing 'payload' object");
            return null;
        }

        var message = payload.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String
            ? messageEl.GetString() ?? string.Empty
            : string.Empty;

        var replacementHistory = new List<ResponseItemPayload>();
        if (payload.TryGetProperty("replacement_history", out var historyEl) && historyEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in historyEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var itemType = item.TryGetProperty("type", out var itemTypeEl) ? itemTypeEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(itemType))
                {
                    replacementHistory.Add(new UnknownResponseItemPayload { PayloadType = "unknown", Raw = item.Clone() });
                    continue;
                }

                replacementHistory.Add(ParseResponseItemPayload(itemType, item));
            }
        }

        return new CompactedEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Message = message,
            ReplacementHistory = replacementHistory
        };
    }

    private BackgroundEvent? ParseBackgroundEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new BackgroundEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private CompactionCheckpointWarningEvent? ParseCompactionCheckpointWarningEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new CompactionCheckpointWarningEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private ErrorEvent? ParseErrorEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var msg = TryGetString(payload, "message") ?? TryGetString(payload, "text");
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        return new ErrorEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Message = msg };
    }

    private TurnAbortedEvent? ParseTurnAbortedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var reason = TryGetString(payload, "reason");
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        return new TurnAbortedEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Reason = reason };
    }

    private TurnDiffEvent? ParseTurnDiffEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var diff = TryGetString(payload, "unified_diff");
        if (string.IsNullOrWhiteSpace(diff))
            return null;

        return new TurnDiffEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, UnifiedDiff = diff };
    }

    private EnteredReviewModeEvent? ParseEnteredReviewModeEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var prompt = TryGetString(payload, "prompt");
        var hint = TryGetString(payload, "user_facing_hint");

        ReviewTarget? target = null;
        if (payload.TryGetProperty("target", out var targetEl) && targetEl.ValueKind == JsonValueKind.Object)
        {
            var targetType = TryGetString(targetEl, "type") ?? "unknown";
            target = new ReviewTarget(
                Type: targetType,
                Branch: TryGetString(targetEl, "branch"),
                Sha: TryGetString(targetEl, "sha"),
                Title: TryGetString(targetEl, "title"),
                Instructions: TryGetString(targetEl, "instructions"));
        }

        return new EnteredReviewModeEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Prompt = prompt,
            UserFacingHint = hint,
            Target = target
        };
    }

    private PlanUpdateEvent? ParsePlanUpdateEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var name = TryGetString(payload, "name");
        var steps = new List<PlanStep>();

        if (payload.TryGetProperty("plan", out var planEl) && planEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in planEl.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object)
                    continue;

                var step = TryGetString(p, "step") ?? string.Empty;
                var status = TryGetString(p, "status") ?? string.Empty;
                steps.Add(new PlanStep(step, status));
            }
        }

        return new PlanUpdateEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, Name = name, Plan = steps };
    }

    private TaskStartedEvent ParseTaskStartedEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        int? ctx = null;
        if (payload.TryGetProperty("model_context_window", out var ctxEl) && ctxEl.ValueKind == JsonValueKind.Number)
            ctx = ctxEl.GetInt32();

        return new TaskStartedEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, ModelContextWindow = ctx };
    }

    private TaskCompleteEvent ParseTaskCompleteEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var last = TryGetString(payload, "last_agent_message");
        return new TaskCompleteEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload, LastAgentMessage = last };
    }

    private PatchApplyBeginEvent? ParsePatchApplyBeginEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        bool? autoApproved = null;
        if (payload.TryGetProperty("auto_approved", out var autoEl) &&
            (autoEl.ValueKind == JsonValueKind.True || autoEl.ValueKind == JsonValueKind.False))
        {
            autoApproved = autoEl.GetBoolean();
        }

        var changes = new Dictionary<string, PatchApplyFileChange>(StringComparer.OrdinalIgnoreCase);
        if (payload.TryGetProperty("changes", out var changesEl) && changesEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in changesEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var change = ParsePatchApplyFileChange(prop.Value);
                if (change != null)
                {
                    changes[prop.Name] = change;
                }
            }
        }

        return new PatchApplyBeginEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            AutoApproved = autoApproved,
            Changes = changes
        };
    }

    private static PatchApplyFileChange? ParsePatchApplyFileChange(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        PatchApplyAddOperation? add = null;
        if (el.TryGetProperty("add", out var addEl) && addEl.ValueKind == JsonValueKind.Object)
        {
            var content = TryGetString(addEl, "content") ?? string.Empty;
            add = new PatchApplyAddOperation(content);
        }

        PatchApplyUpdateOperation? update = null;
        if (el.TryGetProperty("update", out var updateEl) && updateEl.ValueKind == JsonValueKind.Object)
        {
            update = new PatchApplyUpdateOperation(
                UnifiedDiff: TryGetString(updateEl, "unified_diff"),
                MovePath: TryGetString(updateEl, "move_path"),
                OriginalContent: TryGetString(updateEl, "original_content"),
                NewContent: TryGetString(updateEl, "new_content"));
        }

        PatchApplyDeleteOperation? delete = null;
        if (el.TryGetProperty("delete", out var delEl) && delEl.ValueKind == JsonValueKind.Object)
        {
            delete = new PatchApplyDeleteOperation();
        }

        if (add == null && update == null && delete == null)
            return null;

        return new PatchApplyFileChange { Add = add, Update = update, Delete = delete };
    }

    private PatchApplyEndEvent? ParsePatchApplyEndEvent(JsonElement root, DateTimeOffset timestamp, string type, JsonElement rawPayload)
    {
        var payload = GetEventBody(root);
        var callId = TryGetString(payload, "call_id");
        if (string.IsNullOrWhiteSpace(callId))
            return null;

        bool? success = null;
        if (payload.TryGetProperty("success", out var successEl) &&
            (successEl.ValueKind == JsonValueKind.True || successEl.ValueKind == JsonValueKind.False))
        {
            success = successEl.GetBoolean();
        }

        return new PatchApplyEndEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            CallId = callId,
            Stdout = TryGetString(payload, "stdout"),
            Stderr = TryGetString(payload, "stderr"),
            Success = success
        };
    }

    private static TokenUsage ParseTokenUsage(JsonElement el)
    {
        return new TokenUsage(
            InputTokens: TryGetInt(el, "input_tokens"),
            CachedInputTokens: TryGetInt(el, "cached_input_tokens"),
            OutputTokens: TryGetInt(el, "output_tokens"),
            ReasoningOutputTokens: TryGetInt(el, "reasoning_output_tokens"),
            TotalTokens: TryGetInt(el, "total_tokens"));
    }

    private ResponseItemPayload ParseResponseItemPayload(string payloadType, JsonElement payload)
    {
        if (string.Equals(payloadType, "reasoning", StringComparison.OrdinalIgnoreCase))
        {
            var summaries = Array.Empty<string>();
            if (payload.TryGetProperty("summary", out var summaryArray) && summaryArray.ValueKind == JsonValueKind.Array)
            {
                summaries = summaryArray
                    .EnumerateArray()
                    .Select(s => s.ValueKind == JsonValueKind.Object ? TryGetString(s, "text") : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToArray();
            }

            var encrypted = TryGetString(payload, "encrypted_content");

            return new ReasoningResponseItemPayload
            {
                PayloadType = payloadType,
                SummaryTexts = summaries,
                EncryptedContent = encrypted
            };
        }

        if (string.Equals(payloadType, "message", StringComparison.OrdinalIgnoreCase))
        {
            var role = TryGetString(payload, "role");
            var parts = ParseMessageContent(payload);
            return new MessageResponseItemPayload
            {
                PayloadType = payloadType,
                Role = role,
                Content = parts
            };
        }

        if (string.Equals(payloadType, "function_call", StringComparison.OrdinalIgnoreCase))
        {
            var name = TryGetString(payload, "name");
            string? argsJson = null;
            if (payload.TryGetProperty("arguments", out var argsEl))
            {
                argsJson = argsEl.ValueKind == JsonValueKind.String
                    ? argsEl.GetString()
                    : argsEl.GetRawText();
            }
            var callId = TryGetString(payload, "call_id");

            return new FunctionCallResponseItemPayload
            {
                PayloadType = payloadType,
                Name = name,
                ArgumentsJson = argsJson,
                CallId = callId
            };
        }

        if (string.Equals(payloadType, "function_call_output", StringComparison.OrdinalIgnoreCase))
        {
            var callId = TryGetString(payload, "call_id");
            string? output = null;
            if (payload.TryGetProperty("output", out var outputEl))
            {
                output = outputEl.ValueKind == JsonValueKind.String ? outputEl.GetString() : outputEl.GetRawText();
            }

            return new FunctionCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = callId,
                Output = output
            };
        }

        if (string.Equals(payloadType, "custom_tool_call", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomToolCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                CallId = TryGetString(payload, "call_id"),
                Name = TryGetString(payload, "name"),
                Input = TryGetString(payload, "input")
            };
        }

        if (string.Equals(payloadType, "custom_tool_call_output", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomToolCallOutputResponseItemPayload
            {
                PayloadType = payloadType,
                CallId = TryGetString(payload, "call_id"),
                Output = TryGetString(payload, "output")
            };
        }

        if (string.Equals(payloadType, "web_search_call", StringComparison.OrdinalIgnoreCase))
        {
            WebSearchAction? action = null;
            if (payload.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.Object)
            {
                IReadOnlyList<string>? queries = null;
                if (actionEl.TryGetProperty("queries", out var queriesEl) && queriesEl.ValueKind == JsonValueKind.Array)
                {
                    queries = queriesEl.EnumerateArray()
                        .Select(q => q.ValueKind == JsonValueKind.String ? q.GetString() : null)
                        .Where(q => !string.IsNullOrWhiteSpace(q))
                        .Cast<string>()
                        .ToArray();
                }

                action = new WebSearchAction(
                    Type: TryGetString(actionEl, "type"),
                    Query: TryGetString(actionEl, "query"),
                    Queries: queries);
            }

            return new WebSearchCallResponseItemPayload
            {
                PayloadType = payloadType,
                Status = TryGetString(payload, "status"),
                Action = action
            };
        }

        if (string.Equals(payloadType, "ghost_snapshot", StringComparison.OrdinalIgnoreCase))
        {
            GhostCommit? commit = null;
            if (payload.TryGetProperty("ghost_commit", out var commitEl) && commitEl.ValueKind == JsonValueKind.Object)
            {
                IReadOnlyList<string>? files = null;
                if (commitEl.TryGetProperty("preexisting_untracked_files", out var filesEl) && filesEl.ValueKind == JsonValueKind.Array)
                {
                    files = filesEl.EnumerateArray()
                        .Select(f => f.ValueKind == JsonValueKind.String ? f.GetString() : null)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Cast<string>()
                        .ToArray();
                }

                IReadOnlyList<string>? dirs = null;
                if (commitEl.TryGetProperty("preexisting_untracked_dirs", out var dirsEl) && dirsEl.ValueKind == JsonValueKind.Array)
                {
                    dirs = dirsEl.EnumerateArray()
                        .Select(d => d.ValueKind == JsonValueKind.String ? d.GetString() : null)
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Cast<string>()
                        .ToArray();
                }

                commit = new GhostCommit(
                    Id: TryGetString(commitEl, "id"),
                    Parent: TryGetString(commitEl, "parent"),
                    PreexistingUntrackedFiles: files,
                    PreexistingUntrackedDirs: dirs);
            }

            return new GhostSnapshotResponseItemPayload
            {
                PayloadType = payloadType,
                GhostCommit = commit
            };
        }

        if (string.Equals(payloadType, "compaction", StringComparison.OrdinalIgnoreCase))
        {
            return new CompactionResponseItemPayload
            {
                PayloadType = payloadType,
                EncryptedContent = TryGetString(payload, "encrypted_content")
            };
        }

        return new UnknownResponseItemPayload
        {
            PayloadType = payloadType,
            Raw = payload.Clone()
        };
    }

    private static IReadOnlyList<ResponseMessageContentPart> ParseMessageContent(JsonElement payload)
    {
        if (!payload.TryGetProperty("content", out var contentArray) || contentArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ResponseMessageContentPart>();
        }

        var parts = new List<ResponseMessageContentPart>();
        foreach (var c in contentArray.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object)
                continue;

            var contentType = TryGetString(c, "type");
            if (string.IsNullOrWhiteSpace(contentType))
                continue;

            if (string.Equals(contentType, "input_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(c, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "output_text", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageOutputTextPart
                {
                    ContentType = contentType,
                    Text = TryGetString(c, "text") ?? string.Empty
                });
                continue;
            }

            if (string.Equals(contentType, "input_image", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ResponseMessageInputImagePart
                {
                    ContentType = contentType,
                    ImageUrl = TryGetString(c, "image_url") ?? string.Empty
                });
                continue;
            }

            parts.Add(new UnknownResponseMessageContentPart
            {
                ContentType = contentType,
                Raw = c.Clone()
            });
        }

        return parts;
    }

    private static JsonElement GetEventBody(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("payload", out var payload) &&
            payload.ValueKind == JsonValueKind.Object)
        {
            root = payload;
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("msg", out var msg) &&
            msg.ValueKind == JsonValueKind.Object)
        {
            root = msg;
        }

        return root;
    }
}
