using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerFilesystemClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;

    public CodexAppServerFilesystemClient(Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
    }

    public async Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Path))
            throw new ArgumentException("Path cannot be empty or whitespace.", nameof(options));

        var result = await _sendRequestAsync(
            "fs/watch",
            new
            {
                path = options.Path
            },
            ct);

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
            new
            {
                watchId = options.WatchId
            },
            ct);

        return new FsUnwatchResult { Raw = result };
    }
}
