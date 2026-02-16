using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
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
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;
using JKToolKit.CodexSDK.AppServer.Internal;
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
    private readonly CodexAppServerThreadsClient _threadsClient;
    private readonly CodexAppServerSkillsAppsClient _skillsAppsClient;
    private readonly CodexAppServerConfigClient _configClient;
    private readonly CodexAppServerFuzzyFileSearchClient _fuzzyFileSearchClient;

    private readonly Channel<AppServerNotification> _globalNotifications;
    private readonly Dictionary<string, CodexTurnHandle> _turnsById = new(StringComparer.Ordinal);
    private int _disposed;
    private int _disconnectSignaled;
    private int _readOnlyAccessOverridesSupport; // -1 = rejected, 0 = unknown, 1 = supported
    private readonly Task _processExitWatcher;
    private AppServerInitializeResult? _initializeResult;

    private bool ExperimentalApiEnabled => _options.Capabilities?.ExperimentalApi == true || _options.ExperimentalApi;

    internal static bool TryParseExperimentalApiRequiredMessage(string? message, out string descriptor)
    {
        const string suffix = " requires experimentalApi capability";

        descriptor = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var idx = message.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
        if (idx <= 0)
        {
            return false;
        }

        descriptor = message[..idx].Trim().Trim('\'', '"');
        return !string.IsNullOrWhiteSpace(descriptor);
    }

    internal static InitializeCapabilities? BuildCapabilitiesFromOptions(CodexAppServerClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var caps = options.Capabilities;

        var experimentalApi = options.ExperimentalApi || caps?.ExperimentalApi == true;

        var optOut = new List<string>();
        if (caps?.OptOutNotificationMethods is { Count: > 0 })
        {
            optOut.AddRange(caps.OptOutNotificationMethods.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        if (options.OptOutNotificationMethods is { Count: > 0 })
        {
            optOut.AddRange(options.OptOutNotificationMethods.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        IReadOnlyList<string>? optOutNormalized = null;
        if (optOut.Count > 0)
        {
            optOutNormalized = optOut.Distinct(StringComparer.Ordinal).ToArray();
        }

        return NormalizeCapabilities(new InitializeCapabilities
        {
            ExperimentalApi = experimentalApi,
            OptOutNotificationMethods = optOutNormalized
        });
    }

    private async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
    {
        try
        {
            return await _rpc.SendRequestAsync(method, @params, ct);
        }
        catch (JsonRpcRemoteException ex) when (TryParseExperimentalApiRequiredMessage(ex.Error.Message, out var descriptor))
        {
            throw new CodexExperimentalApiRequiredException(descriptor, ex);
        }
    }

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
        _threadsClient = new CodexAppServerThreadsClient(SendRequestAsync, ExperimentalApiEnabled);
        _skillsAppsClient = new CodexAppServerSkillsAppsClient(SendRequestAsync);
        _configClient = new CodexAppServerConfigClient(SendRequestAsync, ExperimentalApiEnabled);
        _fuzzyFileSearchClient = new CodexAppServerFuzzyFileSearchClient(SendRequestAsync, ExperimentalApiEnabled);

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

        var requestedCapabilities = BuildCapabilitiesFromOptions(_options);

        try
        {
            var result = await _rpc.SendRequestAsync(
                "initialize",
                BuildInitializeParams(clientInfo, requestedCapabilities),
                ct);

            await _rpc.SendNotificationAsync("initialized", @params: null, ct);

            _initializeResult = new AppServerInitializeResult(result);
            return _initializeResult;
        }
        catch (JsonRpcRemoteException ex)
        {
            var dataJson = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? ex.Error.Data.Value.GetRawText()
                : null;

            var wantsCapabilities = requestedCapabilities is not null ||
                                    _options.ExperimentalApi ||
                                    (_options.OptOutNotificationMethods is { Count: > 0 });

            var help = wantsCapabilities
                ? "This may indicate your Codex app-server build does not support the requested initialize capabilities. Try upgrading Codex or omit CodexAppServerClientOptions.Capabilities / ExperimentalApi / OptOutNotificationMethods."
                : null;

            throw new CodexAppServerInitializeException(
                ex.Error.Code,
                ex.Error.Message,
                dataJson,
                help,
                stderrTail: _process.StderrTail,
                innerException: ex);
        }
    }

    /// <summary>
    /// Gets the initialize result payload, if <see cref="InitializeAsync"/> has completed successfully.
    /// </summary>
    public AppServerInitializeResult? InitializeResult => _initializeResult;

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        SendRequestAsync(method, @params, ct);

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
    public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default) =>
        _threadsClient.StartThreadAsync(options, ct);

    /// <summary>
    /// Resumes an existing thread by ID.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.ResumeThreadAsync(threadId, ct);

    /// <summary>
    /// Resumes an existing thread using the provided options.
    /// </summary>
    public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default) =>
        _threadsClient.ResumeThreadAsync(options, ct);

    /// <summary>
    /// Lists threads with optional filters and paging.
    /// </summary>
    public Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct = default) =>
        _threadsClient.ListThreadsAsync(options, ct);

    /// <summary>
    /// Reads a thread by ID.
    /// </summary>
    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.ReadThreadAsync(threadId, ct);

    /// <summary>
    /// Lists identifiers of threads currently loaded in memory by the app-server.
    /// </summary>
    public Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default) =>
        _threadsClient.ListLoadedThreadsAsync(options, ct);

    /// <summary>
    /// Starts thread compaction.
    /// </summary>
    public Task CompactThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.CompactThreadAsync(threadId, ct);

    /// <summary>
    /// Rolls back the thread by the specified number of turns.
    /// </summary>
    public Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct = default) =>
        _threadsClient.RollbackThreadAsync(threadId, numTurns, ct);

    /// <summary>
    /// Terminates all running background terminals associated with the thread (experimental).
    /// </summary>
    public Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.CleanThreadBackgroundTerminalsAsync(threadId, ct);

    /// <summary>
    /// Forks a thread.
    /// </summary>
    public Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default) =>
        _threadsClient.ForkThreadAsync(options, ct);

    /// <summary>
    /// Archives a thread.
    /// </summary>
    public Task<CodexThread> ArchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.ArchiveThreadAsync(threadId, ct);

    /// <summary>
    /// Unarchives a thread.
    /// </summary>
    public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.UnarchiveThreadAsync(threadId, ct);

    /// <summary>
    /// Sets (or clears) the thread name.
    /// </summary>
    public Task SetThreadNameAsync(string threadId, string? name, CancellationToken ct = default) =>
        _threadsClient.SetThreadNameAsync(threadId, name, ct);

    /// <summary>
    /// Lists skills.
    /// </summary>
    public Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct = default) =>
        _skillsAppsClient.ListSkillsAsync(options, ct);

    /// <summary>
    /// Lists apps/connectors.
    /// </summary>
    public Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default) =>
        _skillsAppsClient.ListAppsAsync(options, ct);

    /// <summary>
    /// Reads the active configuration requirements constraints (for example from <c>requirements.toml</c> or MDM).
    /// </summary>
    public Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct = default) =>
        _configClient.ReadConfigRequirementsAsync(ct);

    /// <summary>
    /// Reads remote skills.
    /// </summary>
    public Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct = default) =>
        _configClient.ReadRemoteSkillsAsync(ct);

    /// <summary>
    /// Writes a remote skill reference.
    /// </summary>
    public Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct = default) =>
        _configClient.WriteRemoteSkillAsync(hazelnutId, isPreload, ct);

    /// <summary>
    /// Writes skills configuration.
    /// </summary>
    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct = default) =>
        _configClient.WriteSkillsConfigAsync(enabled, path, ct);

    /// <summary>
    /// Starts a fuzzy file search session (experimental).
    /// </summary>
    public Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.StartFuzzyFileSearchSessionAsync(sessionId, roots, ct);

    /// <summary>
    /// Updates a fuzzy file search session query (experimental).
    /// </summary>
    public Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.UpdateFuzzyFileSearchSessionAsync(sessionId, query, ct);

    /// <summary>
    /// Stops a fuzzy file search session (experimental).
    /// </summary>
    public Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default) =>
        _fuzzyFileSearchClient.StopFuzzyFileSearchSessionAsync(sessionId, ct);

    /// <summary>
    /// Starts a new turn within the specified thread.
    /// </summary>
    public async Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateTurnStart(options, experimentalApiEnabled: ExperimentalApiEnabled);

        if (ContainsReadOnlyAccessOverrides(options.SandboxPolicy) &&
            Volatile.Read(ref _readOnlyAccessOverridesSupport) == -1)
        {
            var ua = InitializeResult?.UserAgent ?? "<unknown userAgent>";
            throw new InvalidOperationException(
                $"turn/start sandboxPolicy ReadOnlyAccess overrides were previously rejected by this app-server build. userAgent='{ua}'. Do not send ReadOnlyAccess fields unless your Codex app-server supports them.");
        }

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
            result = await SendRequestAsync("turn/start", turnStartParams, ct);
        }
        catch (JsonRpcRemoteException ex) when (ex.Error.Code == -32602 && ContainsReadOnlyAccessOverrides(options.SandboxPolicy))
        {
            Interlocked.Exchange(ref _readOnlyAccessOverridesSupport, -1);
            var ua = InitializeResult?.UserAgent ?? "<unknown userAgent>";
            var sandboxJson = JsonSerializer.Serialize(options.SandboxPolicy, CreateDefaultSerializerOptions());
            var data = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? $" Data: {ex.Error.Data.Value.GetRawText()}"
                : string.Empty;

            throw new InvalidOperationException(
                $"turn/start rejected sandboxPolicy parameters (likely unsupported by this Codex app-server build). userAgent='{ua}'. sandboxPolicy={sandboxJson}. Error: {ex.Error.Code}: {ex.Error.Message}.{data}",
                ex);
        }

        if (ContainsReadOnlyAccessOverrides(options.SandboxPolicy))
        {
            Interlocked.Exchange(ref _readOnlyAccessOverridesSupport, 1);
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
    /// <remarks>
    /// Steering is best-effort and may race with turn completion. Cancellation stops waiting for the response but does
    /// not guarantee the server did not apply the steer request.
    /// </remarks>
    public async Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default)
    {
        var result = await SteerTurnRawAsync(options, ct);
        return result.TurnId;
    }

    /// <summary>
    /// Steers an in-progress turn by appending input items and returns the raw JSON result payload.
    /// </summary>
    /// <remarks>
    /// Steering is best-effort and may race with turn completion. Cancellation stops waiting for the response but does
    /// not guarantee the server did not apply the steer request.
    /// </remarks>
    public async Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));
        if (string.IsNullOrWhiteSpace(options.ExpectedTurnId))
            throw new ArgumentException("ExpectedTurnId cannot be empty or whitespace.", nameof(options.ExpectedTurnId));

        try
        {
            var raw = await SendRequestAsync(
                "turn/steer",
                BuildTurnSteerParams(options),
                ct);

            return new TurnSteerResult
            {
                TurnId = ExtractTurnId(raw) ?? options.ExpectedTurnId,
                Raw = raw
            };
        }
        catch (JsonRpcRemoteException ex)
        {
            var ua = InitializeResult?.UserAgent;
            throw new CodexAppServerRequestFailedException(
                method: "turn/steer",
                errorCode: ex.Error.Code,
                errorMessage: $"{ex.Error.Message} (expectedTurnId='{options.ExpectedTurnId}')",
                errorData: ex.Error.Data,
                userAgent: ua,
                innerException: ex);
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

        JsonElement result;
        try
        {
            result = await SendRequestAsync(
                "review/start",
                BuildReviewStartParams(options),
                ct);
        }
        catch (JsonRpcRemoteException ex)
        {
            var ua = InitializeResult?.UserAgent;
            throw new CodexAppServerRequestFailedException(
                method: "review/start",
                errorCode: ex.Error.Code,
                errorMessage: ex.Error.Message,
                errorData: ex.Error.Data,
                userAgent: ua,
                innerException: ex);
        }

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

    /// <summary>
    /// Starts a review via the app-server.
    /// </summary>
    /// <remarks>
    /// This is an alias for <see cref="StartReviewAsync"/> to better align with exec-mode naming (<c>ReviewAsync</c>).
    /// </remarks>
    public Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct = default) =>
        StartReviewAsync(options, ct);

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
            steerRaw: (input, c) => SteerTurnRawAsync(new TurnSteerOptions { ThreadId = threadId, ExpectedTurnId = turnId, Input = input }, c),
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
        SendRequestAsync(
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

    internal static string? ExtractId(JsonElement element, params string[] propertyNames) =>
        CodexAppServerClientJson.ExtractId(element, propertyNames);

    internal static string? ExtractThreadId(JsonElement result) =>
        CodexAppServerClientJson.ExtractThreadId(result);

    internal static string? ExtractTurnId(JsonElement result) =>
        CodexAppServerClientJson.ExtractTurnId(result);

    internal static string? ExtractIdByPath(JsonElement element, string p1, string p2) =>
        CodexAppServerClientJson.ExtractIdByPath(element, p1, p2);

    internal static string? FindStringPropertyRecursive(JsonElement element, string propertyName, int maxDepth) =>
        CodexAppServerClientJson.FindStringPropertyRecursive(element, propertyName, maxDepth);

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

    internal static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult) =>
        CodexAppServerClientThreadParsers.ParseThreadListThreads(listResult);

    internal static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject) =>
        CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject);

    internal static string? ExtractNextCursor(JsonElement listResult) =>
        CodexAppServerClientThreadParsers.ExtractNextCursor(listResult);

    internal static IReadOnlyList<string> ParseThreadLoadedListThreadIds(JsonElement loadedListResult) =>
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(loadedListResult);

    internal static IReadOnlyList<SkillsListEntryResult> ParseSkillsListEntries(JsonElement skillsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListEntries(skillsListResult);

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(JsonElement skillsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(skillsListResult);

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(IReadOnlyList<SkillsListEntryResult> entries) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(entries);

    internal static IReadOnlyList<AppDescriptor> ParseAppsListApps(JsonElement appsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(appsListResult);

    internal static IReadOnlyList<RemoteSkillDescriptor> ParseRemoteSkillsReadSkills(JsonElement remoteSkillsResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseRemoteSkillsReadSkills(remoteSkillsResult);

    internal static ConfigRequirements? ParseConfigRequirementsReadRequirements(JsonElement configRequirementsReadResult, bool experimentalApiEnabled) =>
        CodexAppServerClientConfigRequirementsParser.ParseConfigRequirementsReadRequirements(configRequirementsReadResult, experimentalApiEnabled);

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

    private static int? GetInt32OrNull(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
        {
            return i;
        }

        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out i))
        {
            return i;
        }

        return null;
    }

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

    private static IReadOnlyList<string>? GetOptionalStringArray(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propertyName, out var p))
        {
            return null;
        }

        if (p.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (p.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in p.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
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
