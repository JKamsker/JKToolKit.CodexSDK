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

    public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.UnarchiveThreadAsync(threadId, token), ct);

    public Task SetThreadNameAsync(string threadId, string name, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.SetThreadNameAsync(threadId, name, token), ct);
}

#pragma warning restore CS1591
