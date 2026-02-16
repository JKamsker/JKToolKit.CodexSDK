using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Protocol;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal sealed class CodexAppServerTurnsClient
{
    private readonly CodexAppServerClientOptions _options;
    private readonly Func<string, object?, CancellationToken, Task<JsonElement>> _sendRequestAsync;
    private readonly Func<AppServerInitializeResult?> _initializeResult;
    private readonly Dictionary<string, CodexTurnHandle> _turnsById;
    private readonly CodexAppServerReadOnlyAccessOverridesSupport _readOnlyAccessOverridesSupport;
    private readonly bool _experimentalApiEnabled;

    public CodexAppServerTurnsClient(
        CodexAppServerClientOptions options,
        Func<string, object?, CancellationToken, Task<JsonElement>> sendRequestAsync,
        Func<AppServerInitializeResult?> initializeResult,
        Dictionary<string, CodexTurnHandle> turnsById,
        CodexAppServerReadOnlyAccessOverridesSupport readOnlyAccessOverridesSupport,
        bool experimentalApiEnabled)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _sendRequestAsync = sendRequestAsync ?? throw new ArgumentNullException(nameof(sendRequestAsync));
        _initializeResult = initializeResult ?? throw new ArgumentNullException(nameof(initializeResult));
        _turnsById = turnsById ?? throw new ArgumentNullException(nameof(turnsById));
        _readOnlyAccessOverridesSupport = readOnlyAccessOverridesSupport ?? throw new ArgumentNullException(nameof(readOnlyAccessOverridesSupport));
        _experimentalApiEnabled = experimentalApiEnabled;
    }

    public async Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(threadId));

        ArgumentNullException.ThrowIfNull(options);

        ExperimentalApiGuards.ValidateTurnStart(options, experimentalApiEnabled: _experimentalApiEnabled);

        if (ContainsReadOnlyAccessOverrides(options.SandboxPolicy) &&
            Volatile.Read(ref _readOnlyAccessOverridesSupport.Value) == -1)
        {
            var ua = _initializeResult()?.UserAgent ?? "<unknown userAgent>";
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
            result = await _sendRequestAsync("turn/start", turnStartParams, ct);
        }
        catch (JsonRpcRemoteException ex) when (ex.Error.Code == -32602 && ContainsReadOnlyAccessOverrides(options.SandboxPolicy))
        {
            Interlocked.Exchange(ref _readOnlyAccessOverridesSupport.Value, -1);
            var ua = _initializeResult()?.UserAgent ?? "<unknown userAgent>";
            var sandboxJson = JsonSerializer.Serialize(options.SandboxPolicy, CodexAppServerClient.CreateDefaultSerializerOptions());
            var data = ex.Error.Data is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined }
                ? $" Data: {ex.Error.Data.Value.GetRawText()}"
                : string.Empty;

            throw new InvalidOperationException(
                $"turn/start rejected sandboxPolicy parameters (likely unsupported by this Codex app-server build). userAgent='{ua}'. sandboxPolicy={sandboxJson}. Error: {ex.Error.Code}: {ex.Error.Message}.{data}",
                ex);
        }

        if (ContainsReadOnlyAccessOverrides(options.SandboxPolicy))
        {
            Interlocked.Exchange(ref _readOnlyAccessOverridesSupport.Value, 1);
        }

        var turnId = CodexAppServerClientJson.ExtractTurnId(result);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException(
                $"turn/start returned no turn id. Raw result: {result}");
        }

        return CreateTurnHandle(threadId, turnId);
    }

    public async Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct = default)
    {
        var result = await SteerTurnRawAsync(options, ct);
        return result.TurnId;
    }

    public async Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));
        if (string.IsNullOrWhiteSpace(options.ExpectedTurnId))
            throw new ArgumentException("ExpectedTurnId cannot be empty or whitespace.", nameof(options.ExpectedTurnId));

        try
        {
            var raw = await _sendRequestAsync(
                "turn/steer",
                CodexAppServerClient.BuildTurnSteerParams(options),
                ct);

            return new TurnSteerResult
            {
                TurnId = CodexAppServerClientJson.ExtractTurnId(raw) ?? options.ExpectedTurnId,
                Raw = raw
            };
        }
        catch (JsonRpcRemoteException ex)
        {
            var ua = _initializeResult()?.UserAgent;
            throw new CodexAppServerRequestFailedException(
                method: "turn/steer",
                errorCode: ex.Error.Code,
                errorMessage: $"{ex.Error.Message} (expectedTurnId='{options.ExpectedTurnId}')",
                errorData: ex.Error.Data,
                userAgent: ua,
                innerException: ex);
        }
    }

    public async Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.ThreadId))
            throw new ArgumentException("ThreadId cannot be empty or whitespace.", nameof(options.ThreadId));
        ArgumentNullException.ThrowIfNull(options.Target);

        JsonElement result;
        try
        {
            result = await _sendRequestAsync(
                "review/start",
                CodexAppServerClient.BuildReviewStartParams(options),
                ct);
        }
        catch (JsonRpcRemoteException ex)
        {
            var ua = _initializeResult()?.UserAgent;
            throw new CodexAppServerRequestFailedException(
                method: "review/start",
                errorCode: ex.Error.Code,
                errorMessage: ex.Error.Message,
                errorData: ex.Error.Data,
                userAgent: ua,
                innerException: ex);
        }

        var reviewThreadId = CodexAppServerClientJson.GetStringOrNull(result, "reviewThreadId");

        var turnObj = CodexAppServerClientJson.TryGetObject(result, "turn") ?? result;
        var turnId = CodexAppServerClientJson.ExtractTurnId(turnObj);
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new InvalidOperationException(
                $"review/start returned no turn id. Raw result: {result}");
        }

        var turnThreadId = CodexAppServerClientJson.ExtractThreadId(turnObj) ?? reviewThreadId ?? options.ThreadId;

        return new ReviewStartResult
        {
            Turn = CreateTurnHandle(turnThreadId, turnId),
            ReviewThreadId = reviewThreadId,
            Raw = result
        };
    }

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
        _sendRequestAsync(
            "turn/interrupt",
            new TurnInterruptParams { ThreadId = threadId, TurnId = turnId },
            ct);

    private static bool ContainsReadOnlyAccessOverrides(SandboxPolicy? policy) =>
        policy switch
        {
            SandboxPolicy.ReadOnly r => r.Access is not null,
            SandboxPolicy.WorkspaceWrite w => w.ReadOnlyAccess is not null,
            _ => false
        };
}
