using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when an account login flow completes.
/// </summary>
public sealed record class AccountLoginCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets an optional login flow identifier.
    /// </summary>
    public string? LoginId { get; }

    /// <summary>
    /// Gets a value indicating whether the login succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets an optional error message if <see cref="Success"/> is false.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AccountLoginCompletedNotification"/>.
    /// </summary>
    public AccountLoginCompletedNotification(string? LoginId, bool Success, string? Error, JsonElement Params)
        : base(AppServerMethods.AccountLoginCompleted, Params)
    {
        this.LoginId = LoginId;
        this.Success = Success;
        this.Error = Error;
    }
}
