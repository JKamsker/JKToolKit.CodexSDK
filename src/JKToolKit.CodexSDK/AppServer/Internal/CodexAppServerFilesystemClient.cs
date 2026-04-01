using System.IO;
using System.Text.Json;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerFilesystemClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerFilesystemClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task<FsReadFileResult> FsReadFileAsync(FsReadFileOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/readFile",
            new UpstreamV2.FsReadFileParams
            {
                Path = options.Path
            },
            ct).ConfigureAwait(false);

        return new FsReadFileResult
        {
            DataBase64 = CodexAppServerClientJson.GetRequiredString(result, "dataBase64", "fs/readFile response"),
            Raw = result
        };
    }

    public async Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));
        if (options.DataBase64 is null)
            throw new ArgumentException("DataBase64 cannot be null.", nameof(options));

        var result = await _sendRequestAsync(
            "fs/writeFile",
            new UpstreamV2.FsWriteFileParams
            {
                Path = options.Path,
                DataBase64 = options.DataBase64
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "fs/writeFile response");
        return new FsWriteFileResult { Raw = result };
    }

    public async Task<FsCreateDirectoryResult> FsCreateDirectoryAsync(FsCreateDirectoryOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/createDirectory",
            new UpstreamV2.FsCreateDirectoryParams
            {
                Path = options.Path,
                Recursive = options.Recursive
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "fs/createDirectory response");
        return new FsCreateDirectoryResult { Raw = result };
    }

    public async Task<FsGetMetadataResult> FsGetMetadataAsync(FsGetMetadataOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/getMetadata",
            new UpstreamV2.FsGetMetadataParams
            {
                Path = options.Path
            },
            ct).ConfigureAwait(false);

        return new FsGetMetadataResult
        {
            IsDirectory = CodexAppServerClientJson.GetRequiredBool(result, "isDirectory", "fs/getMetadata response"),
            IsFile = CodexAppServerClientJson.GetRequiredBool(result, "isFile", "fs/getMetadata response"),
            CreatedAtMs = CodexAppServerClientJson.GetRequiredInt64(result, "createdAtMs", "fs/getMetadata response"),
            ModifiedAtMs = CodexAppServerClientJson.GetRequiredInt64(result, "modifiedAtMs", "fs/getMetadata response"),
            Raw = result
        };
    }

    public async Task<FsReadDirectoryResult> FsReadDirectoryAsync(FsReadDirectoryOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/readDirectory",
            new UpstreamV2.FsReadDirectoryParams
            {
                Path = options.Path
            },
            ct).ConfigureAwait(false);

        return new FsReadDirectoryResult
        {
            Entries = ParseDirectoryEntries(result),
            Raw = result
        };
    }

    public async Task<FsRemoveResult> FsRemoveAsync(FsRemoveOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/remove",
            new UpstreamV2.FsRemoveParams
            {
                Path = options.Path,
                Recursive = options.Recursive,
                Force = options.Force
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "fs/remove response");
        return new FsRemoveResult { Raw = result };
    }

    public async Task<FsCopyResult> FsCopyAsync(FsCopyOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.SourcePath, nameof(options));
        ValidatePath(options.DestinationPath, nameof(options));

        var result = await _sendRequestAsync(
            "fs/copy",
            new UpstreamV2.FsCopyParams
            {
                SourcePath = options.SourcePath,
                DestinationPath = options.DestinationPath,
                Recursive = options.Recursive
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "fs/copy response");
        return new FsCopyResult { Raw = result };
    }

    public async Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));

        var result = await _sendRequestAsync(
            "fs/watch",
            new UpstreamV2.FsWatchParams
            {
                Path = options.Path
            },
            ct).ConfigureAwait(false);

        return new FsWatchResult
        {
            Path = GetRequiredResponsePath(result, "fs/watch response"),
            WatchId = CodexAppServerClientJson.GetRequiredString(result, "watchId", "fs/watch response"),
            Raw = result
        };
    }

    public async Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.WatchId))
            throw new ArgumentException("WatchId cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "fs/unwatch",
            new UpstreamV2.FsUnwatchParams
            {
                WatchId = options.WatchId
            },
            ct).ConfigureAwait(false);

        EnsureObjectResponse(result, "fs/unwatch response");
        return new FsUnwatchResult { Raw = result };
    }

    private static void ValidatePath(string? path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty or whitespace.", paramName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Path must be absolute.", paramName);
        }
    }

    private static string GetRequiredResponsePath(JsonElement obj, string context)
    {
        var path = CodexAppServerClientJson.GetRequiredString(obj, "path", context);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"Path returned from {context} must be absolute.");
        }

        return path;
    }

    private static IReadOnlyList<FsDirectoryEntry> ParseDirectoryEntries(JsonElement result)
    {
        var entries = GetRequiredArray(result, "entries", "fs/readDirectory response");

        var parsed = new List<FsDirectoryEntry>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("fs/readDirectory response entries[] must contain only objects.");
            }

            parsed.Add(new FsDirectoryEntry
            {
                FileName = CodexAppServerClientJson.GetRequiredString(entry, "fileName", "fs/readDirectory entry"),
                IsDirectory = CodexAppServerClientJson.GetRequiredBool(entry, "isDirectory", "fs/readDirectory entry"),
                IsFile = CodexAppServerClientJson.GetRequiredBool(entry, "isFile", "fs/readDirectory entry"),
                Raw = entry.Clone()
            });
        }

        return parsed;
    }

    private static void EnsureObjectResponse(JsonElement result, string context)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{context} must be a JSON object.");
        }
    }

    private static JsonElement GetRequiredArray(JsonElement obj, string propertyName, string context)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Missing required array '{propertyName}' on {context}.");
        }

        return array;
    }
}
