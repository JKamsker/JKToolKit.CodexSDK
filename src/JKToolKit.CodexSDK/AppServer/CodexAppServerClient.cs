using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AppServer.Protocol.FuzzyFileSearch;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// A client for interacting with the Codex CLI "app-server" JSON-RPC interface.
/// </summary>
public sealed partial class CodexAppServerClient : IAsyncDisposable
{
    private readonly CodexAppServerClientCore _core;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly CodexAppServerThreadsClient _threadsClient;
    private readonly CodexAppServerSkillsAppsClient _skillsAppsClient;
    private readonly CodexAppServerConfigClient _configClient;
    private readonly CodexAppServerMcpClient _mcpClient;
    private readonly CodexAppServerPluginsClient _pluginsClient;
    private readonly CodexAppServerCommandExecClient _commandExecClient;
    private readonly CodexAppServerFilesystemClient _filesystemClient;
    private readonly CodexAppServerFuzzyFileSearchClient _fuzzyFileSearchClient;
    private readonly CodexAppServerTurnsClient _turnsClient;
    private readonly CodexAppServerCollaborationModesClient _collaborationModesClient;
    private readonly CodexAppServerReadOnlyAccessOverridesSupport _readOnlyAccessOverridesSupport = new();

    private bool ExperimentalApiEnabled => _core.ExperimentalApiEnabled;

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
        JsonSerializerOptions serializerOptions,
        bool startExitWatcher = true)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(rpc);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
        _core = new CodexAppServerClientCore(options, process, rpc, logger, startExitWatcher);

        Func<bool> experimentalApiEnabled = () => _core.ExperimentalApiEnabled;
        _threadsClient = new CodexAppServerThreadsClient(_core.SendRequestAsync, experimentalApiEnabled);
        _skillsAppsClient = new CodexAppServerSkillsAppsClient(_core.SendRequestAsync);
        _configClient = new CodexAppServerConfigClient(_core.SendRequestAsync, experimentalApiEnabled, logger);
        _mcpClient = new CodexAppServerMcpClient(_core.SendRequestAsync);
        _pluginsClient = new CodexAppServerPluginsClient(_core.SendRequestAsync);
        _commandExecClient = new CodexAppServerCommandExecClient(_core.SendRequestAsync);
        _filesystemClient = new CodexAppServerFilesystemClient(_core.SendRequestAsync);
        _fuzzyFileSearchClient = new CodexAppServerFuzzyFileSearchClient(_core.SendRequestAsync, experimentalApiEnabled);
        _collaborationModesClient = new CodexAppServerCollaborationModesClient(_core.SendRequestAsync, experimentalApiEnabled);
        _turnsClient = new CodexAppServerTurnsClient(
            options,
            _core.SendRequestAsync,
            initializeResult: () => _core.InitializeResult,
            registerTurnHandle: _core.RegisterTurnHandle,
            removeTurnHandle: _core.RemoveTurnHandle,
            readOnlyAccessOverridesSupport: _readOnlyAccessOverridesSupport,
            experimentalApiEnabled: experimentalApiEnabled);
    }

    internal CodexAppServerClient(
        CodexAppServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger logger,
        bool startExitWatcher = true)
        : this(options, process, rpc, logger, options?.SerializerOptionsOverride ?? CreateDefaultSerializerOptions(), startExitWatcher)
    {
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

        return await CreateInitializedAsync(options, process, rpc, logger, serializerOptions, ct).ConfigureAwait(false);
    }

    internal static async Task<CodexAppServerClient> CreateInitializedAsync(
        CodexAppServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger logger,
        JsonSerializerOptions serializerOptions,
        CancellationToken ct)
    {
        var client = new CodexAppServerClient(options, process, rpc, logger, serializerOptions);

        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        handshakeCts.CancelAfter(options.StartupTimeout);

        try
        {
            await client.InitializeAsync(options.DefaultClientInfo, handshakeCts.Token).ConfigureAwait(false);
            return client;
        }
        catch
        {
            try { await client.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
            throw;
        }
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
    public Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default) =>
        _core.InitializeAsync(clientInfo, ct);

    /// <summary>
    /// Gets the initialize result payload, if <see cref="InitializeAsync"/> has completed successfully.
    /// </summary>
    public AppServerInitializeResult? InitializeResult => _core.InitializeResult;

    /// <summary>
    /// A task that completes when the underlying Codex app-server subprocess exits.
    /// </summary>
    public Task ExitTask => _core.ExitTask;

    /// <summary>
    /// Subscribes to the global app-server notification stream.
    /// </summary>
    /// <remarks>
    /// This stream is backed by a bounded, drop-oldest queue. Each notification is delivered to at most one consumer.
    /// If you enumerate this stream multiple times concurrently, notifications will be distributed across readers
    /// (queue semantics), not broadcast (pub-sub).
    /// </remarks>
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _core.Notifications(ct);

    /// <summary>
    /// Subscribes to the global raw JSON-RPC notification stream (method + params).
    /// </summary>
    /// <remarks>
    /// This stream is backed by a bounded, drop-oldest queue. Each notification is delivered to at most one consumer.
    /// If you enumerate this stream multiple times concurrently, notifications will be distributed across readers
    /// (queue semantics), not broadcast (pub-sub).
    /// </remarks>
    public IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct = default) =>
        _core.NotificationsRaw(ct);

    /// <summary>
    /// Gets drop counters for bounded notification buffers.
    /// </summary>
    public AppServerNotificationDropStats NotificationDropStats => _core.NotificationDropStats;

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
    /// Reads a thread by ID with additional options.
    /// </summary>
    public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct = default) =>
        _threadsClient.ReadThreadAsync(threadId, options, ct);

    /// <summary>
    /// Lists identifiers of threads currently loaded in memory by the app-server.
    /// </summary>
    public Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct = default) =>
        _threadsClient.ListLoadedThreadsAsync(options, ct);

    /// <summary>
    /// Unsubscribes the current client from a thread without archiving it.
    /// </summary>
    /// <remarks>
    /// If the current client is the last subscriber, the server may unload the thread from memory.
    /// </remarks>
    public Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.UnsubscribeThreadAsync(threadId, ct);

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
    public Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.ArchiveThreadAsync(threadId, ct);

    /// <summary>
    /// Unarchives a thread.
    /// </summary>
    public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct = default) =>
        _threadsClient.UnarchiveThreadAsync(threadId, ct);

    /// <summary>
    /// Sets the thread name.
    /// </summary>
    public Task SetThreadNameAsync(string threadId, string name, CancellationToken ct = default) =>
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
    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(SkillsConfigWriteOptions options, CancellationToken ct = default) =>
        _configClient.WriteSkillsConfigAsync(options, ct);

    /// <summary>
    /// Performs a one-shot fuzzy file search request.
    /// </summary>
    public async Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(
        string query,
        IReadOnlyList<string> roots,
        string? cancellationToken = null,
        CancellationToken ct = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0)
        {
            throw new ArgumentException("Roots cannot be empty.", nameof(roots));
        }

        if (roots.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Roots cannot contain null, empty, or whitespace entries.", nameof(roots));
        }

        var response = await _core.SendRequestAsync(
            "fuzzyFileSearch",
            new FuzzyFileSearchParams
            {
                Query = query,
                Roots = roots.ToArray(),
                CancellationToken = string.IsNullOrWhiteSpace(cancellationToken) ? null : cancellationToken
            },
            ct);

        return AppServerNotificationParsing.ParseFuzzyFileSearchResults(response);
    }

    /// <summary>
    /// Writes skills configuration using a path-based selector.
    /// </summary>
    public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct = default) =>
        _configClient.WriteSkillsConfigAsync(
            new SkillsConfigWriteOptions
            {
                Enabled = enabled,
                Path = path
            },
            ct);

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
    public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default) =>
        _turnsClient.StartTurnAsync(threadId, options, ct);

    /// <summary>
    /// Steers an in-progress turn by appending input items.
    /// </summary>
    /// <remarks>
    /// Steering is best-effort and may race with turn completion. Cancellation stops waiting for the response but does
    /// not guarantee the server did not apply the steer request.
    /// </remarks>
    public Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default) =>
        _turnsClient.SteerTurnAsync(options, ct);

    /// <summary>
    /// Steers an in-progress turn by appending input items and returns the raw JSON result payload.
    /// </summary>
    /// <remarks>
    /// Steering is best-effort and may race with turn completion. Cancellation stops waiting for the response but does
    /// not guarantee the server did not apply the steer request.
    /// </remarks>
    public Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct = default) =>
        _turnsClient.SteerTurnRawAsync(options, ct);

    /// <summary>
    /// Starts a review via the app-server.
    /// </summary>
    public Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct = default) =>
        _turnsClient.StartReviewAsync(options, ct);

    /// <summary>
    /// Starts a review via the app-server.
    /// </summary>
    /// <remarks>
    /// This is an alias for <see cref="StartReviewAsync"/> to better align with exec-mode naming (<c>ReviewAsync</c>).
    /// </remarks>
    public Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct = default) =>
        _turnsClient.StartReviewAsync(options, ct);

    /// <summary>
    /// Disposes the underlying app-server connection and terminates the process.
    /// </summary>
    public ValueTask DisposeAsync() => _core.DisposeAsync();
}
