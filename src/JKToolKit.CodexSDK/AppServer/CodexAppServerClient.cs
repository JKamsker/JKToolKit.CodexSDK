using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
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
public sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly CodexAppServerClientCore _core;
    private readonly CodexAppServerThreadsClient _threadsClient;
    private readonly CodexAppServerSkillsAppsClient _skillsAppsClient;
    private readonly CodexAppServerConfigClient _configClient;
    private readonly CodexAppServerMcpClient _mcpClient;
    private readonly CodexAppServerFuzzyFileSearchClient _fuzzyFileSearchClient;
    private readonly CodexAppServerTurnsClient _turnsClient;
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
        bool startExitWatcher = true)
    {
        _core = new CodexAppServerClientCore(options, process, rpc, logger, startExitWatcher);

        Func<bool> experimentalApiEnabled = () => _core.ExperimentalApiEnabled;
        _threadsClient = new CodexAppServerThreadsClient(_core.SendRequestAsync, experimentalApiEnabled);
        _skillsAppsClient = new CodexAppServerSkillsAppsClient(_core.SendRequestAsync);
        _configClient = new CodexAppServerConfigClient(_core.SendRequestAsync, experimentalApiEnabled);
        _mcpClient = new CodexAppServerMcpClient(_core.SendRequestAsync);
        _fuzzyFileSearchClient = new CodexAppServerFuzzyFileSearchClient(_core.SendRequestAsync, experimentalApiEnabled);
        _turnsClient = new CodexAppServerTurnsClient(
            options,
            _core.SendRequestAsync,
            initializeResult: () => _core.InitializeResult,
            registerTurnHandle: _core.RegisterTurnHandle,
            removeTurnHandle: _core.RemoveTurnHandle,
            readOnlyAccessOverridesSupport: _readOnlyAccessOverridesSupport,
            experimentalApiEnabled: experimentalApiEnabled);
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
    public Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default) =>
        _core.InitializeAsync(clientInfo, ct);

    /// <summary>
    /// Gets the initialize result payload, if <see cref="InitializeAsync"/> has completed successfully.
    /// </summary>
    public AppServerInitializeResult? InitializeResult => _core.InitializeResult;

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _core.SendRequestAsync(method, @params, ct);

    /// <summary>
    /// A task that completes when the underlying Codex app-server subprocess exits.
    /// </summary>
    public Task ExitTask => _core.ExitTask;

    /// <summary>
    /// Subscribes to the global app-server notification stream.
    /// </summary>
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _core.Notifications(ct);

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
    /// Reloads MCP server configuration from disk and queues a refresh for loaded threads.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>config/mcpServer/reload</c>.
    /// Refresh is applied on each thread's next active turn.
    /// </remarks>
    public Task ReloadMcpServersAsync(CancellationToken ct = default) =>
        _mcpClient.ReloadMcpServersAsync(ct);

    /// <summary>
    /// Lists MCP servers with their tools/resources and auth status.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServerStatus/list</c>.
    /// </remarks>
    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct = default) =>
        _mcpClient.ListMcpServerStatusAsync(options, ct);

    /// <summary>
    /// Starts an OAuth login flow for a configured MCP server.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServer/oauth/login</c>.
    /// The server later emits <c>mcpServer/oauthLogin/completed</c>.
    /// </remarks>
    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default) =>
        _mcpClient.StartMcpServerOauthLoginAsync(options, ct);

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

    /// <summary>
    /// Disposes the underlying app-server connection and terminates the process.
    /// </summary>
    public ValueTask DisposeAsync() => _core.DisposeAsync();

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
}
