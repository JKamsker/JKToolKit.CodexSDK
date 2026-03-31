namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexApprovalsReviewerParser
{
    public static CodexApprovalsReviewer? ParseOrNull(string? value) =>
        value switch
        {
            "user" => CodexApprovalsReviewer.User,
            "guardian_subagent" => CodexApprovalsReviewer.GuardianSubagent,
            _ => null
        };
}
