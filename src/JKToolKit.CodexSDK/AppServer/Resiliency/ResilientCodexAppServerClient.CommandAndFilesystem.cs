#pragma warning disable CS1591

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

public sealed partial class ResilientCodexAppServerClient
{
    public Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.CommandExecution, (c, token) => c.CommandExecAsync(options, token), ct);

    public Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.CommandExecution, (c, token) => c.CommandExecWriteAsync(options, token), ct);

    public Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.CommandExecution, (c, token) => c.CommandExecResizeAsync(options, token), ct);

    public Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.CommandExecution, (c, token) => c.CommandExecTerminateAsync(options, token), ct);

    public Task<ThreadShellCommandResult> ThreadShellCommandAsync(ThreadShellCommandOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.ThreadShellCommandAsync(options, token), ct);

    public Task<ThreadMetadataUpdateResult> UpdateThreadMetadataAsync(ThreadMetadataUpdateOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.ThreadManagement, (c, token) => c.UpdateThreadMetadataAsync(options, token), ct);

    public Task<FsReadFileResult> FsReadFileAsync(FsReadFileOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsReadFileAsync(options, token), ct);

    public Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsWriteFileAsync(options, token), ct);

    public Task<FsCreateDirectoryResult> FsCreateDirectoryAsync(FsCreateDirectoryOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsCreateDirectoryAsync(options, token), ct);

    public Task<FsGetMetadataResult> FsGetMetadataAsync(FsGetMetadataOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsGetMetadataAsync(options, token), ct);

    public Task<FsReadDirectoryResult> FsReadDirectoryAsync(FsReadDirectoryOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsReadDirectoryAsync(options, token), ct);

    public Task<FsRemoveResult> FsRemoveAsync(FsRemoveOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsRemoveAsync(options, token), ct);

    public Task<FsCopyResult> FsCopyAsync(FsCopyOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsCopyAsync(options, token), ct);

    public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsWatchAsync(options, token), ct);

    public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsUnwatchAsync(options, token), ct);
}

#pragma warning restore CS1591
