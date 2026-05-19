using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Specifies who reviews app-server approval requests.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CodexApprovalsReviewer>))]
public enum CodexApprovalsReviewer
{
    /// <summary>
    /// Route approvals to the user.
    /// </summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>
    /// Route approvals to the guardian subagent.
    /// </summary>
    [JsonStringEnumMemberName("guardian_subagent")]
    GuardianSubagent,

    /// <summary>
    /// Route approvals to upstream automatic approval review.
    /// </summary>
    [JsonStringEnumMemberName("auto_review")]
    AutoReview
}
