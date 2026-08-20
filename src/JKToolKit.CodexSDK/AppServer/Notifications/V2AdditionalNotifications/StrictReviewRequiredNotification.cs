using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when Guardian requires a strict review.
/// </summary>
public sealed record class StrictReviewRequiredNotification(
    string ThreadId,
    string TurnId,
    long StartedAtMs,
    JsonElement Params) : AppServerNotification("autoApprovalReview/strictReviewRequired", Params);
