using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TurnCompletedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public JsonElement Turn { get; }

    public TurnCompletedNotification(string ThreadId, JsonElement Turn, JsonElement Params)
        : base("turn/completed", Params)
    {
        this.ThreadId = ThreadId;
        this.Turn = Turn;
    }

    public string? TurnId =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty("id", out var id) &&
        id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;

    public string? Status =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty("status", out var s) &&
        s.ValueKind == JsonValueKind.String
            ? s.GetString()
            : null;

    public JsonElement? Error =>
        Turn.ValueKind == JsonValueKind.Object &&
        Turn.TryGetProperty("error", out var e) &&
        e.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)
            ? e
            : null;
}


