using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerThreadsClient
{
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<bool> _experimentalApiEnabled;

    public CodexAppServerThreadsClient(
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<bool> experimentalApiEnabled)
    {
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _experimentalApiEnabled = experimentalApiEnabled ?? throw new ArgumentNullException(nameof(experimentalApiEnabled));
    }

    public async Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: _experimentalApiEnabled());

        var result = await _sendRequestAsync(
            "thread/start",
            new ThreadStartParams
            {
                Model = options.Model?.Value,
                ModelProvider = options.ModelProvider,
                Cwd = options.Cwd,
                ApprovalPolicy = options.ApprovalPolicy?.Value,
                Sandbox = options.Sandbox?.ToAppServerWireValue(),
                Config = options.Config,
                BaseInstructions = options.BaseInstructions,
                DeveloperInstructions = options.DeveloperInstructions,
                Personality = options.Personality,
                Ephemeral = options.Ephemeral,
                ExperimentalRawEvents = options.ExperimentalRawEvents
            },
            ct);

        var threadId = CodexAppServerClientJson.ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/start returned no thread id. Raw result: {result}");
        }
        return new CodexThread(threadId, result);
    }

    public async Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/resume",
            new ThreadResumeParams { ThreadId = threadId },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    public async Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.History is null &&
            string.IsNullOrWhiteSpace(options.Path) &&
            string.IsNullOrWhiteSpace(options.ThreadId))
        {
            throw new ArgumentException("Either ThreadId, History, or Path must be specified.", nameof(options));
        }

        ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: _experimentalApiEnabled());

        var result = await _sendRequestAsync(
            "thread/resume",
            new ThreadResumeParams
            {
                ThreadId = options.ThreadId,
                History = options.History,
                Path = options.Path,
                Model = options.Model?.Value,
                ModelProvider = options.ModelProvider,
                Cwd = options.Cwd,
                ApprovalPolicy = options.ApprovalPolicy?.Value,
                Sandbox = options.Sandbox?.ToAppServerWireValue(),
                Config = options.Config,
                BaseInstructions = options.BaseInstructions,
                DeveloperInstructions = options.DeveloperInstructions,
                Personality = options.Personality
            },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? options.ThreadId;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                $"thread/resume returned no thread id. Raw result: {result}");
        }
        return new CodexThread(id, result);
    }

    public async Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "thread/list",
            new ThreadListParams
            {
                Archived = options.Archived,
                Cwd = options.Cwd,
                Query = options.Query,
                PageSize = options.PageSize,
                Cursor = options.Cursor,
                SortKey = options.SortKey,
                SortDirection = options.SortDirection
            },
            ct);

        return new CodexThreadListPage
        {
            Threads = CodexAppServerClientThreadParsers.ParseThreadListThreads(result),
            NextCursor = CodexAppServerClientThreadParsers.ExtractNextCursor(result),
            Raw = result
        };
    }

    public async Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/read",
            new ThreadReadParams { ThreadId = threadId },
            ct);

        var threadObject = CodexAppServerClientJson.TryGetObject(result, "thread") ?? result;
        var summary = CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject) ?? new CodexThreadSummary
        {
            ThreadId = threadId,
            Raw = threadObject
        };

        return new CodexThreadReadResult
        {
            Thread = summary,
            Raw = result
        };
    }

    public async Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "thread/loaded/list",
            new ThreadLoadedListParams
            {
                Cursor = options.Cursor,
                Limit = options.Limit
            },
            ct);

        return new CodexLoadedThreadListPage
        {
            ThreadIds = CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(result),
            NextCursor = CodexAppServerClientThreadParsers.ExtractNextCursor(result),
            Raw = result
        };
    }

    public async Task CompactThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        _ = await _sendRequestAsync(
            "thread/compact/start",
            new ThreadCompactStartParams { ThreadId = threadId },
            ct);
    }

    public async Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        if (numTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(numTurns), numTurns, "NumTurns must be greater than zero.");

        var result = await _sendRequestAsync(
            "thread/rollback",
            new ThreadRollbackParams
            {
                ThreadId = threadId,
                NumTurns = numTurns
            },
            ct);

        var threadObj = CodexAppServerClientJson.TryGetObject(result, "thread") ?? result;
        var id = CodexAppServerClientJson.ExtractThreadId(threadObj) ?? threadId;
        return new CodexThread(id, result);
    }

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

        public async Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);
        ExperimentalApiGuards.ValidateThreadFork(options, _experimentalApiEnabled());

            var result = await _sendRequestAsync(
                "thread/fork",
                new ThreadForkParams
                {
                ThreadId = options.ThreadId,
                Path = options.Path
            },
            ct);

        var threadId = CodexAppServerClientJson.ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/fork returned no thread id. Raw result: {result}");
        }

        return new CodexThread(threadId, result);
    }

    public async Task<CodexThread> ArchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/archive",
            new ThreadArchiveParams { ThreadId = threadId },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    public async Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/unarchive",
            new ThreadUnarchiveParams { ThreadId = threadId },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    public async Task SetThreadNameAsync(string threadId, string? name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        _ = await _sendRequestAsync(
            "thread/name/set",
            new ThreadSetNameParams { ThreadId = threadId, ThreadName = name },
            ct);
    }
}
