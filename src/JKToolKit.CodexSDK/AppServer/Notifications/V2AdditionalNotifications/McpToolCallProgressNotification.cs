using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class McpToolCallProgressNotification : AppServerNotification
{
    public string ThreadId { get; }
    public string TurnId { get; }
    public string ItemId { get; }
    public string Message { get; }

    public McpToolCallProgressNotification(string ThreadId, string TurnId, string ItemId, string Message, JsonElement Params)
        : base("item/mcpToolCall/progress", Params)
    {
        this.ThreadId = ThreadId;
        this.TurnId = TurnId;
        this.ItemId = ItemId;
        this.Message = Message;
    }
}

