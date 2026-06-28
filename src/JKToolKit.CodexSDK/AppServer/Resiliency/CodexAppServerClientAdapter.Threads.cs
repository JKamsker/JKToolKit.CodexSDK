namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct);

    Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct);

    Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct);

    Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct);

    Task<ThreadSearchPage> SearchThreadsAsync(ThreadSearchOptions options, CancellationToken ct);

    Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct);

    Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct);

    Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct);

    Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct);

    Task CompactThreadAsync(string threadId, CancellationToken ct);

    Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct);

    Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct);

    Task<ThreadBackgroundTerminalListPage> ListThreadBackgroundTerminalsAsync(
        ThreadBackgroundTerminalListOptions options,
        CancellationToken ct);

    Task<ThreadBackgroundTerminalTerminateResult> TerminateThreadBackgroundTerminalAsync(
        ThreadBackgroundTerminalTerminateOptions options,
        CancellationToken ct);

    Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct);

    Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct);

    Task<ThreadDeleteResult> DeleteThreadAsync(string threadId, CancellationToken ct);

    Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct);

    Task SetThreadNameAsync(string threadId, string name, CancellationToken ct);

    Task<PermissionProfileListPage> ListPermissionProfilesAsync(PermissionProfileListOptions options, CancellationToken ct);

    Task<ThreadSettingsUpdateResult> UpdateThreadSettingsAsync(ThreadSettingsUpdateOptions options, CancellationToken ct);

    Task<ThreadGoalResult> SetThreadGoalAsync(ThreadGoalSetOptions options, CancellationToken ct);

    Task<ThreadGoalResult> GetThreadGoalAsync(string threadId, CancellationToken ct);

    Task<ThreadGoalClearResult> ClearThreadGoalAsync(string threadId, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct) => _inner.StartThreadAsync(options, ct);

    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct) => _inner.ResumeThreadAsync(threadId, ct);

    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct) => _inner.ResumeThreadAsync(options, ct);

    public Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct) => _inner.ListThreadsAsync(options, ct);

    public Task<ThreadSearchPage> SearchThreadsAsync(ThreadSearchOptions options, CancellationToken ct) => _inner.SearchThreadsAsync(options, ct);

    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct) => _inner.ReadThreadAsync(threadId, ct);

    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct) => _inner.ReadThreadAsync(threadId, options, ct);

    public Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct) => _inner.ListLoadedThreadsAsync(options, ct);

    public Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct) => _inner.UnsubscribeThreadAsync(threadId, ct);

    public Task CompactThreadAsync(string threadId, CancellationToken ct) => _inner.CompactThreadAsync(threadId, ct);

    public Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct) => _inner.RollbackThreadAsync(threadId, numTurns, ct);

    public Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct) => _inner.CleanThreadBackgroundTerminalsAsync(threadId, ct);

    public Task<ThreadBackgroundTerminalListPage> ListThreadBackgroundTerminalsAsync(
        ThreadBackgroundTerminalListOptions options,
        CancellationToken ct) => _inner.ListThreadBackgroundTerminalsAsync(options, ct);

    public Task<ThreadBackgroundTerminalTerminateResult> TerminateThreadBackgroundTerminalAsync(
        ThreadBackgroundTerminalTerminateOptions options,
        CancellationToken ct) => _inner.TerminateThreadBackgroundTerminalAsync(options, ct);

    public Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct) => _inner.ForkThreadAsync(options, ct);

    public Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct) => _inner.ArchiveThreadAsync(threadId, ct);

    public Task<ThreadDeleteResult> DeleteThreadAsync(string threadId, CancellationToken ct) => _inner.DeleteThreadAsync(threadId, ct);

    public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct) => _inner.UnarchiveThreadAsync(threadId, ct);

    public Task SetThreadNameAsync(string threadId, string name, CancellationToken ct) => _inner.SetThreadNameAsync(threadId, name, ct);

    public Task<PermissionProfileListPage> ListPermissionProfilesAsync(PermissionProfileListOptions options, CancellationToken ct) => _inner.ListPermissionProfilesAsync(options, ct);

    public Task<ThreadSettingsUpdateResult> UpdateThreadSettingsAsync(ThreadSettingsUpdateOptions options, CancellationToken ct) => _inner.UpdateThreadSettingsAsync(options, ct);

    public Task<ThreadGoalResult> SetThreadGoalAsync(ThreadGoalSetOptions options, CancellationToken ct) => _inner.SetThreadGoalAsync(options, ct);

    public Task<ThreadGoalResult> GetThreadGoalAsync(string threadId, CancellationToken ct) => _inner.GetThreadGoalAsync(threadId, ct);

    public Task<ThreadGoalClearResult> ClearThreadGoalAsync(string threadId, CancellationToken ct) => _inner.ClearThreadGoalAsync(threadId, ct);
}
