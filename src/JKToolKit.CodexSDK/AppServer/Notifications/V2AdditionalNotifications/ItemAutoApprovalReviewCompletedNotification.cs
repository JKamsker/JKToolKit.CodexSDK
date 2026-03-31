using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Notification emitted when guardian auto-approval review completes for a tool action.
/// </summary>
public sealed record class ItemAutoApprovalReviewCompletedNotification : AppServerNotification
{
    /// <summary>
    /// Gets the thread identifier.
    /// </summary>
    public string ThreadId { get; }

    /// <summary>
    /// Gets the turn identifier.
    /// </summary>
    public string TurnId { get; }

    /// <summary>
    /// Gets the reviewed item identifier.
    /// </summary>
    public string TargetItemId { get; }

    /// <summary>
    /// Gets the reviewed action payload.
    /// </summary>
    public JsonElement Action { get; }

    /// <summary>
    /// Gets the parsed review details.
    /// </summary>
    public GuardianApprovalReviewInfo Review { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ItemAutoApprovalReviewCompletedNotification"/>.
    /// </summary>
    public ItemAutoApprovalReviewCompletedNotification(
        string threadId,
        string turnId,
        string targetItemId,
        JsonElement action,
        GuardianApprovalReviewInfo review,
        JsonElement @params)
        : base("item/autoApprovalReview/completed", @params)
    {
        ThreadId = threadId;
        TurnId = turnId;
        TargetItemId = targetItemId;
        Action = action;
        Review = review;
    }
}
