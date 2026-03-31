namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Runs a standalone command (argv vector) in the app-server sandbox.
    /// </summary>
    public Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct = default) =>
        _commandExecClient.CommandExecAsync(options, ct);

    /// <summary>
    /// Writes stdin bytes to a running <c>command/exec</c> session and/or closes stdin.
    /// </summary>
    public Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct = default) =>
        _commandExecClient.CommandExecWriteAsync(options, ct);

    /// <summary>
    /// Resizes a running PTY-backed <c>command/exec</c> session.
    /// </summary>
    public Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct = default) =>
        _commandExecClient.CommandExecResizeAsync(options, ct);

    /// <summary>
    /// Terminates a running <c>command/exec</c> session.
    /// </summary>
    public Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct = default) =>
        _commandExecClient.CommandExecTerminateAsync(options, ct);

    /// <summary>
    /// Starts filesystem change notifications for an absolute path.
    /// </summary>
    public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsWatchAsync(options, ct);

    /// <summary>
    /// Stops filesystem change notifications for a prior <c>fs/watch</c> subscription.
    /// </summary>
    public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsUnwatchAsync(options, ct);
}
