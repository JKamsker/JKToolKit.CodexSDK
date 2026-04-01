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

        EnsureObjectResponse(result, "thread/shellCommand response");
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
        var gitInfoPatch = BuildGitInfoPatch(options.GitInfo);

        var result = await _core.SendRequestAsync(
            "thread/metadata/update",
            new
            {
                threadId = options.ThreadId,
                gitInfo = gitInfoPatch
            },
            ct).ConfigureAwait(false);

        return new ThreadMetadataUpdateResult
        {
            Thread = Internal.CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, options.ThreadId),
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
            throw new ArgumentException("GitInfo is required.", nameof(gitInfo));
        }

        var patch = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (gitInfo.UpdateBranch)
        {
            ValidateGitInfoValue(gitInfo.Branch, nameof(ThreadGitInfoUpdate.Branch));
            patch["branch"] = gitInfo.Branch;
        }

        if (gitInfo.UpdateOriginUrl)
        {
            ValidateGitInfoValue(gitInfo.OriginUrl, nameof(ThreadGitInfoUpdate.OriginUrl));
            patch["originUrl"] = gitInfo.OriginUrl;
        }

        if (gitInfo.UpdateSha)
        {
            ValidateGitInfoValue(gitInfo.Sha, nameof(ThreadGitInfoUpdate.Sha));
            patch["sha"] = gitInfo.Sha;
        }

        if (patch.Count == 0)
        {
            throw new ArgumentException("At least one GitInfo update flag must be set.", nameof(gitInfo));
        }

        return patch;
    }

    private static void ValidateGitInfoValue(string? value, string propertyName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} cannot be empty or whitespace when included in the patch.", propertyName);
        }
    }

    private static IReadOnlyDictionary<string, bool> ParseFeatureEnablement(JsonElement result)
    {
        EnsureObjectResponse(result, "experimentalFeature/enablement/set response");
        if (!result.TryGetProperty("enablement", out var enablement) ||
            enablement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Missing required property 'enablement' on experimentalFeature/enablement/set response.");
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
            else
            {
                throw new InvalidOperationException("experimentalFeature/enablement/set response contains a non-boolean enablement value.");
            }
        }

        return values;
    }

    private static void EnsureObjectResponse(JsonElement result, string context)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{context} must be a JSON object.");
        }
    }
}
