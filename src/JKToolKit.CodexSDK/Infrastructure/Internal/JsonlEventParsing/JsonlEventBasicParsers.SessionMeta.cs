using System.Text.Json;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

using static JsonlEventJson;

internal static partial class JsonlEventBasicParsers
{
    public static SessionMetaEvent? ParseSessionMetaEvent(
        JsonElement root,
        DateTimeOffset timestamp,
        string type,
        JsonElement rawPayload,
        in JsonlEventParserContext ctx)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            ctx.Logger.LogWarning("session_meta event missing 'payload' field");
            return null;
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            ctx.Logger.LogWarning("session_meta event has non-object 'payload' field");
            return null;
        }

        if (!payload.TryGetProperty("id", out var idElement))
        {
            ctx.Logger.LogWarning("session_meta event missing 'payload.id' field");
            return null;
        }

        var idString = idElement.ValueKind switch
        {
            JsonValueKind.String => idElement.GetString(),
            JsonValueKind.Null => null,
            _ => idElement.GetRawText()
        };
        if (string.IsNullOrWhiteSpace(idString))
        {
            ctx.Logger.LogWarning("session_meta event has empty 'payload.id' field");
            return null;
        }

        if (!SessionId.TryParse(idString, out var sessionId))
        {
            ctx.Logger.LogWarning("session_meta event has invalid 'payload.id' field");
            return null;
        }

        var cwd = TryGetString(payload, "cwd");
        var cliVersion = TryGetString(payload, "cli_version");
        var originator = TryGetString(payload, "originator");
        var (source, sourceSubagent) = ParseSessionSource(payload);
        var sourceJson = payload.TryGetProperty("source", out var sourceEl) &&
                         sourceEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? sourceEl.Clone()
            : (JsonElement?)null;
        var modelProvider = TryGetString(payload, "model_provider");
        var forkedFromSessionId = TryParseSessionId(payload, "forked_from_id");
        var baseInstructions = payload.TryGetProperty("base_instructions", out var baseInstructionsEl) &&
                               baseInstructionsEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? baseInstructionsEl.Clone()
            : (JsonElement?)null;
        var dynamicTools = payload.TryGetProperty("dynamic_tools", out var dynamicToolsEl) &&
                           dynamicToolsEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? dynamicToolsEl.Clone()
            : (JsonElement?)null;
        var git = payload.TryGetProperty("git", out var gitEl) &&
                  gitEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? gitEl.Clone()
            : (JsonElement?)null;

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
            SourceJson = sourceJson,
            SourceSubagent = sourceSubagent,
            ForkedFromSessionId = forkedFromSessionId,
            AgentNickname = TryGetString(payload, "agent_nickname"),
            AgentRole = TryGetString(payload, "agent_role") ?? TryGetString(payload, "agent_type"),
            AgentPath = TryGetString(payload, "agent_path"),
            ModelProvider = modelProvider,
            BaseInstructions = baseInstructions,
            DynamicTools = dynamicTools,
            Git = git,
            MemoryMode = TryGetString(payload, "memory_mode")
        };
    }

    private static SessionId? TryParseSessionId(JsonElement payload, string propertyName)
    {
        var value = TryGetStringOrRaw(payload, propertyName);
        if (SessionId.TryParse(value, out var sessionId))
        {
            return sessionId;
        }

        return null;
    }

    private static void ParseNetworkAccess(
        JsonElement sandboxPolicy,
        ref bool? networkAccess,
        ref string? networkAccessMode)
    {
        if (sandboxPolicy.TryGetProperty("network_access", out var networkAccessEl))
        {
            if (TryParseNetworkAccessValue(networkAccessEl, out var parsedAccess, out var parsedMode))
            {
                networkAccess = parsedAccess;
                networkAccessMode = parsedMode;
                return;
            }
        }

        foreach (var property in sandboxPolicy.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryParseNetworkAccessValue(property.Value, out var parsedAccess, out var parsedMode))
            {
                networkAccess = parsedAccess;
                networkAccessMode = parsedMode;
                return;
            }
        }
    }

    private static bool TryParseNetworkAccessValue(
        JsonElement value,
        out bool? networkAccess,
        out string? networkAccessMode)
    {
        networkAccess = null;
        networkAccessMode = null;

        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                networkAccess = true;
                networkAccessMode = "enabled";
                return true;
            case JsonValueKind.False:
                networkAccess = false;
                networkAccessMode = "restricted";
                return true;
            case JsonValueKind.String:
            {
                var mode = value.GetString();
                if (string.IsNullOrWhiteSpace(mode))
                {
                    return false;
                }

                networkAccessMode = mode;
                networkAccess = mode.Equals("enabled", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : mode.Equals("restricted", StringComparison.OrdinalIgnoreCase)
                        ? false
                        : null;
                return true;
            }
            case JsonValueKind.Object:
            {
                if (!value.TryGetProperty("network_access", out var nestedNetworkAccess))
                {
                    return false;
                }

                return TryParseNetworkAccessValue(nestedNetworkAccess, out networkAccess, out networkAccessMode);
            }
            default:
                return false;
        }
    }

    private static (string? Source, string? SourceSubagent) ParseSessionSource(JsonElement payload)
    {
        if (!payload.TryGetProperty("source", out var sourceEl))
        {
            return (null, null);
        }

        if (sourceEl.ValueKind == JsonValueKind.String)
        {
            return (sourceEl.GetString(), null);
        }

        if (sourceEl.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        if (!sourceEl.TryGetProperty("subagent", out var subagentEl))
        {
            return (sourceEl.GetRawText(), null);
        }

        return ("subagent", ParseSubagentSource(subagentEl));
    }

    private static string? ParseSubagentSource(JsonElement subagentEl)
    {
        if (subagentEl.ValueKind == JsonValueKind.String)
        {
            return subagentEl.GetString();
        }

        if (subagentEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (subagentEl.TryGetProperty("thread_spawn", out _))
        {
            return "thread_spawn";
        }

        if (subagentEl.TryGetProperty("other", out var otherEl) && otherEl.ValueKind == JsonValueKind.String)
        {
            return otherEl.GetString();
        }

        foreach (var prop in subagentEl.EnumerateObject())
        {
            return prop.Name;
        }

        return null;
    }
}
