using System.Text.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadResponseParsers
{
    public static CodexThread ParseLifecycleThread(JsonElement result, string? fallbackThreadId = null)
    {
        var threadObject = TryGetObject(result, "thread") ?? result;
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject, result) ?? new CodexThreadSummary
        {
            ThreadId = fallbackThreadId ?? ExtractThreadId(result) ?? throw new InvalidOperationException("The response did not contain a thread id."),
            Raw = threadObject
        };

        var approvalPolicy = CodexApprovalPolicy.TryParse(GetStringOrNull(result, "approvalPolicy"), out var parsedApprovalPolicy)
            ? parsedApprovalPolicy
            : (CodexApprovalPolicy?)null;
        var approvalsReviewer = CodexApprovalsReviewerParser.ParseOrNull(GetStringOrNull(result, "approvalsReviewer"));
        var sandbox = CodexSandboxMode.TryParse(GetStringOrNull(result, "sandbox"), out var parsedSandbox)
            ? parsedSandbox
            : (CodexSandboxMode?)null;
        var serviceTier = CodexServiceTier.TryParse(GetStringOrNull(result, "serviceTier"), out var parsedServiceTier)
            ? parsedServiceTier
            : summary.ServiceTier;
        var approvalPolicyRaw = TryGetElement(result, "approvalPolicy");
        var sandboxRaw = TryGetElement(result, "sandbox");
        var reasoningEffort = CodexReasoningEffort.TryParse(GetStringOrNull(result, "reasoningEffort"), out var parsedReasoningEffort)
            ? parsedReasoningEffort
            : (CodexReasoningEffort?)null;

        return new CodexThread(
            summary.ThreadId,
            result,
            summary,
            approvalPolicy,
            approvalPolicyRaw,
            approvalsReviewer,
            sandbox,
            sandboxRaw,
            serviceTier,
            reasoningEffort);
    }

    public static CodexThreadReadResult ParseReadResult(JsonElement result, string fallbackThreadId)
    {
        var threadObject = TryGetObject(result, "thread") ?? result;
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject, result) ?? new CodexThreadSummary
        {
            ThreadId = fallbackThreadId,
            Raw = threadObject
        };

        return new CodexThreadReadResult
        {
            Thread = summary,
            Turns = summary.Turns,
            Raw = result
        };
    }
}
