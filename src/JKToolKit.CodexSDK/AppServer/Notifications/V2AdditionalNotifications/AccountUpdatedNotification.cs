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
    /// Gets the plan type, if present.
    /// </summary>
    public string? PlanType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AccountUpdatedNotification"/>.
    /// </summary>
    public AccountUpdatedNotification(string? AuthMode, JsonElement Params, string? PlanType = null)
        : base("account/updated", Params)
    {
        this.AuthMode = AuthMode;
        this.PlanType = !string.IsNullOrWhiteSpace(PlanType)
            ? PlanType
            : TryGetString(Params, "planType");
    }

    private static string? TryGetString(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object &&
        obj.TryGetProperty(propertyName, out var prop) &&
        prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
