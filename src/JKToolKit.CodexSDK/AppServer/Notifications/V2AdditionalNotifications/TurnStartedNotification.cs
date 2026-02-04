using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class TurnStartedNotification : AppServerNotification
{
    public string ThreadId { get; }
    public JsonElement Turn { get; }

    public TurnStartedNotification(string ThreadId, JsonElement Turn, JsonElement Params)
        : base("turn/started", Params)
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
}

