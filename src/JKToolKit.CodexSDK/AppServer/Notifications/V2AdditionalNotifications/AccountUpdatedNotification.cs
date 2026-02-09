using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when account information is updated.
/// </summary>
public sealed record class AccountUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the authentication mode, if present.
    /// </summary>
    public string? AuthMode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AccountUpdatedNotification"/>.
    /// </summary>
    public AccountUpdatedNotification(string? AuthMode, JsonElement Params)
        : base("account/updated", Params)
    {
        this.AuthMode = AuthMode;
    }
}
