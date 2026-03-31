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

    public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsWatchAsync(options, token), ct);

    public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct = default) =>
        ExecuteAsync(CodexAppServerOperationKind.Filesystem, (c, token) => c.FsUnwatchAsync(options, token), ct);
}

#pragma warning restore CS1591
