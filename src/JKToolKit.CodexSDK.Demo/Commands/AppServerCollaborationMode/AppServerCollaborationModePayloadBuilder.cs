using System.Text.Json;
using System.Text.Json.Nodes;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Demo.Commands.AppServerCollaborationMode;

public sealed record class CollaborationModeMaskProjection
{
    public string Mode { get; init; } = "default";

    public string Model { get; init; } = CodexModel.Gpt52Codex.Value;

    public string? ReasoningEffort { get; init; }

    public string? DeveloperInstructions { get; init; }

    public bool IncludesDeveloperInstructions { get; init; }
}

public static class AppServerCollaborationModePayloadBuilder
{
    public static JsonElement BuildCollaborationModeJson(CollaborationModeMaskProjection mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var settings = new JsonObject
        {
            ["model"] = mask.Model
        };

        if (!string.IsNullOrWhiteSpace(mask.ReasoningEffort))
        {
            settings["reasoning_effort"] = mask.ReasoningEffort;
        }

        if (mask.IncludesDeveloperInstructions)
        {
            settings["developer_instructions"] = mask.DeveloperInstructions is null
                ? null
                : JsonValue.Create(mask.DeveloperInstructions);
        }

        var collab = new JsonObject
        {
            ["mode"] = mask.Mode,
            ["settings"] = settings
        };

        return JsonDocument.Parse(collab.ToJsonString()).RootElement.Clone();
    }

    public static bool TryGetFirstMask(JsonElement result, out CollaborationModeMaskProjection mask)
    {
        mask = new CollaborationModeMaskProjection();

        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var enumerator = data.EnumerateArray();
        if (!enumerator.MoveNext() || enumerator.Current.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var first = enumerator.Current;
        var settings = first.TryGetProperty("settings", out var nestedSettings) && nestedSettings.ValueKind == JsonValueKind.Object
            ? nestedSettings
            : default;

        var mode = TryGetString(first, "mode") ?? mask.Mode;
        var model = TryGetString(first, "model") ?? TryGetString(settings, "model") ?? mask.Model;
        var reasoningEffort =
            TryGetString(first, "reasoning_effort") ??
            TryGetString(first, "reasoningEffort") ??
            TryGetString(settings, "reasoning_effort") ??
            TryGetString(settings, "reasoningEffort");
        var includesDeveloperInstructions =
            TryGetOptionalProperty(first, "developer_instructions", out var developerInstructions) ||
            TryGetOptionalProperty(first, "developerInstructions", out developerInstructions) ||
            TryGetOptionalProperty(settings, "developer_instructions", out developerInstructions) ||
            TryGetOptionalProperty(settings, "developerInstructions", out developerInstructions);

        mask = new CollaborationModeMaskProjection
        {
            Mode = mode,
            Model = model,
            ReasoningEffort = reasoningEffort,
            DeveloperInstructions = developerInstructions,
            IncludesDeveloperInstructions = includesDeveloperInstructions
        };

        return true;
    }

    private static bool TryGetOptionalProperty(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
        }

        return property.ValueKind is JsonValueKind.String or JsonValueKind.Null;
    }

    private static string? TryGetString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object ||
            !obj.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
