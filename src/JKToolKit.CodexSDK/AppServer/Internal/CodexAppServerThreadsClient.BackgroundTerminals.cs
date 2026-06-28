using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed partial class CodexAppServerThreadsClient
{
    public async Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/backgroundTerminals/clean");
        }

        _ = await _sendRequestAsync(
            "thread/backgroundTerminals/clean",
            new ThreadBackgroundTerminalsCleanParams { ThreadId = threadId },
            ct);
    }

    public async Task<ThreadBackgroundTerminalListPage> ListThreadBackgroundTerminalsAsync(
        ThreadBackgroundTerminalListOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/backgroundTerminals/list");
        }

        var result = await _sendRequestAsync(
            "thread/backgroundTerminals/list",
            new
            {
                options.ThreadId,
                options.Cursor,
                options.Limit
            },
            ct);

        return new ThreadBackgroundTerminalListPage
        {
            Data = ParseBackgroundTerminals(result),
            NextCursor = CodexAppServerClientJson.GetStringOrNull(result, "nextCursor"),
            Raw = result
        };
    }

    public async Task<ThreadBackgroundTerminalTerminateResult> TerminateThreadBackgroundTerminalAsync(
        ThreadBackgroundTerminalTerminateOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ProcessId))
            throw new ArgumentException("ProcessId cannot be empty or whitespace.", nameof(options));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/backgroundTerminals/terminate");
        }

        var result = await _sendRequestAsync(
            "thread/backgroundTerminals/terminate",
            new
            {
                options.ThreadId,
                options.ProcessId
            },
            ct);

        return new ThreadBackgroundTerminalTerminateResult
        {
            Terminated = CodexAppServerClientJson.GetRequiredBool(
                result,
                "terminated",
                "thread/backgroundTerminals/terminate response"),
            Raw = result
        };
    }

    private static IReadOnlyList<ThreadBackgroundTerminalInfo> ParseBackgroundTerminals(JsonElement result)
    {
        var data = CodexAppServerClientJson.TryGetArray(result, "data")
            ?? throw new InvalidOperationException("thread/backgroundTerminals/list response missing required array property 'data'.");

        var terminals = new List<ThreadBackgroundTerminalInfo>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("thread/backgroundTerminals/list data entries must be objects.");
            }

            terminals.Add(new ThreadBackgroundTerminalInfo
            {
                ItemId = CodexAppServerClientJson.GetRequiredString(item, "itemId", "thread/backgroundTerminals/list data[]"),
                ProcessId = CodexAppServerClientJson.GetRequiredString(item, "processId", "thread/backgroundTerminals/list data[]"),
                Command = CodexAppServerClientJson.GetRequiredString(item, "command", "thread/backgroundTerminals/list data[]"),
                Cwd = CodexAppServerClientJson.GetRequiredString(item, "cwd", "thread/backgroundTerminals/list data[]"),
                OsPid = CodexAppServerClientJson.GetInt64OrNull(item, "osPid"),
                CpuPercent = CodexAppServerClientJson.GetDoubleOrNull(item, "cpuPercent"),
                RssKb = CodexAppServerClientJson.GetInt64OrNull(item, "rssKb"),
                Raw = item.Clone()
            });
        }

        return terminals;
    }
}
