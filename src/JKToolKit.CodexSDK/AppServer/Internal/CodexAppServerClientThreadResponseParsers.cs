using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.Json;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

using static CodexAppServerClientJson;

internal static class CodexAppServerClientThreadResponseParsers
{
    public static CodexThread ParseLifecycleThread(JsonElement result, string? fallbackThreadId = null)
    {
        var threadObject = TryGetObject(result, JsonFieldNames.Thread) ?? result;
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject, result) ?? new CodexThreadSummary
        {
            ThreadId = fallbackThreadId ?? ExtractThreadId(result) ?? throw new InvalidOperationException("The response did not contain a thread id."),
            Raw = threadObject
        };

        var approvalPolicy = CodexApprovalPolicy.TryParse(GetStringOrNull(result, JsonFieldNames.ApprovalPolicy), out var parsedApprovalPolicy)
            ? parsedApprovalPolicy
            : (CodexApprovalPolicy?)null;
        var approvalsReviewer = CodexApprovalsReviewerParser.ParseOrNull(GetStringOrNull(result, JsonFieldNames.ApprovalsReviewer));
        var sandbox = CodexSandboxMode.TryParse(GetStringOrNull(result, JsonFieldNames.Sandbox), out var parsedSandbox)
            ? parsedSandbox
            : (CodexSandboxMode?)null;
        var serviceTier = CodexServiceTier.TryParse(GetStringOrNull(result, JsonFieldNames.ServiceTier), out var parsedServiceTier)
            ? parsedServiceTier
            : summary.ServiceTier;
        var approvalPolicyRaw = TryGetElement(result, JsonFieldNames.ApprovalPolicy);
        var sandboxRaw = TryGetElement(result, JsonFieldNames.Sandbox);
        var reasoningEffort = CodexReasoningEffort.TryParse(GetStringOrNull(result, JsonFieldNames.ReasoningEffort), out var parsedReasoningEffort)
            ? parsedReasoningEffort
            : (CodexReasoningEffort?)null;
        var disabledPluginIds = GetOptionalStringArray(result, JsonFieldNames.DisabledPluginIds) ?? Array.Empty<string>();
        var collaborationMode = TryGetObject(result, JsonFieldNames.CollaborationMode)?.Clone();
        var runtimeWorkspaceRoots = GetOptionalStringArray(result, JsonFieldNames.RuntimeWorkspaceRoots) ?? Array.Empty<string>();
        var instructionSources = GetOptionalStringArray(result, "instructionSources") ?? Array.Empty<string>();
        var activePermissionProfile = ParseActivePermissionProfile(result);
        var initialTurnsPage = ParseTurnsPage(result, JsonFieldNames.InitialTurnsPage);
        var turnsBackwardsCursor = GetStringOrNull(result, JsonFieldNames.TurnsBackwardsCursor) ?? GetStringOrNull(result, "turns_backwards_cursor");
        var itemsBackwardsCursor = GetStringOrNull(result, JsonFieldNames.ItemsBackwardsCursor) ?? GetStringOrNull(result, "items_backwards_cursor");

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
            disabledPluginIds,
            collaborationMode,
            runtimeWorkspaceRoots,
            instructionSources,
            activePermissionProfile,
            initialTurnsPage,
            turnsBackwardsCursor,
            itemsBackwardsCursor);
    }

    private static CodexTurnsPage? ParseTurnsPage(JsonElement result, string propertyName)
    {
        if (TryGetObject(result, propertyName) is not { } page)
        {
            return null;
        }

        var turns = new List<JKToolKit.CodexSDK.AppServer.ThreadRead.CodexTurn>();
        if (TryGetArray(page, JsonFieldNames.Data) is { } data)
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
            NextCursor = GetStringOrNull(page, JsonFieldNames.NextCursor) ?? GetStringOrNull(page, JsonFieldNames.SnakeCase.NextCursor),
            BackwardsCursor = GetStringOrNull(page, JsonFieldNames.BackwardsCursor) ?? GetStringOrNull(page, "backwards_cursor"),
            Raw = page.Clone()
        };
    }

    public static CodexThreadReadResult ParseReadResult(JsonElement result, string fallbackThreadId)
    {
        var threadObject = TryGetObject(result, JsonFieldNames.Thread) ?? result;
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

        var id = GetStringOrNull(profile, JsonFieldNames.Id);
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
