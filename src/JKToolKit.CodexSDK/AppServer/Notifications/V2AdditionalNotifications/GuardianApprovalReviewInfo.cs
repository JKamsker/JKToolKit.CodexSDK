using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

/// <summary>
/// Known guardian auto-approval review states.
/// </summary>
public enum GuardianApprovalReviewStatus
{
    /// <summary>
    /// The upstream status was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The review is still in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// The review approved the action.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// The review denied the action.
    /// </summary>
    Denied = 3,

    /// <summary>
    /// The review was aborted.
    /// </summary>
    Aborted = 4
}

/// <summary>
/// Known guardian risk levels.
/// </summary>
public enum GuardianRiskLevel
{
    /// <summary>
    /// The upstream risk level was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Low risk.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium risk.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High risk.
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical risk.
    /// </summary>
    Critical = 4
}

/// <summary>
/// Known guardian user-authorization levels.
/// </summary>
public enum GuardianUserAuthorization
{
    /// <summary>
    /// The upstream authorization level was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Low authorization.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium authorization.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High authorization.
    /// </summary>
    High = 3
}

/// <summary>
/// Known sources for a completed guardian auto-approval decision.
/// </summary>
public enum AutoReviewDecisionSource
{
    /// <summary>
    /// The upstream decision source was absent or unrecognized.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The decision was produced by an agent workflow.
    /// </summary>
    Agent = 1
}

/// <summary>
/// Parsed guardian auto-approval review details.
/// </summary>
public sealed record class GuardianApprovalReviewInfo
{
    /// <summary>
    /// Gets the parsed review status.
    /// </summary>
    public GuardianApprovalReviewStatus Status { get; init; }

    /// <summary>
    /// Gets the raw status value when present.
    /// </summary>
    public string? StatusValue { get; init; }

    /// <summary>
    /// Gets the review rationale when present.
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// Gets the risk score when present.
    /// </summary>
    public int? RiskScore { get; init; }

    /// <summary>
    /// Gets the parsed risk level when present.
    /// </summary>
    public GuardianRiskLevel? RiskLevel { get; init; }

    /// <summary>
    /// Gets the parsed user-authorization level when present.
    /// </summary>
    public GuardianUserAuthorization? UserAuthorization { get; init; }

    /// <summary>
    /// Gets the raw review payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
