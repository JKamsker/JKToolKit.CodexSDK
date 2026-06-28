#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.StartThread, (c, token) => c.StartThreadAsync(options, token), ct);

    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(threadId, token), ct);

    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ResumeThread, (c, token) => c.ResumeThreadAsync(options, token), ct);

    public Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ListThreadsAsync(options, token), ct);

    public Task<ThreadSearchPage> SearchThreadsAsync(ThreadSearchOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.SearchThreadsAsync(options, token), ct);

    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ReadThreadAsync(threadId, token), ct);

    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ReadThreadAsync(threadId, options, token), ct);

    public Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ListLoadedThreadsAsync(options, token), ct);

    public Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.UnsubscribeThreadAsync(threadId, token), ct);

    public Task CompactThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.CompactThreadAsync(threadId, token), ct);

    public Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.RollbackThreadAsync(threadId, numTurns, token), ct);

    public Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.CleanThreadBackgroundTerminalsAsync(threadId, token), ct);

    public Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ForkThreadAsync(options, token), ct);

    public Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ArchiveThreadAsync(threadId, token), ct);

    public Task<ThreadDeleteResult> DeleteThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.DeleteThreadAsync(threadId, token), ct);

    public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.UnarchiveThreadAsync(threadId, token), ct);

    public Task SetThreadNameAsync(string threadId, string name, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.SetThreadNameAsync(threadId, name, token), ct);

    public Task<PermissionProfileListPage> ListPermissionProfilesAsync(PermissionProfileListOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ListPermissionProfilesAsync(options, token), ct);

    public Task<ThreadSettingsUpdateResult> UpdateThreadSettingsAsync(ThreadSettingsUpdateOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.UpdateThreadSettingsAsync(options, token), ct);

    public Task<ThreadGoalResult> SetThreadGoalAsync(ThreadGoalSetOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.SetThreadGoalAsync(options, token), ct);

    public Task<ThreadGoalResult> GetThreadGoalAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.GetThreadGoalAsync(threadId, token), ct);

    public Task<ThreadGoalClearResult> ClearThreadGoalAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ClearThreadGoalAsync(threadId, token), ct);
}

#pragma warning restore CS1591
