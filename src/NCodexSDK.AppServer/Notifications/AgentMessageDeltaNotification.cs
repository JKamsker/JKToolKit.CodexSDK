using System.Text.Json;

namespace NCodexSDK.AppServer.Notifications;

public sealed record AgentMessageDeltaNotification(
    string ThreadId,
    string TurnId,
    string ItemId,
    string Delta,
    JsonElement Params)
    : AppServerNotification("item/agentMessage/delta", Params);

