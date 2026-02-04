using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

public sealed record class McpServerOauthLoginCompletedNotification : AppServerNotification
{
    public string Name { get; }
    public bool Success { get; }
    public string? Error { get; }

    public McpServerOauthLoginCompletedNotification(string Name, bool Success, string? Error, JsonElement Params)
        : base("mcpServer/oauthLogin/completed", Params)
    {
        this.Name = Name;
        this.Success = Success;
        this.Error = Error;
    }
}

