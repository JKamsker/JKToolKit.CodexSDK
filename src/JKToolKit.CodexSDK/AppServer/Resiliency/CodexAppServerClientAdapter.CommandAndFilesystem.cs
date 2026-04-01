namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct);

    Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct);

    Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct);

    Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct);

    Task<ThreadShellCommandResult> ThreadShellCommandAsync(ThreadShellCommandOptions options, CancellationToken ct);

    Task<ThreadMetadataUpdateResult> UpdateThreadMetadataAsync(ThreadMetadataUpdateOptions options, CancellationToken ct);

    Task<ExperimentalFeatureEnablementSetResult> SetExperimentalFeatureEnablementAsync(ExperimentalFeatureEnablementSetOptions options, CancellationToken ct);

    Task<FsReadFileResult> FsReadFileAsync(FsReadFileOptions options, CancellationToken ct);

    Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct);

    Task<FsCreateDirectoryResult> FsCreateDirectoryAsync(FsCreateDirectoryOptions options, CancellationToken ct);

    Task<FsGetMetadataResult> FsGetMetadataAsync(FsGetMetadataOptions options, CancellationToken ct);

    Task<FsReadDirectoryResult> FsReadDirectoryAsync(FsReadDirectoryOptions options, CancellationToken ct);

    Task<FsRemoveResult> FsRemoveAsync(FsRemoveOptions options, CancellationToken ct);

    Task<FsCopyResult> FsCopyAsync(FsCopyOptions options, CancellationToken ct);

    Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct);

    Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct) => _inner.CommandExecAsync(options, ct);

    public Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct) => _inner.CommandExecWriteAsync(options, ct);

    public Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct) => _inner.CommandExecResizeAsync(options, ct);

    public Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct) => _inner.CommandExecTerminateAsync(options, ct);

    public Task<ThreadShellCommandResult> ThreadShellCommandAsync(ThreadShellCommandOptions options, CancellationToken ct) => _inner.ThreadShellCommandAsync(options, ct);

    public Task<ThreadMetadataUpdateResult> UpdateThreadMetadataAsync(ThreadMetadataUpdateOptions options, CancellationToken ct) => _inner.UpdateThreadMetadataAsync(options, ct);

    public Task<ExperimentalFeatureEnablementSetResult> SetExperimentalFeatureEnablementAsync(ExperimentalFeatureEnablementSetOptions options, CancellationToken ct) => _inner.SetExperimentalFeatureEnablementAsync(options, ct);

    public Task<FsReadFileResult> FsReadFileAsync(FsReadFileOptions options, CancellationToken ct) => _inner.FsReadFileAsync(options, ct);

    public Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct) => _inner.FsWriteFileAsync(options, ct);

    public Task<FsCreateDirectoryResult> FsCreateDirectoryAsync(FsCreateDirectoryOptions options, CancellationToken ct) => _inner.FsCreateDirectoryAsync(options, ct);

    public Task<FsGetMetadataResult> FsGetMetadataAsync(FsGetMetadataOptions options, CancellationToken ct) => _inner.FsGetMetadataAsync(options, ct);

    public Task<FsReadDirectoryResult> FsReadDirectoryAsync(FsReadDirectoryOptions options, CancellationToken ct) => _inner.FsReadDirectoryAsync(options, ct);

    public Task<FsRemoveResult> FsRemoveAsync(FsRemoveOptions options, CancellationToken ct) => _inner.FsRemoveAsync(options, ct);

    public Task<FsCopyResult> FsCopyAsync(FsCopyOptions options, CancellationToken ct) => _inner.FsCopyAsync(options, ct);

    public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct) => _inner.FsWatchAsync(options, ct);

    public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct) => _inner.FsUnwatchAsync(options, ct);
}
