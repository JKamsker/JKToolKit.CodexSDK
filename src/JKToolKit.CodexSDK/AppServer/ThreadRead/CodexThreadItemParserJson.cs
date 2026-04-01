using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer.ThreadRead;

internal static class CodexThreadItemParserJson
{
    public static IReadOnlyList<JsonElement> CloneElements(JsonElement array) =>
        array.EnumerateArray().Select(item => item.Clone()).ToArray();

    public static string? ExtractErrorMessage(JsonElement element)
    {
        if (!element.TryGetProperty("error", out var error))
        {
            return null;
        }

        return error.ValueKind switch
        {
            JsonValueKind.Object => CodexAppServerClientJson.GetStringOrNull(error, "message"),
            JsonValueKind.String => error.GetString(),
            _ => null
        };
    }

    public static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        var parsed = CodexAppServerClientJson.GetStringOrNull(element, propertyName);
        if (parsed is null)
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetOptionalString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    public static bool TryGetRequiredArray(JsonElement element, string propertyName, out JsonElement array)
    {
        array = default;
        var parsed = CodexAppServerClientJson.TryGetArray(element, propertyName);
        if (parsed is null)
        {
            return false;
        }

        array = parsed.Value;
        return true;
    }

    public static bool TryGetOptionalArray(JsonElement element, string propertyName, out JsonElement? array)
    {
        array = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        array = property;
        return true;
    }

    public static bool TryGetRequiredObject(JsonElement element, string propertyName, out JsonElement obj)
    {
        obj = default;
        var parsed = CodexAppServerClientJson.TryGetObject(element, propertyName);
        if (parsed is null)
        {
            return false;
        }

        obj = parsed.Value;
        return true;
    }

    public static bool TryGetOptionalObject(JsonElement element, string propertyName, out JsonElement? obj)
    {
        obj = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        obj = property;
        return true;
    }

    public static bool TryGetRequiredElement(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        value = property.Clone();
        return true;
    }

    public static bool TryGetOptionalInt32(JsonElement element, string propertyName, out int? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetOptionalInt64(JsonElement element, string propertyName, out long? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    public static bool TryGetOptionalBool(JsonElement element, string propertyName, out bool? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        value = property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

        return value is not null;
    }

    public static bool TryGetStringArray(JsonElement element, string propertyName, out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var parsed = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            parsed.Add(item.GetString() ?? string.Empty);
        }

        values = parsed;
        return true;
    }
}
