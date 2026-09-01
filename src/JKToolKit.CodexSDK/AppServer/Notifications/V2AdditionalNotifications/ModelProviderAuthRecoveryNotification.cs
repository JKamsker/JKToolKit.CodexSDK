using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when model-provider authentication recovery starts or completes.
/// </summary>
public sealed record class ModelProviderAuthRecoveryNotification(
    string MethodName,
    string ThreadId,
    string TurnId,
    string Provider,
    string Message,
    JsonElement Params) : AppServerNotification(MethodName, Params);
