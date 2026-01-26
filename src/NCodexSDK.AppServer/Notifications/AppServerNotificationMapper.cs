using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

internal static class AppServerNotificationMapper
{
    public static AppServerNotification Map(string method, JsonElement? @params)
    {
        if (@params is null || @params.Value.ValueKind != JsonValueKind.Object)
        {
            using var emptyDoc = JsonDocument.Parse("{}");
            return new UnknownNotification(method, emptyDoc.RootElement.Clone());
        }

        var p = @params.Value;

        return method switch
        {
            "item/agentMessage/delta" => new AgentMessageDeltaNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Delta: GetString(p, "delta") ?? string.Empty,
                Params: p),

            "item/started" => new ItemStartedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Params: p),

            "item/completed" => new ItemCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                ItemId: GetString(p, "itemId") ?? string.Empty,
                Params: p),

            "turn/completed" => new TurnCompletedNotification(
                ThreadId: GetString(p, "threadId") ?? string.Empty,
                TurnId: GetString(p, "turnId") ?? string.Empty,
                Status: GetString(p, "status") ?? string.Empty,
                Error: p.TryGetProperty("error", out var err) ? err.Clone() : null,
                Params: p),

            _ => new UnknownNotification(method, p)
        };
    }

    private static string? GetString(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}

