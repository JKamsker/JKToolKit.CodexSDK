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
    public async Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateThreadStart(options, experimentalApiEnabled: ExperimentalApiEnabled);

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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
    /// Lists identifiers of threads currently loaded in memory by the app-server.
    /// </summary>
    public async Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await SendRequestAsync(
            "thread/loaded/list",
            new ThreadLoadedListParams
            {
                Cursor = options.Cursor,
                Limit = options.Limit
            },
            ct);

        return new CodexLoadedThreadListPage
        {
            ThreadIds = ParseThreadLoadedListThreadIds(result),
            NextCursor = ExtractNextCursor(result),
            Raw = result
        };
    }

    /// <summary>
    /// Starts thread compaction.
    /// </summary>
    public async Task CompactThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        _ = await SendRequestAsync(
            "thread/compact/start",
            new ThreadCompactStartParams { ThreadId = threadId },
            ct);
    }

    /// <summary>
    /// Rolls back the thread by the specified number of turns.
    /// </summary>
    public async Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));
        if (numTurns <= 0)
            throw new ArgumentOutOfRangeException(nameof(numTurns), numTurns, "NumTurns must be greater than zero.");

        var result = await SendRequestAsync(
            "thread/rollback",
            new ThreadRollbackParams
            {
                ThreadId = threadId,
                NumTurns = numTurns
            },
            ct);

        var threadObj = TryGetObject(result, "thread") ?? result;
        var id = ExtractThreadId(threadObj) ?? threadId;
        return new CodexThread(id, result);
    }

    /// <summary>
    /// Terminates all running background terminals associated with the thread (experimental).
    /// </summary>
    public async Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        if (!ExperimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("thread/backgroundTerminals/clean");
        }

        _ = await SendRequestAsync(
            "thread/backgroundTerminals/clean",
            new ThreadBackgroundTerminalsCleanParams { ThreadId = threadId },
            ct);
    }

    /// <summary>
    /// Forks a thread.
    /// </summary>
    public async Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ExperimentalApiGuards.ValidateThreadFork(options, ExperimentalApiEnabled);

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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

        var result = await SendRequestAsync(
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

        _ = await SendRequestAsync(
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

        IReadOnlyList<string>? cwds = null;
        if (options.Cwds is { Count: > 0 })
        {
            cwds = options.Cwds;
        }
        else if (!string.IsNullOrWhiteSpace(options.Cwd))
        {
            cwds = [options.Cwd];
        }

        SkillsListExtraRootsForCwd[]? perCwd = null;
        if (options.ExtraRootsForCwd is { Count: > 0 })
        {
            var cwd = options.Cwd ?? (cwds is { Count: 1 } ? cwds[0] : null);
            if (string.IsNullOrWhiteSpace(cwd))
            {
                throw new ArgumentException("ExtraRootsForCwd requires a single Cwd scope.", nameof(options));
            }

            perCwd =
            [
                new SkillsListExtraRootsForCwd
                {
                    Cwd = cwd,
                    ExtraUserRoots = options.ExtraRootsForCwd
                }
            ];
        }

        var result = await SendRequestAsync(
            "skills/list",
            new SkillsListParams
            {
                Cwds = cwds,
                ForceReload = options.ForceReload ? true : null,
                PerCwdExtraUserRoots = perCwd
            },
            ct);

        var entries = ParseSkillsListEntries(result);

        return new SkillsListResult
        {
            Entries = entries,
            Skills = ParseSkillsListSkills(entries),
            Raw = result
        };
    }

    /// <summary>
    /// Lists apps/connectors.
    /// </summary>
    public async Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await SendRequestAsync(
            "app/list",
            new AppListParams
            {
                Cursor = options.Cursor,
                Limit = options.Limit,
                ThreadId = options.ThreadId,
                ForceRefetch = options.ForceRefetch ? true : null
            },
            ct);

        return new AppsListResult
        {
            Apps = ParseAppsListApps(result),
            NextCursor = ExtractNextCursor(result),
            Raw = result
        };
    }

    /// <summary>
    /// Reads the active configuration requirements constraints (for example from <c>requirements.toml</c> or MDM).
    /// </summary>
    public async Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync(
            "configRequirements/read",
            @params: null,
            ct);

        return new ConfigRequirementsReadResult
        {
            Requirements = ParseConfigRequirementsReadRequirements(result, experimentalApiEnabled: ExperimentalApiEnabled),
            Raw = result
        };
    }

    /// <summary>
    /// Reads remote skills.
    /// </summary>
    public async Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync(
            "skills/remote/read",
            @params: null,
            ct);

        return new RemoteSkillsReadResult
        {
            Skills = ParseRemoteSkillsReadSkills(result),
            Raw = result
        };
    }

    /// <summary>
    /// Writes a remote skill reference.
    /// </summary>
    public async Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(hazelnutId))
            throw new ArgumentException("HazelnutId cannot be empty or whitespace.", nameof(hazelnutId));

        var result = await SendRequestAsync(
            "skills/remote/write",
            new SkillsRemoteWriteParams
            {
                HazelnutId = hazelnutId,
                IsPreload = isPreload
            },
            ct);

        return new RemoteSkillWriteResult
        {
            Id = GetStringOrNull(result, "id"),
            Name = GetStringOrNull(result, "name"),
            Path = GetStringOrNull(result, "path"),
            Raw = result
        };
    }

    /// <summary>
    /// Writes skills configuration.
    /// </summary>
    public async Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty or whitespace.", nameof(path));

        var result = await SendRequestAsync(
            "skills/config/write",
            new SkillsConfigWriteParams
            {
                Enabled = enabled,
                Path = path
            },
            ct);

        return new SkillsConfigWriteResult
        {
            EffectiveEnabled = GetBoolOrNull(result, "effectiveEnabled"),
            Raw = result
        };
    }

    /// <summary>
    /// Starts a fuzzy file search session (experimental).
    /// </summary>
    public async Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        ArgumentNullException.ThrowIfNull(roots);

        if (!ExperimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionStart");
        }

        _ = await SendRequestAsync(
            "fuzzyFileSearch/sessionStart",
            new FuzzyFileSearchSessionStartParams
            {
                SessionId = sessionId,
                Roots = roots
            },
            ct);
    }

    /// <summary>
    /// Updates a fuzzy file search session query (experimental).
    /// </summary>
    public async Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty or whitespace.", nameof(query));

        if (!ExperimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionUpdate");
        }

        _ = await SendRequestAsync(
            "fuzzyFileSearch/sessionUpdate",
            new FuzzyFileSearchSessionUpdateParams
            {
                SessionId = sessionId,
                Query = query
            },
            ct);
    }

    /// <summary>
    /// Stops a fuzzy file search session (experimental).
    /// </summary>
    public async Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId cannot be empty or whitespace.", nameof(sessionId));

        if (!ExperimentalApiEnabled)
        {
            throw new CodexExperimentalApiRequiredException("fuzzyFileSearch/sessionStop");
        }

        _ = await SendRequestAsync(
            "fuzzyFileSearch/sessionStop",
            new FuzzyFileSearchSessionStopParams
            {
                SessionId = sessionId
            },
            ct);
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

    internal static IReadOnlyList<string> ParseThreadLoadedListThreadIds(JsonElement loadedListResult)
    {
        var array =
            TryGetArray(loadedListResult, "data") ??
            TryGetArray(loadedListResult, "threads");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var ids = new List<string>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var id = item.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    internal static IReadOnlyList<SkillsListEntryResult> ParseSkillsListEntries(JsonElement skillsListResult)
    {
        var data = TryGetArray(skillsListResult, "data");
        if (data is not null && data.Value.ValueKind == JsonValueKind.Array)
        {
            var entries = new List<SkillsListEntryResult>();
            foreach (var entry in data.Value.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var cwd = GetStringOrNull(entry, "cwd");

                var skills = new List<SkillDescriptor>();
                var skillsArray = TryGetArray(entry, "skills");
                if (skillsArray is not null && skillsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var skill in skillsArray.Value.EnumerateArray())
                    {
                        if (skill.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var name = GetStringOrNull(skill, "name") ?? GetStringOrNull(skill, "id");
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        skills.Add(new SkillDescriptor
                        {
                            Name = name,
                            Description = GetStringOrNull(skill, "description"),
                            ShortDescription = GetStringOrNull(skill, "shortDescription"),
                            Path = GetStringOrNull(skill, "path"),
                            Enabled = GetBoolOrNull(skill, "enabled"),
                            Cwd = cwd,
                            Scope = GetStringOrNull(skill, "scope"),
                            Dependencies = TryGetObject(skill, "dependencies"),
                            Interface = TryGetObject(skill, "interface"),
                            Raw = skill
                        });
                    }
                }

                var errors = new List<CodexSkillErrorInfo>();
                var errorsArray = TryGetArray(entry, "errors");
                if (errorsArray is not null && errorsArray.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var err in errorsArray.Value.EnumerateArray())
                    {
                        if (err.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        errors.Add(new CodexSkillErrorInfo
                        {
                            Message = GetStringOrNull(err, "message"),
                            Path = GetStringOrNull(err, "path"),
                            Raw = err
                        });
                    }
                }

                entries.Add(new SkillsListEntryResult
                {
                    Cwd = cwd,
                    Skills = skills,
                    Errors = errors,
                    Raw = entry
                });
            }

            return entries;
        }

        var legacySkills = TryGetArray(skillsListResult, "skills") ?? TryGetArray(skillsListResult, "items");
        if (legacySkills is not null && legacySkills.Value.ValueKind == JsonValueKind.Array)
        {
            var skills = new List<SkillDescriptor>();
            foreach (var item in legacySkills.Value.EnumerateArray())
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
                    ShortDescription = GetStringOrNull(item, "shortDescription"),
                    Path = GetStringOrNull(item, "path"),
                    Enabled = GetBoolOrNull(item, "enabled"),
                    Scope = GetStringOrNull(item, "scope"),
                    Dependencies = TryGetObject(item, "dependencies"),
                    Interface = TryGetObject(item, "interface"),
                    Raw = item
                });
            }

            return
            [
                new SkillsListEntryResult
                {
                    Cwd = null,
                    Skills = skills,
                    Errors = Array.Empty<CodexSkillErrorInfo>(),
                    Raw = skillsListResult
                }
            ];
        }

        return Array.Empty<SkillsListEntryResult>();
    }

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(JsonElement skillsListResult)
    {
        var entries = ParseSkillsListEntries(skillsListResult);
        return ParseSkillsListSkills(entries);
    }

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(IReadOnlyList<SkillsListEntryResult> entries)
    {
        if (entries.Count == 0)
        {
            return Array.Empty<SkillDescriptor>();
        }

        return entries.SelectMany(e => e.Skills).ToArray();
    }

    internal static IReadOnlyList<AppDescriptor> ParseAppsListApps(JsonElement appsListResult)
    {
        var array =
            TryGetArray(appsListResult, "data") ??
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
                Description = GetStringOrNull(item, "description"),
                LogoUrl = GetStringOrNull(item, "logoUrl") ?? GetStringOrNull(item, "logo_url"),
                LogoUrlDark = GetStringOrNull(item, "logoUrlDark") ?? GetStringOrNull(item, "logo_url_dark"),
                DistributionChannel = GetStringOrNull(item, "distributionChannel"),
                InstallUrl = GetStringOrNull(item, "installUrl"),
                IsAccessible = GetBoolOrNull(item, "isAccessible"),
                IsEnabled = GetBoolOrNull(item, "isEnabled") ?? GetBoolOrNull(item, "enabled"),
                Title = GetStringOrNull(item, "title"),
                DisabledReason = GetStringOrNull(item, "disabledReason"),
                Raw = item
            });
        }

        return apps;
    }

    internal static IReadOnlyList<RemoteSkillDescriptor> ParseRemoteSkillsReadSkills(JsonElement remoteSkillsResult)
    {
        var array =
            TryGetArray(remoteSkillsResult, "data") ??
            TryGetArray(remoteSkillsResult, "skills");

        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RemoteSkillDescriptor>();
        }

        var skills = new List<RemoteSkillDescriptor>();
        foreach (var item in array.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetStringOrNull(item, "id");
            var name = GetStringOrNull(item, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            skills.Add(new RemoteSkillDescriptor
            {
                Id = id,
                Name = name,
                Description = GetStringOrNull(item, "description"),
                Raw = item
            });
        }

        return skills;
    }

    internal static ConfigRequirements? ParseConfigRequirementsReadRequirements(JsonElement configRequirementsReadResult, bool experimentalApiEnabled)
    {
        if (configRequirementsReadResult.ValueKind != JsonValueKind.Object ||
            !configRequirementsReadResult.TryGetProperty("requirements", out var req) ||
            req.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var allowedApprovalPolicies = GetOptionalStringArray(req, "allowedApprovalPolicies")
            ?.Select(CodexApprovalPolicy.Parse)
            .ToArray();

        var allowedSandboxModes = GetOptionalStringArray(req, "allowedSandboxModes")
            ?.Select(CodexSandboxMode.Parse)
            .ToArray();

        var allowedWebSearchModes = GetOptionalStringArray(req, "allowedWebSearchModes")
            ?.Select(CodexWebSearchMode.Parse)
            .ToArray();

        CodexResidencyRequirement? residency = null;
        if (CodexResidencyRequirement.TryParse(GetStringOrNull(req, "enforceResidency"), out var r))
        {
            residency = r;
        }

        NetworkRequirements? network = null;
        if (experimentalApiEnabled && TryGetObject(req, "network") is { } net)
        {
            network = ParseNetworkRequirements(net);
        }

        return new ConfigRequirements
        {
            AllowedApprovalPolicies = allowedApprovalPolicies,
            AllowedSandboxModes = allowedSandboxModes,
            AllowedWebSearchModes = allowedWebSearchModes,
            EnforceResidency = residency,
            Network = network,
            Raw = req.Clone()
        };
    }

    private static NetworkRequirements ParseNetworkRequirements(JsonElement network)
    {
        return new NetworkRequirements
        {
            Enabled = GetBoolOrNull(network, "enabled"),
            HttpPort = GetInt32OrNull(network, "httpPort"),
            SocksPort = GetInt32OrNull(network, "socksPort"),
            AllowUpstreamProxy = GetBoolOrNull(network, "allowUpstreamProxy"),
            DangerouslyAllowNonLoopbackProxy = GetBoolOrNull(network, "dangerouslyAllowNonLoopbackProxy"),
            DangerouslyAllowNonLoopbackAdmin = GetBoolOrNull(network, "dangerouslyAllowNonLoopbackAdmin"),
            AllowedDomains = GetOptionalStringArray(network, "allowedDomains"),
            DeniedDomains = GetOptionalStringArray(network, "deniedDomains"),
            AllowUnixSockets = GetOptionalStringArray(network, "allowUnixSockets"),
            AllowLocalBinding = GetBoolOrNull(network, "allowLocalBinding"),
            Raw = network.Clone()
        };
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
