using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when thread settings are updated.
/// </summary>
public sealed record class ThreadSettingsUpdatedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the raw thread settings payload.
    /// </summary>
    public JsonElement ThreadSettings { get; }

    /// <summary>
    /// Gets the updated working directory, when present.
    /// </summary>
    public string? Cwd { get; }

    /// <summary>
    /// Gets the updated model, when present.
    /// </summary>
    public string? Model { get; }

    /// <summary>
    /// Gets the updated service tier, when present.
    /// </summary>
    public string? ServiceTier { get; }

    /// <summary>
    /// Gets the updated disabled plugin identifiers.
    /// </summary>
    public IReadOnlyList<string> DisabledPluginIds { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ThreadSettingsUpdatedNotification"/>.
    /// </summary>
    public ThreadSettingsUpdatedNotification(
        string threadId,
        JsonElement threadSettings,
        string? cwd,
        string? model,
        string? serviceTier,
        IReadOnlyList<string>? disabledPluginIds,
        JsonElement @params)
        : base(AppServerMethods.ThreadSettingsUpdated, @params)
    {
        ThreadId = threadId;
        ThreadSettings = threadSettings;
        Cwd = cwd;
        Model = model;
        ServiceTier = serviceTier;
        DisabledPluginIds = disabledPluginIds ?? Array.Empty<string>();
    }
}
