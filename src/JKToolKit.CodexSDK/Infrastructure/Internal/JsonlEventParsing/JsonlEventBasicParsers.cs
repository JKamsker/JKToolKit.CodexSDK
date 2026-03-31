using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventBasicParsers
{

    public static UserMessageEvent? ParseUserMessageEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        var payload = GetEventBody(root);
        if (!TryGetMessageOrText(payload, out var msgString))
        {
            ctx.Logger.LogWarning("user_message event missing message/text field");
            return null;
        }

        return new UserMessageEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = msgString ?? string.Empty
        };
    }

    public static AgentMessageEvent? ParseAgentMessageEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        var payload = GetEventBody(root);
        if (!TryGetMessageOrText(payload, out var msgString))
        {
            ctx.Logger.LogWarning("agent_message event missing message/text field");
            return null;
        }

        return new AgentMessageEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = msgString ?? string.Empty
        };
    }

    public static AgentReasoningEvent? ParseAgentReasoningEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        var payload = GetEventBody(root);
        if (!TryGetMessageOrText(payload, out var reasoningString))
        {
            ctx.Logger.LogWarning("agent_reasoning event missing message/text field");
            return null;
        }

        return new AgentReasoningEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            Text = reasoningString ?? string.Empty
        };
    }

    public static TokenCountEvent? ParseTokenCountEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
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

    public static TurnContextEvent? ParseTurnContextEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        string? approvalPolicy = null;
        string? sandboxPolicyType = null;
        bool? networkAccess = null;
        string? networkAccessMode = null;
        JsonElement? sandboxPolicyJson = null;

        if (root.TryGetProperty("payload", out var payload))
        {
            if (payload.TryGetProperty("approval_policy", out var approvalElement))
            {
                approvalPolicy = approvalElement.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => approvalElement.GetString(),
                    _ => approvalElement.GetRawText()
                };
            }

            // Codex has produced both `sandbox_policy_type: string` and `sandbox_policy: { type, network_access }`.
            if (payload.TryGetProperty("sandbox_policy_type", out var sandboxElement) && sandboxElement.ValueKind == JsonValueKind.String)
                sandboxPolicyType = sandboxElement.GetString();

            if (payload.TryGetProperty("sandbox_policy", out var sandboxPolicy) && sandboxPolicy.ValueKind == JsonValueKind.Object)
            {
                sandboxPolicyJson = sandboxPolicy.Clone();

                if (sandboxPolicy.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                    sandboxPolicyType = typeEl.GetString();

                ParseNetworkAccess(sandboxPolicy, ref networkAccess, ref networkAccessMode);
            }
        }

        return new TurnContextEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload,
            ApprovalPolicy = approvalPolicy,
            SandboxPolicyType = sandboxPolicyType,
            NetworkAccess = networkAccess,
            NetworkAccessMode = networkAccessMode,
            SandboxPolicyJson = sandboxPolicyJson
        };
    }

    public static CompactedEvent? ParseCompactedEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("compacted event missing 'payload' object");
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

                var itemType = TryGetString(item, "type");
                if (string.IsNullOrWhiteSpace(itemType))
                {
                    replacementHistory.Add(new UnknownResponseItemPayload { PayloadType = "unknown", Raw = item.Clone() });
                    continue;
                }

                replacementHistory.Add(JsonlEventResponseItemParsers.ParseResponseItemPayload(itemType, item));
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

    public static UnknownCodexEvent ParseUnknownEvent(
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        ctx.Logger.LogDebug("Encountered unknown event type: {Type}", type);

        return new UnknownCodexEvent
        {
            Timestamp = timestamp,
            Type = type,
            RawPayload = rawPayload
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

    private static RateLimits? ParseRateLimits(JsonElement rateLimitsElement)
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

    private static bool TryGetMessageOrText(JsonElement payload, out string? value)
    {
        if (payload.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String)
        {
            value = messageEl.GetString();
            return true;
        }

        if (payload.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
        {
            value = textEl.GetString();
            return true;
        }

        value = null;
        return false;
    }

}
