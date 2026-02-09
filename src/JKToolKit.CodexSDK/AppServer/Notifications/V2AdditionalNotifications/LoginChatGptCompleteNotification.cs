using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when a ChatGPT login flow completes.
/// </summary>
public sealed record class LoginChatGptCompleteNotification : AppServerNotification
{
    /// <summary>
    /// Gets the login flow identifier.
    /// </summary>
    public string LoginId { get; }

    /// <summary>
    /// Gets a value indicating whether the login succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets an optional error message if <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="LoginChatGptCompleteNotification"/>.
    /// </summary>
    public LoginChatGptCompleteNotification(string LoginId, bool Success, string? Error, JsonElement Params)
        : base("loginChatGptComplete", Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}
