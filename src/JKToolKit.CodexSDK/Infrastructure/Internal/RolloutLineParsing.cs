using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class RolloutLineParsing
{
    public static bool TryGetPayloadObject(JsonElement root, string eventType, out JsonElement payload)
    {
        payload = default;

        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("type", out var typeElement) &&
               typeElement.ValueKind == JsonValueKind.String &&
               string.Equals(typeElement.GetString(), eventType, StringComparison.Ordinal) &&
               root.TryGetProperty("payload", out payload) &&
               payload.ValueKind == JsonValueKind.Object;
    }

    public static DateTimeOffset? GetTopLevelTimestampOrNull(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("timestamp", out var timestampElement) ||
            timestampElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(timestampElement.GetString(), out var parsed) ? parsed : null;
    }

    public static DateTimeOffset? GetPayloadTimestampOrNull(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("timestamp", out var timestampElement) ||
            timestampElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(timestampElement.GetString(), out var parsed) ? parsed : null;
    }
}
