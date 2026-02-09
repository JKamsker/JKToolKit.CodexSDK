using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when authentication status changes.
/// </summary>
public sealed record class AuthStatusChangeNotification : AppServerNotification
{
    /// <summary>
    /// Gets the authentication method identifier, if any.
    /// </summary>
    public string? AuthMethod { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AuthStatusChangeNotification"/>.
    /// </summary>
    public AuthStatusChangeNotification(string? AuthMethod, JsonElement Params)
        : base("authStatusChange", Params)
    {
        this.AuthMethod = AuthMethod;
    }
}
