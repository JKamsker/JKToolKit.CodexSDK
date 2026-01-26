using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NCodexSDK.AppServer.Notifications;
using NCodexSDK.Infrastructure.JsonRpc;
using NCodexSDK.Infrastructure.Stdio;
using NCodexSDK.Abstractions;
using NCodexSDK.Infrastructure;
using NCodexSDK.Public.Models;

namespace NCodexSDK.AppServer;

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

        var fileSystem = new RealFileSystem();
        var pathProviderLogger = loggerFactory.CreateLogger<DefaultCodexPathProvider>();
        ICodexPathProvider pathProvider = new DefaultCodexPathProvider(fileSystem, pathProviderLogger);

        var stdioFactory = new StdioProcessFactory(pathProvider, loggerFactory.CreateLogger<StdioProcessFactory>());

        var process = await stdioFactory.StartAsync(
            options.Launch,
            options.CodexExecutablePath,
            options.StartupTimeout,
            options.ShutdownTimeout,
            ct);

        var rpc = new JsonRpcConnection(
            reader: process.Stdout,
            writer: process.Stdin,
            includeJsonRpcHeader: false,
            notificationBufferCapacity: options.NotificationBufferCapacity,
            serializerOptions: options.SerializerOptionsOverride,
            logger: loggerFactory.CreateLogger<JsonRpcConnection>());

        var client = new CodexAppServerClient(options, process, rpc, logger);

        await client.InitializeAsync(options.DefaultClientInfo, ct);

        return client;
    }

    public async Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(clientInfo);

        var result = await _rpc.SendRequestAsync(
            "initialize",
            new
            {
                clientInfo = new { name = clientInfo.Name, title = clientInfo.Title, version = clientInfo.Version }
            },
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
            new
            {
                model = options.Model?.Value,
                cwd = options.Cwd,
                approvalPolicy = options.ApprovalPolicy?.Value,
                sandbox = options.Sandbox?.ToAppServerWireValue()
            },
            ct);

        var threadId = ExtractId(result, "id", "threadId") ?? string.Empty;
        return new CodexThread(threadId, result);
    }

    public async Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        var result = await _rpc.SendRequestAsync(
            "thread/resume",
            new { threadId },
            ct);

        var id = ExtractId(result, "id", "threadId") ?? threadId;
        return new CodexThread(id, result);
    }

    public async Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        ArgumentNullException.ThrowIfNull(options);

        var result = await _rpc.SendRequestAsync(
            "turn/start",
            new
            {
                threadId,
                input = options.Input.Select(i => new { type = i.Type, content = i.Content }).ToArray(),
                model = options.Model?.Value,
                effort = options.Effort?.Value,
                cwd = options.Cwd
            },
            ct);

        var turnId = ExtractId(result, "id", "turnId") ?? string.Empty;

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
        _rpc.SendRequestAsync("turn/interrupt", new { threadId, turnId }, ct);

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

    private static string? TryGetTurnId(AppServerNotification notification) =>
        notification switch
        {
            AgentMessageDeltaNotification d => d.TurnId,
            ItemStartedNotification s => s.TurnId,
            ItemCompletedNotification c => c.TurnId,
            TurnCompletedNotification t => t.TurnId,
            _ => null
        };
}
