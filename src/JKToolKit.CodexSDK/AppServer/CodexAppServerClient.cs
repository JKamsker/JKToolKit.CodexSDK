using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// A client for interacting with the Codex CLI "app-server" JSON-RPC interface.
/// </summary>
public sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly CodexAppServerClientOptions _options;
    private readonly ILogger _logger;
    private readonly IStdioProcess _process;
    private readonly IJsonRpcConnection _rpc;

    private readonly Channel<AppServerNotification> _globalNotifications;
    private readonly Dictionary<string, CodexTurnHandle> _turnsById = new(StringComparer.Ordinal);
    private int _disposed;
    private int _disconnectSignaled;
    private readonly Task _processExitWatcher;
    private AppServerInitializeResult? _initializeResult;

    private bool ExperimentalApiEnabled => _options.Capabilities?.ExperimentalApi == true;

    internal static InitializeCapabilities? NormalizeCapabilities(InitializeCapabilities? capabilities)
    {
        if (capabilities is null)
        {
            return null;
        }

        var experimentalApi = capabilities.ExperimentalApi;
        var optOut = capabilities.OptOutNotificationMethods;
        var hasOptOut = optOut is { Count: > 0 };

        if (!experimentalApi && !hasOptOut)
        {
            return null;
        }

        return new InitializeCapabilities
        {
            ExperimentalApi = experimentalApi,
            OptOutNotificationMethods = hasOptOut ? optOut : null
        };
    }

    internal static InitializeParams BuildInitializeParams(AppServerClientInfo clientInfo, InitializeCapabilities? capabilities) =>
        new()
        {
            ClientInfo = clientInfo,
            Capabilities = NormalizeCapabilities(capabilities)
        };

    internal CodexAppServerClient(
        CodexAppServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger logger,
        bool startExitWatcher = true)
    {
        _options = options;
        _process = process;
        _rpc = rpc;
        _logger = logger;

        _globalNotifications = Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(options.NotificationBufferCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _rpc.OnNotification += OnRpcNotificationAsync;
        _rpc.OnServerRequest = OnRpcServerRequestAsync;

        _processExitWatcher = startExitWatcher ? Task.Run(WatchProcessExitAsync) : Task.CompletedTask;
    }

    /// <summary>
    /// Starts a new Codex app-server process and returns a connected client.
    /// </summary>
    public static async Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerFactory = NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<CodexAppServerClient>();
        var serializerOptions = options.SerializerOptionsOverride ?? CreateDefaultSerializerOptions();

        var stdioFactory = CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory);
        var launch = ApplyCodexHome(options.Launch, options.CodexHomeDirectory);
        var (process, rpc) = await CodexJsonRpcBootstrap.StartAsync(
            stdioFactory,
            loggerFactory,
            launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            options.NotificationBufferCapacity,
            serializerOptions,
            includeJsonRpcHeader: false,
            ct);

        var client = new CodexAppServerClient(options, process, rpc, logger);

        _ = await client.InitializeAsync(options.DefaultClientInfo, ct);

        return client;
    }

    internal static JsonSerializerOptions CreateDefaultSerializerOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

    private static CodexLaunch ApplyCodexHome(CodexLaunch launch, string? codexHomeDirectory)
    {
        if (string.IsNullOrWhiteSpace(codexHomeDirectory))
        {
            return launch;
        }

        return launch.WithEnvironment("CODEX_HOME", codexHomeDirectory);
    }

    /// <summary>
    /// Performs the JSON-RPC initialization handshake.
    /// </summary>
    public async Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(clientInfo);

        var result = await _rpc.SendRequestAsync(
            "initialize",
            BuildInitializeParams(clientInfo, _options.Capabilities),
            ct);

        await _rpc.SendNotificationAsync("initialized", @params: null, ct);

        _initializeResult = new AppServerInitializeResult(result);
        return _initializeResult;
    }

    /// <summary>
    /// Gets the initialize result payload, if <see cref="InitializeAsync"/> has completed successfully.
    /// </summary>
    public AppServerInitializeResult? InitializeResult => _initializeResult;

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _rpc.SendRequestAsync(method, @params, ct);

    /// <summary>
    /// A task that completes when the underlying Codex app-server subprocess exits.
    /// </summary>
    public Task ExitTask => _process.Completion;

    /// <summary>
    /// Subscribes to the global app-server notification stream.
    /// </summary>
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _globalNotifications.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Starts a new thread.
    /// </summary>
    public async Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: ExperimentalApiEnabled);

        var result = await _rpc.SendRequestAsync(
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

        var threadId = ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/start returned no thread id. Raw result: {result}");
        }
        return new CodexThread(threadId, result);
    }

    /// <summary>
    /// Resumes an existing thread by ID.
    /// </summary>
    public async Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _rpc.SendRequestAsync(
            "thread/resume",
            new ThreadResumeParams { ThreadId = threadId },
            ct);

        var id = ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    /// <summary>
    /// Resumes an existing thread using the provided options.
    /// </summary>
    public async Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));

        ExperimentalApiGuards.ValidateThreadResume(options, experimentalApiEnabled: ExperimentalApiEnabled);

        var result = await _rpc.SendRequestAsync(
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

        var id = ExtractThreadId(result) ?? options.ThreadId;
        return new CodexThread(id, result);
    }

    /// <summary>
    /// Lists threads with optional filters and paging.
    /// </summary>
    public async Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
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
            Threads = ParseThreadListThreads(result),
            NextCursor = ExtractNextCursor(result),
            Raw = result
        };
    }

    /// <summary>
    /// Reads a thread by ID.
    /// </summary>
    public async Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _rpc.SendRequestAsync(
            "thread/read",
            new ThreadReadParams { ThreadId = threadId },
            ct);

        var threadObject = TryGetObject(result, "thread") ?? result;
        var summary = ParseThreadSummary(threadObject) ?? new CodexThreadSummary
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

    /// <summary>
    /// Forks a thread.
    /// </summary>
    public async Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ExperimentalApiGuards.ValidateThreadFork(options, ExperimentalApiEnabled);

        var result = await _rpc.SendRequestAsync(
            "thread/fork",
            new ThreadForkParams
            {
                ThreadId = options.ThreadId,
                Path = options.Path
            },
            ct);

        var threadId = ExtractThreadId(result);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new InvalidOperationException(
                $"thread/fork returned no thread id. Raw result: {result}");
        }

        return new CodexThread(threadId, result);
    }

    /// <summary>
    /// Archives a thread.
    /// </summary>
    public async Task<CodexThread> ArchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _rpc.SendRequestAsync(
            "thread/archive",
            new ThreadArchiveParams { ThreadId = threadId },
            ct);

        var id = ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    /// <summary>
    /// Unarchives a thread.
    /// </summary>
    public async Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _rpc.SendRequestAsync(
            "thread/unarchive",
            new ThreadUnarchiveParams { ThreadId = threadId },
            ct);

        var id = ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    /// <summary>
    /// Sets (or clears) the thread name.
    /// </summary>
    public async Task SetThreadNameAsync(string threadId, string? name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        _ = await _rpc.SendRequestAsync(
            "thread/name/set",
            new ThreadSetNameParams { ThreadId = threadId, ThreadName = name },
            ct);
    }

    /// <summary>
    /// Lists skills.
    /// </summary>
    public async Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
            "skills/list",
            new SkillsListParams
            {
                Cwd = options.Cwd,
                ExtraRootsForCwd = options.ExtraRootsForCwd
            },
            ct);

        return new SkillsListResult
        {
            Skills = ParseSkillsListSkills(result),
            Raw = result
        };
    }

    /// <summary>
    /// Lists apps/connectors.
    /// </summary>
    public async Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
            "app/list",
            new AppListParams { Cwd = options.Cwd },
            ct);

        return new AppsListResult
        {
            Apps = ParseAppsListApps(result),
            Raw = result
        };
    }

    /// <summary>
    /// Starts a new turn within the specified thread.
    /// </summary>
    public async Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateTurnStart(options, experimentalApiEnabled: ExperimentalApiEnabled);

        var turnStartParams = new TurnStartParams
        {
            ThreadId = threadId,
            Input = options.Input.Select(i => i.Wire).ToArray(),
            Cwd = options.Cwd,
            ApprovalPolicy = options.ApprovalPolicy?.Value,
            SandboxPolicy = options.SandboxPolicy,
            Model = options.Model?.Value,
            Effort = options.Effort?.Value,
            Summary = options.Summary,
            Personality = options.Personality,
            OutputSchema = options.OutputSchema,
            CollaborationMode = options.CollaborationMode
        };

        JsonElement result;
        try
        {
            result = await _rpc.SendRequestAsync("turn/start", turnStartParams, ct);
        }
        catch (JsonRpcRemoteException ex) when (ex.Error.Code == -32602 && ContainsReadOnlyAccessOverrides(options.SandboxPolicy))
        {
            var ua = InitializeResult?.UserAgent ?? "<unknown userAgent>";
            var sandboxJson = JsonSerializer.Serialize(options.SandboxPolicy, CreateDefaultSerializerOptions());
            var data = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? $" Data: {ex.Error.Data.Value.GetRawText()}"
                : string.Empty;

            throw new InvalidOperationException(
                $"turn/start rejected sandboxPolicy parameters (likely unsupported by this Codex app-server build). userAgent='{ua}'. sandboxPolicy={sandboxJson}. Error: {ex.Error.Code}: {ex.Error.Message}.{data}",
                ex);
        }

        var turnId = ExtractTurnId(result);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException(
                $"turn/start returned no turn id. Raw result: {result}");
        }

        return CreateTurnHandle(threadId, turnId);
    }

    private static bool ContainsReadOnlyAccessOverrides(SandboxPolicy? policy) =>
        policy switch
        {
            SandboxPolicy.ReadOnly r => r.Access is not null,
            SandboxPolicy.WorkspaceWrite w => w.ReadOnlyAccess is not null,
            _ => false
        };

    /// <summary>
    /// Steers an in-progress turn by appending input items.
    /// </summary>
    public async Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));
        if (string.IsNullOrWhiteSpace(options.ExpectedTurnId))
            throw new ArgumentException("ExpectedTurnId cannot be empty or whitespace.", nameof(options.ExpectedTurnId));

        try
        {
            var result = await _rpc.SendRequestAsync(
                "turn/steer",
                BuildTurnSteerParams(options),
                ct);

            return ExtractTurnId(result) ?? options.ExpectedTurnId;
        }
        catch (JsonRpcRemoteException ex)
        {
            var data = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? $" Data: {ex.Error.Data.Value.GetRawText()}"
                : string.Empty;

            throw new InvalidOperationException(
                $"turn/steer failed for expectedTurnId '{options.ExpectedTurnId}': {ex.Error.Code}: {ex.Error.Message}.{data}",
                ex);
        }
    }

    /// <summary>
    /// Starts a review via the app-server.
    /// </summary>
    public async Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));
        ArgumentNullException.ThrowIfNull(options.Target);

        var result = await _rpc.SendRequestAsync(
            "review/start",
            BuildReviewStartParams(options),
            ct);

        var reviewThreadId = GetStringOrNull(result, "reviewThreadId");

        var turnObj = TryGetObject(result, "turn") ?? result;
        var turnId = ExtractTurnId(turnObj);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException(
                $"review/start returned no turn id. Raw result: {result}");
        }

        var turnThreadId = ExtractThreadId(turnObj) ?? reviewThreadId ?? options.ThreadId;

        return new ReviewStartResult
        {
            Turn = CreateTurnHandle(turnThreadId, turnId),
            ReviewThreadId = reviewThreadId,
            Raw = result
        };
    }

    internal static TurnSteerParams BuildTurnSteerParams(TurnSteerOptions options) =>
        new()
        {
            ThreadId = options.ThreadId,
            ExpectedTurnId = options.ExpectedTurnId,
            Input = options.Input.Select(i => i.Wire).ToArray()
        };

    internal static ReviewStartParams BuildReviewStartParams(ReviewStartOptions options) =>
        new()
        {
            ThreadId = options.ThreadId,
            Target = options.Target.ToWire(),
            Delivery = options.Delivery switch
            {
                ReviewDelivery.Inline => "inline",
                ReviewDelivery.Detached => "detached",
                _ => null
            }
        };

    private CodexTurnHandle CreateTurnHandle(string threadId, string turnId)
    {
        var handle = new CodexTurnHandle(
            threadId,
            turnId,
            interrupt: c => InterruptAsync(threadId, turnId, c),
            steer: (input, c) => SteerTurnAsync(new TurnSteerOptions { ThreadId = threadId, ExpectedTurnId = turnId, Input = input }, c),
            onDispose: () =>
            {
                lock (_turnsById)
                {
                    _turnsById.Remove(turnId);
                }
            },
            bufferCapacity: _options.NotificationBufferCapacity);

        lock (_turnsById)
        {
            _turnsById[turnId] = handle;
        }

        return handle;
    }

    private Task InterruptAsync(string threadId, string turnId, CancellationToken ct) =>
        _rpc.SendRequestAsync(
            "turn/interrupt",
            new TurnInterruptParams { ThreadId = threadId, TurnId = turnId },
            ct);

    private ValueTask OnRpcNotificationAsync(JsonRpcNotification notification)
    {
        var mapped = AppServerNotificationMapper.Map(notification.Method, notification.Params);

        _globalNotifications.Writer.TryWrite(mapped);
        LogIfBogus(mapped);

        var turnId = TryGetTurnId(mapped);
        if (!string.IsNullOrWhiteSpace(turnId))
        {
            CodexTurnHandle? handle;
            lock (_turnsById)
            {
                _turnsById.TryGetValue(turnId, out handle);
            }

            if (handle is not null)
            {
                handle.EventsChannel.Writer.TryWrite(mapped);

                if (mapped is TurnCompletedNotification completed)
                {
                    handle.CompletionTcs.TrySetResult(completed);
                    handle.EventsChannel.Writer.TryComplete();
                }
            }
        }

        return ValueTask.CompletedTask;
    }

    private void LogIfBogus(AppServerNotification notification)
    {
#if DEBUG
        var isBogus = notification switch
        {
            AgentMessageDeltaNotification d => string.IsNullOrWhiteSpace(d.ThreadId) ||
                                              string.IsNullOrWhiteSpace(d.TurnId) ||
                                              string.IsNullOrWhiteSpace(d.ItemId),
            ItemStartedNotification s => string.IsNullOrWhiteSpace(s.ThreadId) ||
                                         string.IsNullOrWhiteSpace(s.TurnId) ||
                                         string.IsNullOrWhiteSpace(s.ItemId),
            ItemCompletedNotification c => string.IsNullOrWhiteSpace(c.ThreadId) ||
                                           string.IsNullOrWhiteSpace(c.TurnId) ||
                                           string.IsNullOrWhiteSpace(c.ItemId),
            TurnStartedNotification s => string.IsNullOrWhiteSpace(s.ThreadId) ||
                                         string.IsNullOrWhiteSpace(s.TurnId),
            TurnCompletedNotification t => string.IsNullOrWhiteSpace(t.ThreadId) ||
                                           string.IsNullOrWhiteSpace(t.TurnId),
            TurnDiffUpdatedNotification d => string.IsNullOrWhiteSpace(d.ThreadId) ||
                                            string.IsNullOrWhiteSpace(d.TurnId),
            TurnPlanUpdatedNotification p => string.IsNullOrWhiteSpace(p.ThreadId) ||
                                             string.IsNullOrWhiteSpace(p.TurnId),
            ThreadTokenUsageUpdatedNotification u => string.IsNullOrWhiteSpace(u.ThreadId) ||
                                                    string.IsNullOrWhiteSpace(u.TurnId),
            ErrorNotification e => string.IsNullOrWhiteSpace(e.ThreadId) ||
                                   string.IsNullOrWhiteSpace(e.TurnId),
            _ => false
        };

        if (isBogus)
        {
            _logger.LogWarning("Received malformed app-server notification: {Method}. Raw params: {Params}", notification.Method, notification.Params);
        }
#endif
    }

    private async ValueTask<JsonRpcResponse> OnRpcServerRequestAsync(JsonRpcRequest req)
    {
        var handler = _options.ApprovalHandler;

        if (handler is null)
        {
            return new JsonRpcResponse(
                req.Id,
                Result: null,
                Error: new JsonRpcError(-32601, $"Unhandled server request '{req.Method}'."));
        }

        try
        {
            var result = await handler.HandleAsync(req.Method, req.Params, CancellationToken.None);
            return new JsonRpcResponse(req.Id, Result: result, Error: null);
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse(req.Id, Result: null, Error: new JsonRpcError(-32000, ex.Message));
        }
    }

    /// <summary>
    /// Disposes the underlying app-server connection and terminates the process.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _globalNotifications.Writer.TryComplete();

        CodexTurnHandle[] handles;
        lock (_turnsById)
        {
            handles = _turnsById.Values.ToArray();
            _turnsById.Clear();
        }

        foreach (var handle in handles)
        {
            handle.EventsChannel.Writer.TryComplete();
            handle.CompletionTcs.TrySetCanceled();
        }

        await _rpc.DisposeAsync();
        await _process.DisposeAsync();
    }

    private async Task WatchProcessExitAsync()
    {
        try
        {
            await _process.Completion.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        SignalDisconnect(BuildDisconnectException());
    }

    private Exception BuildDisconnectException()
    {
        var stderrTail = Array.Empty<string>();
        try
        {
            stderrTail = _process.StderrTail.ToArray();
        }
        catch
        {
            // ignore
        }

        var exitCode = _process.ExitCode;
        var pid = _process.ProcessId;

        var msg = exitCode is null
            ? "Codex app-server subprocess disconnected."
            : $"Codex app-server subprocess exited with code {exitCode}.";

        if (stderrTail.Length > 0)
        {
            msg += $" (stderr tail: {string.Join(" | ", stderrTail.TakeLast(5))})";
        }

        return new CodexAppServerDisconnectedException(
            msg,
            processId: pid,
            exitCode: exitCode,
            stderrTail: stderrTail);
    }

    private void SignalDisconnect(Exception ex)
    {
        if (Interlocked.Exchange(ref _disconnectSignaled, 1) != 0)
        {
            return;
        }

        _globalNotifications.Writer.TryComplete(ex);

        CodexTurnHandle[] handles;
        lock (_turnsById)
        {
            handles = _turnsById.Values.ToArray();
            _turnsById.Clear();
        }

        foreach (var handle in handles)
        {
            handle.EventsChannel.Writer.TryComplete(ex);
            handle.CompletionTcs.TrySetException(ex);
        }
    }

    internal static string? ExtractId(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    internal static string? ExtractThreadId(JsonElement result)
    {
        // Common shapes:
        // - { "threadId": "..." }
        // - { "id": "..." }
        // - { "thread": { "id": "..." } }
        // - { "thread": { "threadId": "..." } }
        return ExtractId(result, "threadId", "id") ??
               ExtractIdByPath(result, "thread", "threadId") ??
               ExtractIdByPath(result, "thread", "id") ??
               FindStringPropertyRecursive(result, propertyName: "threadId", maxDepth: 6);
    }

    internal static string? ExtractTurnId(JsonElement result)
    {
        // Common shapes:
        // - { "turnId": "..." }
        // - { "id": "..." }
        // - { "turn": { "id": "..." } }
        // - { "turn": { "turnId": "..." } }
        return ExtractId(result, "turnId", "id") ??
               ExtractIdByPath(result, "turn", "turnId") ??
               ExtractIdByPath(result, "turn", "id") ??
               FindStringPropertyRecursive(result, propertyName: "turnId", maxDepth: 6);
    }

    internal static string? ExtractIdByPath(JsonElement element, string p1, string p2)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(p1, out var child) || child.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ExtractId(child, p2);
    }

    internal static string? FindStringPropertyRecursive(JsonElement element, string propertyName, int maxDepth)
    {
        if (maxDepth < 0)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                foreach (var p in element.EnumerateObject())
                {
                    var found = FindStringPropertyRecursive(p.Value, propertyName, maxDepth - 1);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                return null;
            }
            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindStringPropertyRecursive(item, propertyName, maxDepth - 1);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                return null;
            }
            default:
                return null;
        }
    }

    private static string? TryGetTurnId(AppServerNotification notification) =>
        notification switch
        {
            AgentMessageDeltaNotification d => d.TurnId,
            ItemStartedNotification s => s.TurnId,
            ItemCompletedNotification c => c.TurnId,
            TurnStartedNotification s => s.TurnId,
            TurnDiffUpdatedNotification d => d.TurnId,
            TurnPlanUpdatedNotification p => p.TurnId,
            ThreadTokenUsageUpdatedNotification u => u.TurnId,
            PlanDeltaNotification d => d.TurnId,
            RawResponseItemCompletedNotification r => r.TurnId,
            CommandExecutionOutputDeltaNotification d => d.TurnId,
            TerminalInteractionNotification t => t.TurnId,
            FileChangeOutputDeltaNotification d => d.TurnId,
            McpToolCallProgressNotification p => p.TurnId,
            ReasoningSummaryTextDeltaNotification d => d.TurnId,
            ReasoningSummaryPartAddedNotification d => d.TurnId,
            ReasoningTextDeltaNotification d => d.TurnId,
            ContextCompactedNotification c => c.TurnId,
            ErrorNotification e => e.TurnId,
            TurnCompletedNotification t => t.TurnId,
            _ => null
        };

    internal static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult)
    {
        var array =
            TryGetArray(listResult, "threads") ??
            TryGetArray(listResult, "items") ??
            TryGetArray(listResult, "sessions");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexThreadSummary>();
        }

        var threads = new List<CodexThreadSummary>();
        foreach (var item in array.Value.EnumerateArray())
        {
            var summary = ParseThreadSummary(item);
            if (summary is not null)
            {
                threads.Add(summary);
            }
        }

        return threads;
    }

    internal static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject)
    {
        if (threadObject.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var primary = TryGetObject(threadObject, "thread") ?? threadObject;

        var threadId = ExtractThreadId(threadObject);
        if (string.IsNullOrWhiteSpace(threadId))
        {
            return null;
        }

        var name =
            GetStringOrNull(primary, "name") ??
            GetStringOrNull(primary, "threadName") ??
            GetStringOrNull(primary, "title");

        var archived = GetBoolOrNull(primary, "archived");
        var createdAt = GetDateTimeOffsetOrNull(primary, "createdAt");
        var cwd = GetStringOrNull(primary, "cwd");
        var model = GetStringOrNull(primary, "model");

        return new CodexThreadSummary
        {
            ThreadId = threadId,
            Name = name,
            Archived = archived,
            CreatedAt = createdAt,
            Cwd = cwd,
            Model = model,
            Raw = threadObject
        };
    }

    internal static string? ExtractNextCursor(JsonElement listResult) =>
        GetStringOrNull(listResult, "nextCursor") ??
        GetStringOrNull(listResult, "cursor");

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(JsonElement skillsListResult)
    {
        var array =
            TryGetArray(skillsListResult, "skills") ??
            TryGetArray(skillsListResult, "items");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SkillDescriptor>();
        }

        var skills = new List<SkillDescriptor>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetStringOrNull(item, "name") ?? GetStringOrNull(item, "id");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            skills.Add(new SkillDescriptor
            {
                Name = name,
                Description = GetStringOrNull(item, "description"),
                Path = GetStringOrNull(item, "path"),
                Raw = item
            });
        }

        return skills;
    }

    internal static IReadOnlyList<AppDescriptor> ParseAppsListApps(JsonElement appsListResult)
    {
        var array =
            TryGetArray(appsListResult, "apps") ??
            TryGetArray(appsListResult, "items");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AppDescriptor>();
        }

        var apps = new List<AppDescriptor>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            apps.Add(new AppDescriptor
            {
                Id = GetStringOrNull(item, "id"),
                Name = GetStringOrNull(item, "name"),
                Title = GetStringOrNull(item, "title"),
                Enabled = GetBoolOrNull(item, "enabled"),
                DisabledReason = GetStringOrNull(item, "disabledReason"),
                Raw = item
            });
        }

        return apps;
    }

    private static JsonElement? TryGetArray(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.Array
            ? p
            : null;

    private static JsonElement? TryGetObject(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.Object
            ? p
            : null;

    private static string? GetStringOrNull(JsonElement obj, string propertyName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propertyName, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static bool? GetBoolOrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static DateTimeOffset? GetDateTimeOffsetOrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(p.GetString(), out var dto))
        {
            return dto;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var epoch))
        {
            // Best-effort: treat large values as milliseconds, otherwise seconds.
            return epoch > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return null;
    }
}
