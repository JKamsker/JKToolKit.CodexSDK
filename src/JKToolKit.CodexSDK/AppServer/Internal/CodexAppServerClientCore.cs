using System.Text.Json;
using System.Threading.Channels;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerClientCore : IAsyncDisposable
{
    private readonly CodexAppServerClientOptions _options;
    private readonly ILogger _logger;
    private readonly IStdioProcess _process;
    private readonly IJsonRpcConnection _rpc;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly CancellationToken _disposeToken;

    private readonly Channel<AppServerNotification> _globalNotifications;
    private readonly Dictionary<string, CodexTurnHandle> _turnsById = new(StringComparer.Ordinal);
    private int _disposed;
    private int _disconnectSignaled;
    private readonly Task _processExitWatcher;
    private AppServerInitializeResult? _initializeResult;

    public CodexAppServerClientCore(
        CodexAppServerClientOptions options,
        IStdioProcess process,
        IJsonRpcConnection rpc,
        ILogger logger,
        bool startExitWatcher)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _globalNotifications = Channel.CreateBounded<AppServerNotification>(new BoundedChannelOptions(options.NotificationBufferCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _rpc.OnNotification += OnRpcNotificationAsync;
        _rpc.OnServerRequest = OnRpcServerRequestAsync;

        _disposeToken = _disposeCts.Token;
        _processExitWatcher = startExitWatcher ? Task.Run(WatchProcessExitAsync) : Task.CompletedTask;
    }

    public bool ExperimentalApiEnabled => _options.Capabilities?.ExperimentalApi == true || _options.ExperimentalApi;

    public AppServerInitializeResult? InitializeResult => _initializeResult;

    public Task ExitTask => _process.Completion;

    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct) =>
        _globalNotifications.Reader.ReadAllAsync(ct);

    internal bool TryGetTurnHandle(string turnId, out CodexTurnHandle? handle)
    {
        lock (_turnsById)
        {
            return _turnsById.TryGetValue(turnId, out handle);
        }
    }

    internal void RegisterTurnHandle(string turnId, CodexTurnHandle handle)
    {
        lock (_turnsById)
        {
            _turnsById[turnId] = handle;
        }
    }

    internal void RemoveTurnHandle(string turnId)
    {
        lock (_turnsById)
        {
            _turnsById.Remove(turnId);
        }
    }

    private CodexTurnHandle[] SnapshotAndClearTurns()
    {
        lock (_turnsById)
        {
            var handles = _turnsById.Values.ToArray();
            _turnsById.Clear();
            return handles;
        }
    }

    public async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
    {
        try
        {
            return await _rpc.SendRequestAsync(method, @params, ct);
        }
        catch (JsonRpcRemoteException ex) when (CodexAppServerClient.TryParseExperimentalApiRequiredMessage(ex.Error.Message, out var descriptor))
        {
            throw new CodexExperimentalApiRequiredException(descriptor, ex);
        }
    }

    public async Task<AppServerInitializeResult> InitializeAsync(AppServerClientInfo clientInfo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clientInfo);

        var requestedCapabilities = CodexAppServerClient.BuildCapabilitiesFromOptions(_options);

        try
        {
            var result = await _rpc.SendRequestAsync(
                "initialize",
                CodexAppServerClient.BuildInitializeParams(clientInfo, requestedCapabilities),
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
            if (!string.IsNullOrWhiteSpace(dataJson))
            {
                dataJson = CodexDiagnosticsSanitizer.Sanitize(dataJson, maxChars: 2000);
            }

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
                stderrTail: CodexDiagnosticsSanitizer.SanitizeLines(_process.StderrTail, maxLines: 20, maxCharsPerLine: 400),
                innerException: ex);
        }
    }

    private ValueTask OnRpcNotificationAsync(JsonRpcNotification notification)
    {
        var mapped = AppServerNotificationMapper.Map(notification.Method, notification.Params);

        _globalNotifications.Writer.TryWrite(mapped);
        LogIfBogus(mapped);

        var turnId = TryGetTurnId(mapped);
        if (!string.IsNullOrWhiteSpace(turnId))
        {
            _ = TryGetTurnHandle(turnId, out var handle);

            if (handle is not null)
            {
                handle.EventsChannel.Writer.TryWrite(mapped);

                if (mapped is TurnCompletedNotification completed)
                {
                    handle.CompletionTcs.TrySetResult(completed);
                    handle.EventsChannel.Writer.TryComplete();
                    RemoveTurnHandle(turnId);
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
            var result = await handler.HandleAsync(req.Method, req.Params, _disposeToken);
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

        _disposeCts.Cancel();
        _globalNotifications.Writer.TryComplete();

        var handles = SnapshotAndClearTurns();

        foreach (var handle in handles)
        {
            handle.EventsChannel.Writer.TryComplete();
            handle.CompletionTcs.TrySetCanceled();
        }

        await _rpc.DisposeAsync();
        await _process.DisposeAsync();

        try { await _processExitWatcher.ConfigureAwait(false); } catch { /* ignore */ }
        try { _disposeCts.Dispose(); } catch { /* ignore */ }
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

        try
        {
            SignalDisconnect(BuildDisconnectException());
        }
        catch
        {
            SignalDisconnect(new CodexAppServerDisconnectedException(
                "Codex app-server subprocess disconnected.",
                processId: null,
                exitCode: null,
                stderrTail: Array.Empty<string>()));
        }
    }

    private Exception BuildDisconnectException()
    {
        var stderrTailRaw = Array.Empty<string>();
        try
        {
            stderrTailRaw = _process.StderrTail.ToArray();
        }
        catch
        {
            // ignore
        }

        var stderrTail = CodexDiagnosticsSanitizer.SanitizeLines(stderrTailRaw, maxLines: 20, maxCharsPerLine: 400);

        int? exitCode = null;
        int? pid = null;
        try { exitCode = _process.ExitCode; } catch { /* ignore */ }
        try { pid = _process.ProcessId; } catch { /* ignore */ }

        var msg = exitCode is null
            ? "Codex app-server subprocess disconnected."
            : $"Codex app-server subprocess exited with code {exitCode}.";

        // Note: include only redacted/truncated stderr snippets to reduce accidental leakage when exception messages are logged.
        if (stderrTail.Length > 0)
            msg += $" (stderr tail, redacted: {string.Join(" | ", stderrTail.TakeLast(5))})";

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

        var handles = SnapshotAndClearTurns();

        foreach (var handle in handles)
        {
            handle.EventsChannel.Writer.TryComplete(ex);
            handle.CompletionTcs.TrySetException(ex);
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

