using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

public sealed record AppServerInitializeResult
{
    public JsonElement Raw { get; }

    public string? UserAgent { get; }

    public AppServerInitializeResult(JsonElement raw)
    {
        Raw = raw;

        UserAgent = raw.ValueKind == JsonValueKind.Object &&
                    raw.TryGetProperty("userAgent", out var ua) &&
                    ua.ValueKind == JsonValueKind.String
            ? ua.GetString()
            : null;
    }
}

