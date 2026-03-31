using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;
using UpstreamV2 = JKToolKit.CodexSDK.Generated.Upstream.AppServer.V2;

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
                ServiceTier = CodexAppServerWireBuilders.BuildServiceTier(
                    options.ServiceTier,
                    options.ClearServiceTier,
                    nameof(ThreadStartOptions.ClearServiceTier)),
                ServiceName = options.ServiceName,
                ApprovalPolicy = CodexAppServerAskForApprovalWiring.BuildAskForApproval(options.AskForApproval, options.ApprovalPolicy),
                ApprovalsReviewer = options.ApprovalsReviewer,
                Sandbox = options.Sandbox?.ToAppServerWireValue(),
                Config = options.Config,
                BaseInstructions = options.BaseInstructions,
                DeveloperInstructions = options.DeveloperInstructions,
                Personality = options.Personality,
                Ephemeral = options.Ephemeral,
                ExperimentalRawEvents = options.ExperimentalRawEvents,
                DynamicTools = options.DynamicTools,
                PersistExtendedHistory = options.PersistExtendedHistory
            },
            ct);

        var threadId = CodexAppServerClientJson.ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/start returned no thread id. Raw result: {result}");
        }

        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, threadId);
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
        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, id);
    }

    public async Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var hasHistory = HasNonEmptyHistory(options.History);
        if (!hasHistory &&
            string.IsNullOrWhiteSpace(options.Path) &&
            string.IsNullOrWhiteSpace(options.ThreadId))
        {
            throw new ArgumentException("Either ThreadId, History, or Path must be specified.", nameof(options));
        }

        ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: _experimentalApiEnabled());

        var history = hasHistory ? options.History : null;
        var result = await _sendRequestAsync(
            "thread/resume",
            new ThreadResumeParams
            {
                ThreadId = CodexAppServerWireBuilders.BuildThreadIdOrPlaceholder(options.ThreadId),
                History = history,
                Path = options.Path,
                Model = options.Model?.Value,
                ModelProvider = options.ModelProvider,
                Cwd = options.Cwd,
                ServiceTier = CodexAppServerWireBuilders.BuildServiceTier(
                    options.ServiceTier,
                    options.ClearServiceTier,
                    nameof(ThreadResumeOptions.ClearServiceTier)),
                ApprovalPolicy = CodexAppServerAskForApprovalWiring.BuildAskForApproval(options.AskForApproval, options.ApprovalPolicy),
                ApprovalsReviewer = options.ApprovalsReviewer,
                Sandbox = options.Sandbox?.ToAppServerWireValue(),
                Config = options.Config,
                BaseInstructions = options.BaseInstructions,
                DeveloperInstructions = options.DeveloperInstructions,
                Personality = options.Personality,
                PersistExtendedHistory = options.PersistExtendedHistory
            },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? options.ThreadId;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException(
                $"thread/resume returned no thread id. Raw result: {result}");
        }

        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, id);
    }

    private static bool HasNonEmptyHistory(JsonElement? history)
    {
        if (history is not { } h || h.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (h.ValueKind == JsonValueKind.Array)
        {
            var e = h.EnumerateArray();
            return e.MoveNext();
        }

        return true;
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
                Limit = options.Limit,
                ModelProviders = options.ModelProviders,
                SearchTerm = options.SearchTerm,
                SourceKinds = options.SourceKinds,
                Cursor = options.Cursor,
                SortKey = options.SortKey,
            },
            ct);

        return new CodexThreadListPage
        {
            Threads = CodexAppServerClientThreadParsers.ParseThreadListThreads(result),
            NextCursor = CodexAppServerClientThreadParsers.ExtractNextCursor(result),
            Raw = result
        };
    }

    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default) =>
        ReadThreadAsync(threadId, new ThreadReadOptions(), ct);

    public async Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "thread/read",
            new ThreadReadParams { ThreadId = threadId, IncludeTurns = options.IncludeTurns },
            ct);

        return CodexAppServerClientThreadResponseParsers.ParseReadResult(result, threadId);
    }

    public async Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _sendRequestAsync(
            "thread/loaded/list",
            new UpstreamV2.ThreadLoadedListParams
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

    public async Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/unsubscribe",
            new UpstreamV2.ThreadUnsubscribeParams { ThreadId = threadId },
            ct);

        var status = ParseThreadUnsubscribeStatus(CodexAppServerClientJson.GetStringOrNull(result, "status"));

        return new ThreadUnsubscribeResult
        {
            Status = status,
            Raw = result
        };
    }

    public async Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be empty or whitespace.", nameof(prompt));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/realtime/start");
        }

        _ = await _sendRequestAsync(
            "thread/realtime/start",
            new ThreadRealtimeStartParams
            {
                ThreadId = threadId,
                Prompt = prompt,
                SessionId = sessionId
            },
            ct);
    }

    public async Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        ArgumentNullException.ThrowIfNull(audio);

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/realtime/appendAudio");
        }

        _ = await _sendRequestAsync(
            "thread/realtime/appendAudio",
            new ThreadRealtimeAppendAudioParams
            {
                ThreadId = threadId,
                Audio = audio
            },
            ct);
    }

    public async Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty or whitespace.", nameof(text));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/realtime/appendText");
        }

        _ = await _sendRequestAsync(
            "thread/realtime/appendText",
            new ThreadRealtimeAppendTextParams
            {
                ThreadId = threadId,
                Text = text
            },
            ct);
    }

    public async Task StopThreadRealtimeAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        if (!_experimentalApiEnabled())
        {
            throw new CodexExperimentalApiRequiredException("thread/realtime/stop");
        }

        _ = await _sendRequestAsync(
            "thread/realtime/stop",
            new ThreadRealtimeStopParams { ThreadId = threadId },
            ct);
    }

    private static ThreadUnsubscribeStatus ParseThreadUnsubscribeStatus(string? value) =>
        value switch
        {
            "notLoaded" => ThreadUnsubscribeStatus.NotLoaded,
            "notSubscribed" => ThreadUnsubscribeStatus.NotSubscribed,
            "unsubscribed" => ThreadUnsubscribeStatus.Unsubscribed,
            _ => ThreadUnsubscribeStatus.Unknown
        };

    public async Task CompactThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        _ = await _sendRequestAsync(
            "thread/compact/start",
            new UpstreamV2.ThreadCompactStartParams { ThreadId = threadId },
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
            new UpstreamV2.ThreadRollbackParams
            {
                ThreadId = threadId,
                NumTurns = numTurns
            },
            ct);

        var threadObj = CodexAppServerClientJson.TryGetObject(result, "thread") ?? result;
        var id = CodexAppServerClientJson.ExtractThreadId(threadObj) ?? threadId;
        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, id);
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
                ThreadId = string.IsNullOrWhiteSpace(options.Path)
                    ? CodexAppServerWireBuilders.BuildThreadIdOrPlaceholder(options.ThreadId)
                    : string.Empty,
                Path = options.Path,
                ServiceTier = CodexAppServerWireBuilders.BuildServiceTier(
                    options.ServiceTier,
                    options.ClearServiceTier,
                    nameof(ThreadForkOptions.ClearServiceTier)),
                Model = options.Model?.Value,
                ModelProvider = options.ModelProvider,
                Cwd = options.Cwd,
                ApprovalPolicy = CodexAppServerAskForApprovalWiring.BuildAskForApproval(options.AskForApproval, options.ApprovalPolicy),
                ApprovalsReviewer = options.ApprovalsReviewer,
                Sandbox = options.Sandbox?.ToAppServerWireValue(),
                Config = options.Config,
                BaseInstructions = options.BaseInstructions,
                DeveloperInstructions = options.DeveloperInstructions,
                Ephemeral = options.Ephemeral,
                PersistExtendedHistory = options.PersistExtendedHistory
            },
            ct);

        var threadId = CodexAppServerClientJson.ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/fork returned no thread id. Raw result: {result}");
        }

        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, threadId);
    }

    public async Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/archive",
            new UpstreamV2.ThreadArchiveParams { ThreadId = threadId },
            ct);

        return new ThreadArchiveResult
        {
            Raw = result
        };
    }

    public async Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _sendRequestAsync(
            "thread/unarchive",
            new UpstreamV2.ThreadUnarchiveParams { ThreadId = threadId },
            ct);

        var id = CodexAppServerClientJson.ExtractThreadId(result) ?? threadId;
        return CodexAppServerClientThreadResponseParsers.ParseLifecycleThread(result, id);
    }

    public async Task SetThreadNameAsync(string threadId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty or whitespace.", nameof(name));

        _ = await _sendRequestAsync(
            "thread/name/set",
            new UpstreamV2.ThreadSetNameParams { ThreadId = threadId, Name = name },
            ct);
    }
}
