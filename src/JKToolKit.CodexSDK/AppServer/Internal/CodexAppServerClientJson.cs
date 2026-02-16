using System.Globalization;
using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerClientJson
{
    public static string? ExtractId(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    public static string? ExtractThreadId(JsonElement result)
    {
        // Common shapes:
        // - { "threadId": "..." }
        // - { "id": "..." }
        // - { "thread": { "id": "..." } }
        // - { "thread": { "threadId": "..." } }
        return ExtractId(result, "threadId", "id") ??
               ExtractIdByPath(result, "thread", "threadId") ??
               ExtractIdByPath(result, "thread", "id") ??
               FindStringPropertyRecursive(result, propertyName: "threadId", maxDepth: 6);
    }

    public static string? ExtractTurnId(JsonElement result)
    {
        // Common shapes:
        // - { "turnId": "..." }
        // - { "id": "..." }
        // - { "turn": { "id": "..." } }
        // - { "turn": { "turnId": "..." } }
        return ExtractId(result, "turnId", "id") ??
               ExtractIdByPath(result, "turn", "turnId") ??
               ExtractIdByPath(result, "turn", "id") ??
               FindStringPropertyRecursive(result, propertyName: "turnId", maxDepth: 6);
    }

    public static string? ExtractIdByPath(JsonElement element, string p1, string p2)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(p1, out var child) || child.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ExtractId(child, p2);
    }

    public static string? FindStringPropertyRecursive(JsonElement element, string propertyName, int maxDepth)
    {
        if (maxDepth < 0)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                foreach (var p in element.EnumerateObject())
                {
                    var found = FindStringPropertyRecursive(p.Value, propertyName, maxDepth - 1);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                return null;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindStringPropertyRecursive(item, propertyName, maxDepth - 1);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                return null;
            }
            default:
                return null;
        }
    }

    public static JsonElement? TryGetArray(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.Array
            ? p
            : null;

    public static JsonElement? TryGetObject(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.Object
            ? p
            : null;

    public static string? GetStringOrNull(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    public static int? GetInt32OrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
        {
            return i;
        }

        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out i))
        {
            return i;
        }

        return null;
    }

    public static bool? GetBoolOrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    public static IReadOnlyList<string>? GetOptionalStringArray(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (p.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in p.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }

    public static DateTimeOffset? GetDateTimeOffsetOrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.String)
        {
            var s = p.GetString();
            if (!string.IsNullOrWhiteSpace(s) &&
                DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                return dto;
            }
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var epoch))
        {
            // Best-effort: treat large values as milliseconds, otherwise seconds.
            return epoch > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return null;
    }
}

