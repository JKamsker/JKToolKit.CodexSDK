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
        var runtimeWorkspaceRoots = GetOptionalStringArray(result, "runtimeWorkspaceRoots") ?? Array.Empty<string>();
        var instructionSources = GetOptionalStringArray(result, "instructionSources") ?? Array.Empty<string>();
        var activePermissionProfile = ParseActivePermissionProfile(result);
        var initialTurnsPage = ParseTurnsPage(result, "initialTurnsPage");

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
            reasoningEffort,
            runtimeWorkspaceRoots,
            instructionSources,
            activePermissionProfile,
            initialTurnsPage);
    }

    private static CodexTurnsPage? ParseTurnsPage(JsonElement result, string propertyName)
    {
        if (TryGetObject(result, propertyName) is not { } page)
        {
            return null;
        }

        var turns = new List<JKToolKit.CodexSDK.AppServer.ThreadRead.CodexTurn>();
        if (TryGetArray(page, "data") is { } data)
        {
            foreach (var item in data.EnumerateArray())
            {
                var turn = JKToolKit.CodexSDK.AppServer.ThreadRead.CodexTurn.TryParse(item);
                if (turn is not null)
                {
                    turns.Add(turn);
                }
            }
        }

        return new CodexTurnsPage
        {
            Data = turns,
            NextCursor = GetStringOrNull(page, "nextCursor") ?? GetStringOrNull(page, "next_cursor"),
            BackwardsCursor = GetStringOrNull(page, "backwardsCursor") ?? GetStringOrNull(page, "backwards_cursor"),
            Raw = page.Clone()
        };
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

    private static ActivePermissionProfileInfo? ParseActivePermissionProfile(JsonElement result)
    {
        if (TryGetObject(result, "activePermissionProfile") is not { } profile)
        {
            return null;
        }

        var id = GetStringOrNull(profile, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return new ActivePermissionProfileInfo
        {
            Id = id,
            Extends = GetStringOrNull(profile, "extends"),
            Raw = profile.Clone()
        };
    }
}
