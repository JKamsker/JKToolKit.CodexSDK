using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientOpenTelemetryRequirementsParser
{
    public static OpenTelemetryRequirements Parse(JsonElement otel)
    {
        var toolResult = TryGetObject(otel, "tool_result");
        return new OpenTelemetryRequirements
        {
            ToolResultMaxBytes = toolResult is { } value ? GetInt64OrNull(value, "max_bytes") : null,
            LogUserPrompt = GetBoolOrNull(otel, "log_user_prompt"),
            LogAgentResponses = GetBoolOrNull(otel, "log_agent_responses"),
            LogGuardianAssessments = GetBoolOrNull(otel, "log_guardian_assessments"),
            Environment = GetStringOrNull(otel, "environment"),
            Exporter = CloneOptionalValue(otel, "exporter"),
            TraceExporter = CloneOptionalValue(otel, "trace_exporter"),
            MetricsExporter = CloneOptionalValue(otel, "metrics_exporter"),
            SpanAttributes = ParseStringMap(otel, "span_attributes"),
            TraceState = ParseNestedStringMap(otel, "tracestate"),
            Raw = otel.Clone()
        };
    }

    private static JsonElement? CloneOptionalValue(JsonElement obj, string propertyName)
    {
        var value = TryGetElement(obj, propertyName);
        return value is { } element && element.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? element.Clone()
            : null;
    }

    private static IReadOnlyDictionary<string, string>? ParseStringMap(JsonElement obj, string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        return values.EnumerateObject()
            .Where(static property => property.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                static property => property.Name,
                static property => property.Value.GetString()!,
                StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ParseNestedStringMap(
        JsonElement obj,
        string propertyName)
    {
        if (TryGetObject(obj, propertyName) is not { } values)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var property in values.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                result[property.Name] = property.Value.EnumerateObject()
                    .Where(static nested => nested.Value.ValueKind == JsonValueKind.String)
                    .ToDictionary(
                        static nested => nested.Name,
                        static nested => nested.Value.GetString()!,
                        StringComparer.Ordinal);
            }
        }

        return result;
    }
}
