namespace JKToolKit.CodexSDK.AppServer.Resiliency;

internal partial interface ICodexAppServerClientAdapter
{
    Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct);

    Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct);

    Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct);

    Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct);

    Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct);

    Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct);
}

internal sealed partial class CodexAppServerClientAdapter
{
    public Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct) => _inner.CommandExecAsync(options, ct);

    public Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct) => _inner.CommandExecWriteAsync(options, ct);

    public Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct) => _inner.CommandExecResizeAsync(options, ct);

    public Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct) => _inner.CommandExecTerminateAsync(options, ct);

    public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct) => _inner.FsWatchAsync(options, ct);

    public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct) => _inner.FsUnwatchAsync(options, ct);
}
