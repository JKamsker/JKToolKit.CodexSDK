using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer;

public sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly CodexAppServerClientOptions _options;
    private readonly ILogger _logger;
    private readonly StdioProcess _process;
    private readonly JsonRpcConnection _rpc;

    private readonly Channel<AppServerNotification> _globalNotifications;
    private readonly Dictionary<string, CodexTurnHandle> _turnsById = new(StringComparer.Ordinal);
    private int _disposed;

    internal CodexAppServerClient(
        CodexAppServerClientOptions options,
        StdioProcess process,
        JsonRpcConnection rpc,
        ILogger logger)
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
    }

    public static async Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var loggerFactory = NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger<CodexAppServerClient>();
        var serializerOptions = options.SerializerOptionsOverride ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

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

        await client.InitializeAsync(options.DefaultClientInfo, ct);

        return client;
    }

    private static CodexLaunch ApplyCodexHome(CodexLaunch launch, string? codexHomeDirectory)
    {
        if (string.IsNullOrWhiteSpace(codexHomeDirectory))
        {
            return launch;
        }

        return launch.WithEnvironment("CODEX_HOME", codexHomeDirectory);
    }

    public async Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(clientInfo);

        var result = await _rpc.SendRequestAsync(
            "initialize",
            new InitializeParams(clientInfo, Capabilities: null),
            ct);

        await _rpc.SendNotificationAsync("initialized", @params: null, ct);

        return new AppServerInitializeResult(result);
    }

    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _rpc.SendRequestAsync(method, @params, ct);

    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default) =>
        _globalNotifications.Reader.ReadAllAsync(ct);

    public async Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
            "thread/start",
            new ThreadStartParams(
                Model: options.Model?.Value,
                ModelProvider: options.ModelProvider,
                Cwd: options.Cwd,
                ApprovalPolicy: options.ApprovalPolicy?.Value,
                Sandbox: options.Sandbox?.ToAppServerWireValue(),
                Config: options.Config,
                BaseInstructions: options.BaseInstructions,
                DeveloperInstructions: options.DeveloperInstructions,
                Personality: options.Personality,
                Ephemeral: options.Ephemeral,
                ExperimentalRawEvents: options.ExperimentalRawEvents),
            ct);

        var threadId = ExtractThreadId(result);
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

        var result = await _rpc.SendRequestAsync(
            "thread/resume",
            new ThreadResumeParams(threadId),
            ct);

        var id = ExtractThreadId(result) ?? threadId;
        return new CodexThread(id, result);
    }

    public async Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));

        var result = await _rpc.SendRequestAsync(
            "thread/resume",
            new ThreadResumeParams(
                ThreadId: options.ThreadId,
                History: options.History,
                Path: options.Path,
                Model: options.Model?.Value,
                ModelProvider: options.ModelProvider,
                Cwd: options.Cwd,
                ApprovalPolicy: options.ApprovalPolicy?.Value,
                Sandbox: options.Sandbox?.ToAppServerWireValue(),
                Config: options.Config,
                BaseInstructions: options.BaseInstructions,
                DeveloperInstructions: options.DeveloperInstructions,
                Personality: options.Personality),
            ct);

        var id = ExtractThreadId(result) ?? options.ThreadId;
        return new CodexThread(id, result);
    }

    public async Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
            "turn/start",
            new TurnStartParams(
                ThreadId: threadId,
                Input: options.Input.Select(i => i.Wire).ToArray(),
                Cwd: options.Cwd,
                ApprovalPolicy: options.ApprovalPolicy?.Value,
                SandboxPolicy: options.SandboxPolicy,
                Model: options.Model?.Value,
                Effort: options.Effort?.Value,
                Summary: options.Summary,
                Personality: options.Personality,
                OutputSchema: options.OutputSchema,
                CollaborationMode: options.CollaborationMode),
            ct);

        var turnId = ExtractTurnId(result);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException(
                $"turn/start returned no turn id. Raw result: {result}");
        }

        var handle = new CodexTurnHandle(
            threadId,
            turnId,
            interrupt: c => InterruptAsync(threadId, turnId, c),
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
        _rpc.SendRequestAsync("turn/interrupt", new TurnInterruptParams(threadId, turnId), ct);

    private ValueTask OnRpcNotificationAsync(JsonRpcNotification notification)
    {
        var mapped = AppServerNotificationMapper.Map(notification.Method, notification.Params);

        _globalNotifications.Writer.TryWrite(mapped);

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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _globalNotifications.Writer.TryComplete();

        foreach (var handle in _turnsById.Values)
        {
            handle.EventsChannel.Writer.TryComplete();
            handle.CompletionTcs.TrySetCanceled();
        }

        await _rpc.DisposeAsync();
        await _process.DisposeAsync();
    }

    private static string? ExtractId(JsonElement element, params string[] propertyNames)
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

    private static string? ExtractThreadId(JsonElement result)
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

    private static string? ExtractTurnId(JsonElement result)
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

    private static string? ExtractIdByPath(JsonElement element, string p1, string p2)
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

    private static string? FindStringPropertyRecursive(JsonElement element, string propertyName, int maxDepth)
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
}
