using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Lists permission profiles available to the current configuration.
    /// </summary>
    public async Task<PermissionProfileListPage> ListPermissionProfilesAsync(
        PermissionProfileListOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _core.SendRequestAsync(
            "permissionProfile/list",
            new { options.Cursor, options.Limit, options.Cwd },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadManagementParsers.ParsePermissionProfiles(result);
    }

    /// <summary>
    /// Searches local conversation history.
    /// </summary>
    public async Task<ThreadSearchPage> SearchThreadsAsync(ThreadSearchOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.SearchTerm))
            throw new ArgumentException("SearchTerm cannot be empty or whitespace.", nameof(options));

        var result = await _core.SendRequestAsync(
            "thread/search",
            new
            {
                options.Cursor,
                options.Limit,
                options.SortKey,
                options.SortDirection,
                options.SourceKinds,
                options.Archived,
                options.SearchTerm
            },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadManagementParsers.ParseThreadSearch(result);
    }

    /// <summary>
    /// Updates settings used by subsequent turns in a thread (experimental).
    /// </summary>
    public async Task<ThreadSettingsUpdateResult> UpdateThreadSettingsAsync(
        ThreadSettingsUpdateOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));
        if (!_core.ExperimentalApiEnabled)
            throw new CodexExperimentalApiRequiredException("thread/settings/update");
        if (options.SandboxPolicy is not null && !string.IsNullOrWhiteSpace(options.PermissionProfileId))
            throw new ArgumentException("SandboxPolicy and PermissionProfileId cannot both be set.", nameof(options));

        var result = await _core.SendRequestAsync(
            "thread/settings/update",
            BuildThreadSettingsUpdateParams(options),
            ct).ConfigureAwait(false);

        return new ThreadSettingsUpdateResult { Raw = result };
    }

    /// <summary>
    /// Sets or updates a thread goal.
    /// </summary>
    public async Task<ThreadGoalResult> SetThreadGoalAsync(ThreadGoalSetOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));
        if (options.Objective is not null && string.IsNullOrWhiteSpace(options.Objective))
            throw new ArgumentException("Objective cannot be empty or whitespace.", nameof(options));

        var result = await _core.SendRequestAsync(
            "thread/goal/set",
            new
            {
                options.ThreadId,
                options.Objective,
                Status = options.Status is { } status
                    ? CodexAppServerThreadManagementParsers.FormatThreadGoalStatus(status)
                    : null,
                options.TokenBudget
            },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadManagementParsers.ParseThreadGoalResult(result);
    }

    /// <summary>
    /// Reads the current thread goal.
    /// </summary>
    public async Task<ThreadGoalResult> GetThreadGoalAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _core.SendRequestAsync(
            "thread/goal/get",
            new { ThreadId = threadId },
            ct).ConfigureAwait(false);

        return CodexAppServerThreadManagementParsers.ParseThreadGoalResult(result);
    }

    /// <summary>
    /// Clears the current thread goal.
    /// </summary>
    public async Task<ThreadGoalClearResult> ClearThreadGoalAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _core.SendRequestAsync(
            "thread/goal/clear",
            new { ThreadId = threadId },
            ct).ConfigureAwait(false);

        return new ThreadGoalClearResult
        {
            Cleared = CodexAppServerClientJson.GetBoolOrNull(result, "cleared") == true,
            Raw = result
        };
    }

    private static object BuildThreadSettingsUpdateParams(ThreadSettingsUpdateOptions options) =>
        new
        {
            options.ThreadId,
            options.Cwd,
            ApprovalPolicy = CodexAppServerAskForApprovalWiring.BuildAskForApproval(
                options.AskForApproval,
                options.ApprovalPolicy),
            options.ApprovalsReviewer,
            options.SandboxPolicy,
            Permissions = options.PermissionProfileId,
            options.Model,
            ServiceTier = CodexAppServerWireBuilders.BuildServiceTier(
                options.ServiceTier,
                options.ClearServiceTier,
                nameof(ThreadSettingsUpdateOptions.ClearServiceTier)),
            Effort = options.Effort?.Value,
            options.Summary,
            CollaborationMode = options.CollaborationMode is { ValueKind: not JsonValueKind.Undefined }
                ? options.CollaborationMode
                : null,
            options.Personality
        };
}
