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
            DataBase64 = CodexAppServerClientJson.GetStringOrNull(result, "dataBase64")
                ?? CodexAppServerClientJson.GetStringOrNull(result, "data")
                ?? string.Empty,
            Raw = result
        };
    }

    public async Task<FsWriteFileResult> FsWriteFileAsync(FsWriteFileOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidatePath(options.Path, nameof(options));
        if (string.IsNullOrWhiteSpace(options.DataBase64))
            throw new ArgumentException("DataBase64 cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "fs/writeFile",
            new UpstreamV2.FsWriteFileParams
            {
                Path = options.Path,
                DataBase64 = options.DataBase64
            },
            ct).ConfigureAwait(false);

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
            IsDirectory = GetBooleanLike(result, "isDirectory"),
            IsFile = GetBooleanLike(result, "isFile"),
            CreatedAtMs = GetInt64OrDefault(result, "createdAtMs"),
            ModifiedAtMs = GetInt64OrDefault(result, "modifiedAtMs"),
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
            Path = CodexAppServerClientJson.GetStringOrNull(result, "path") ?? options.Path,
            WatchId = CodexAppServerClientJson.GetStringOrNull(result, "watchId")
                ?? throw new InvalidOperationException("fs/watch returned no watchId."),
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

        return new FsUnwatchResult { Raw = result };
    }

    private static void ValidatePath(string? path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty or whitespace.", paramName);
        }
    }

    private static bool GetBooleanLike(JsonElement obj, string propertyName)
    {
        var value = CodexAppServerClientJson.GetBoolOrNull(obj, propertyName);
        if (value.HasValue)
        {
            return value.Value;
        }

        var stringValue = CodexAppServerClientJson.GetStringOrNull(obj, propertyName);
        return bool.TryParse(stringValue, out var parsed) && parsed;
    }

    private static long GetInt64OrDefault(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var property))
        {
            return default;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out number))
        {
            return number;
        }

        return default;
    }

    private static IReadOnlyList<FsDirectoryEntry> ParseDirectoryEntries(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FsDirectoryEntry>();
        }

        var parsed = new List<FsDirectoryEntry>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            parsed.Add(new FsDirectoryEntry
            {
                FileName = CodexAppServerClientJson.GetStringOrNull(entry, "fileName")
                    ?? CodexAppServerClientJson.GetStringOrNull(entry, "file_name")
                    ?? string.Empty,
                IsDirectory = GetBooleanLike(entry, "isDirectory"),
                IsFile = GetBooleanLike(entry, "isFile"),
                Raw = entry.Clone()
            });
        }

        return parsed;
    }
}
