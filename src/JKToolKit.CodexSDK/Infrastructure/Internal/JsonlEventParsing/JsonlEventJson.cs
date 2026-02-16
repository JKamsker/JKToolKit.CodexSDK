using System.Globalization;
using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.Internal.JsonlEventParsing;

internal static class JsonlEventJson
{
    public static string? TryGetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    public static double? TryGetDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.Number when p.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(
                p.GetString(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var d) => d,
            _ => null
        };
    }

    public static int? TryGetInt(JsonElement el, string name)
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

    public static JsonElement GetEventBody(JsonElement root)
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

