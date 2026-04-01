namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when account information is updated.
/// </summary>
public sealed record class AccountUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the authentication mode, if present.
    /// </summary>
    public CodexAuthMode? AuthMode { get; }

    /// <summary>
    /// Gets the plan type, if present.
    /// </summary>
    public CodexPlanType? PlanType { get; }

    /// <summary>
    /// Gets the raw auth-mode wire value, when present.
    /// </summary>
    public string? AuthModeValue => AuthMode?.Value;

    /// <summary>
    /// Gets the raw plan-type wire value, when present.
    /// </summary>
    public string? PlanTypeValue => PlanType?.Value;

    /// <summary>
    /// Initializes a new instance of <see cref="AccountUpdatedNotification"/>.
    /// </summary>
    public AccountUpdatedNotification(CodexAuthMode? AuthMode, CodexPlanType? PlanType, System.Text.Json.JsonElement Params)
        : base("account/updated", Params)
    {
        this.AuthMode = AuthMode;
        this.PlanType = PlanType;
    }
}
