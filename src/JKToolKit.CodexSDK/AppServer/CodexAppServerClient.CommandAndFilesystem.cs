using System.Text.Json;

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
    /// Runs a shell command string in the thread's configured shell.
    /// </summary>
    public async Task<ThreadShellCommandResult> ThreadShellCommandAsync(ThreadShellCommandOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Command))
            throw new ArgumentException("Command cannot be empty or whitespace.", nameof(options));

        var result = await _core.SendRequestAsync(
            "thread/shellCommand",
            new
            {
                threadId = options.ThreadId,
                command = options.Command
            },
            ct).ConfigureAwait(false);

        return new ThreadShellCommandResult
        {
            Raw = result
        };
    }

    /// <summary>
    /// Updates persisted thread metadata.
    /// </summary>
    public async Task<ThreadMetadataUpdateResult> UpdateThreadMetadataAsync(ThreadMetadataUpdateOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));

        var result = await _core.SendRequestAsync(
            "thread/metadata/update",
            new
            {
                threadId = options.ThreadId,
                gitInfo = BuildGitInfoPatch(options.GitInfo)
            },
            ct).ConfigureAwait(false);

        return new ThreadMetadataUpdateResult
        {
            Thread = new CodexThread(ExtractThreadId(result) ?? options.ThreadId, result),
            Raw = result
        };
    }

    /// <summary>
    /// Updates process-wide experimental feature enablement.
    /// </summary>
    public async Task<ExperimentalFeatureEnablementSetResult> SetExperimentalFeatureEnablementAsync(
        ExperimentalFeatureEnablementSetOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Enablement);

        if (!ExperimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("experimentalFeature/enablement/set");
        }

        var result = await _core.SendRequestAsync(
            "experimentalFeature/enablement/set",
            new
            {
                enablement = options.Enablement
            },
            ct).ConfigureAwait(false);

        return new ExperimentalFeatureEnablementSetResult
        {
            Enablement = ParseFeatureEnablement(result),
            Raw = result
        };
    }

    /// <summary>
    /// Reads a file from the host filesystem.
    /// </summary>
    public Task<FsReadFileResult> FsReadFileAsync(FsReadFileOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsReadFileAsync(options, ct);

    /// <summary>
    /// Writes a file on the host filesystem.
    /// </summary>
    public Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsWriteFileAsync(options, ct);

    /// <summary>
    /// Creates a directory on the host filesystem.
    /// </summary>
    public Task<FsCreateDirectoryResult> FsCreateDirectoryAsync(FsCreateDirectoryOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsCreateDirectoryAsync(options, ct);

    /// <summary>
    /// Reads metadata for a host filesystem path.
    /// </summary>
    public Task<FsGetMetadataResult> FsGetMetadataAsync(FsGetMetadataOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsGetMetadataAsync(options, ct);

    /// <summary>
    /// Lists entries in a host filesystem directory.
    /// </summary>
    public Task<FsReadDirectoryResult> FsReadDirectoryAsync(FsReadDirectoryOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsReadDirectoryAsync(options, ct);

    /// <summary>
    /// Removes a file or directory tree from the host filesystem.
    /// </summary>
    public Task<FsRemoveResult> FsRemoveAsync(FsRemoveOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsRemoveAsync(options, ct);

    /// <summary>
    /// Copies a file or directory tree on the host filesystem.
    /// </summary>
    public Task<FsCopyResult> FsCopyAsync(FsCopyOptions options, CancellationToken ct = default) =>
        _filesystemClient.FsCopyAsync(options, ct);

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

    private static object? BuildGitInfoPatch(ThreadGitInfoUpdate? gitInfo)
    {
        if (gitInfo is null)
        {
            return null;
        }

        var patch = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (gitInfo.UpdateBranch)
        {
            patch["branch"] = gitInfo.Branch;
        }

        if (gitInfo.UpdateOriginUrl)
        {
            patch["originUrl"] = gitInfo.OriginUrl;
        }

        if (gitInfo.UpdateSha)
        {
            patch["sha"] = gitInfo.Sha;
        }

        return patch;
    }

    private static IReadOnlyDictionary<string, bool> ParseFeatureEnablement(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("enablement", out var enablement) ||
            enablement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }

        var values = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var property in enablement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.True)
            {
                values[property.Name] = true;
            }
            else if (property.Value.ValueKind == JsonValueKind.False)
            {
                values[property.Name] = false;
            }
        }

        return values;
    }
}
