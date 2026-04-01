using System.Collections.Generic;
using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventBasicParsers
{
    public static TurnContextEvent? ParseTurnContextEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("turn_context event missing object 'payload' field");
            return null;
        }

        string? approvalPolicy = null;
        string? sandboxPolicyType = null;
        bool? networkAccess = null;
        string? networkAccessMode = null;
        JsonElement? sandboxPolicyJson = null;

        string? turnId = null;
        string? traceId = null;
        string? cwd = null;
        string? currentDate = null;
        string? timezone = null;
        CodexModel? model = null;
        string? personality = null;
        JsonElement? collaborationMode = null;
        bool? realtimeActive = null;
        CodexReasoningEffort? reasoningEffort = null;
        JsonElement? reasoningSummary = null;
        string? userInstructions = null;
        string? developerInstructions = null;
        JsonElement? finalOutputJsonSchema = null;
        JsonElement? truncationPolicy = null;
        TurnContextNetwork? network = null;

        if (payload.ValueKind == JsonValueKind.Object)
        {
            approvalPolicy = TryGetString(payload, "approval_policy");

            if (payload.TryGetProperty("sandbox_policy_type", out var sandboxElement) && sandboxElement.ValueKind == JsonValueKind.String)
            {
                sandboxPolicyType = sandboxElement.GetString();
            }

            if (payload.TryGetProperty("sandbox_policy", out var sandboxPolicy) && sandboxPolicy.ValueKind == JsonValueKind.Object)
            {
                sandboxPolicyJson = sandboxPolicy.Clone();

                if (sandboxPolicy.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                {
                    sandboxPolicyType = typeEl.GetString();
                }

                ParseNetworkAccess(sandboxPolicy, ref networkAccess, ref networkAccessMode);
            }

            turnId = TryGetString(payload, "turn_id");
            traceId = TryGetString(payload, "trace_id");
            cwd = TryGetString(payload, "cwd");
            currentDate = TryGetString(payload, "current_date");
            timezone = TryGetString(payload, "timezone");

            if (TryGetString(payload, "model") is { } modelString &&
                CodexModel.TryParse(modelString, out var parsedModel))
            {
                model = parsedModel;
            }

            personality = TryGetString(payload, "personality");

            if (payload.TryGetProperty("collaboration_mode", out var collaborationEl) &&
                collaborationEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                collaborationMode = collaborationEl.Clone();
            }

            if (payload.TryGetProperty("realtime_active", out var realtimeEl))
            {
                realtimeActive = realtimeEl.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(realtimeEl.GetString(), out var parsed) => parsed,
                    _ => null
                };
            }

            if (TryGetString(payload, "effort") is { } effortString &&
                CodexReasoningEffort.TryParse(effortString, out var parsedEffort))
            {
                reasoningEffort = parsedEffort;
            }

            if (payload.TryGetProperty("summary", out var summaryEl) &&
                summaryEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                reasoningSummary = summaryEl.Clone();
            }

            userInstructions = TryGetString(payload, "user_instructions");
            developerInstructions = TryGetString(payload, "developer_instructions");

            if (payload.TryGetProperty("final_output_json_schema", out var finalSchemaEl) &&
                finalSchemaEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                finalOutputJsonSchema = finalSchemaEl.Clone();
            }

            if (payload.TryGetProperty("truncation_policy", out var truncationEl) &&
                truncationEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                truncationPolicy = truncationEl.Clone();
            }

            if (payload.TryGetProperty("network", out var networkEl) &&
                networkEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                network = ParseTurnContextNetwork(networkEl);
            }
        }

        if (string.IsNullOrWhiteSpace(approvalPolicy) ||
            string.IsNullOrWhiteSpace(sandboxPolicyType) ||
            string.IsNullOrWhiteSpace(cwd) ||
            model is null ||
            reasoningSummary is null)
        {
            ctx.Logger.LogWarning("turn_context event missing required upstream fields");
            return null;
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
            SandboxPolicyJson = sandboxPolicyJson,
            TurnId = turnId,
            TraceId = traceId,
            Cwd = cwd,
            CurrentDate = currentDate,
            Timezone = timezone,
            Model = model,
            Personality = personality,
            CollaborationMode = collaborationMode,
            RealtimeActive = realtimeActive,
            ReasoningEffort = reasoningEffort,
            ReasoningSummary = reasoningSummary,
            UserInstructions = userInstructions,
            DeveloperInstructions = developerInstructions,
            FinalOutputJsonSchema = finalOutputJsonSchema,
            TruncationPolicy = truncationPolicy,
            Network = network
        };
    }

    private static TurnContextNetwork? ParseTurnContextNetwork(JsonElement networkEl)
    {
        if (networkEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var allowedDomains =
            TryGetStringArray(networkEl, "allowed_domains") ??
            TryGetStringArray(networkEl, "allowedDomains");
        var deniedDomains =
            TryGetStringArray(networkEl, "denied_domains") ??
            TryGetStringArray(networkEl, "deniedDomains");

        return new TurnContextNetwork(allowedDomains, deniedDomains);
    }

    private static IReadOnlyList<string>? TryGetStringArray(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } value)
            {
                values.Add(value);
            }
        }

        return values;
    }
}
