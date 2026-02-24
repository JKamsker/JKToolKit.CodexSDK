using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerOverridePipelineTests
{
    [Fact]
    public async Task SendRequestAsync_AppliesRequestTransformersInOrder_AndObserversSeeTransformedParams()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (_, _, _) => Task.FromResult(Parse("""{"ok":true}"""))
        };

        var t1 = new RecordingRequestTransformer((_, _) => Parse("""{"step":1}"""));
        var t2 = new RecordingRequestTransformer((_, p) =>
        {
            var step = p.GetProperty("step").GetInt32();
            return Parse($$"""{"step":{{step + 1}}}""");
        });

        var observer = new RecordingObserver();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            RequestParamsTransformers = new IAppServerRequestParamsTransformer[] { t1, t2 },
            MessageObservers = new IAppServerMessageObserver[] { observer }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        var result = await core.SendRequestAsync("demo/request", @params: null, CancellationToken.None);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        t1.Seen.Should().HaveCount(1);
        t1.Seen[0].EnumerateObject().Should().BeEmpty();

        t2.Seen.Should().HaveCount(1);
        t2.Seen[0].GetProperty("step").GetInt32().Should().Be(1);

        rpc.LastParams.Should().BeOfType<JsonElement>();
        ((JsonElement)rpc.LastParams!).GetProperty("step").GetInt32().Should().Be(2);

        observer.Requests.Should().HaveCount(1);
        observer.Requests[0].GetProperty("step").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task SendRequestAsync_AppliesResponseTransformersInOrder_AndObserversSeeTransformedResult()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (_, _, _) => Task.FromResult(Parse("""{"value":0}"""))
        };

        var t1 = new RecordingResponseTransformer((_, _) => Parse("""{"value":1}"""));
        var t2 = new RecordingResponseTransformer((_, r) =>
        {
            var v = r.GetProperty("value").GetInt32();
            return Parse($$"""{"value":{{v + 1}}}""");
        });

        var observer = new RecordingObserver();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            ResponseTransformers = new IAppServerResponseTransformer[] { t1, t2 },
            MessageObservers = new IAppServerMessageObserver[] { observer }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        var result = await core.SendRequestAsync("demo/response", @params: null, CancellationToken.None);
        result.GetProperty("value").GetInt32().Should().Be(2);

        t1.Seen.Should().HaveCount(1);
        t1.Seen[0].GetProperty("value").GetInt32().Should().Be(0);

        t2.Seen.Should().HaveCount(1);
        t2.Seen[0].GetProperty("value").GetInt32().Should().Be(1);

        observer.Responses.Should().HaveCount(1);
        observer.Responses[0].GetProperty("value").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task SendRequestAsync_SwallowsObserverExceptions()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (_, _, _) => Task.FromResult(Parse("""{"ok":true}"""))
        };

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            MessageObservers = new IAppServerMessageObserver[] { new ThrowingObserver() }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await core.SendRequestAsync("demo/observer", @params: null, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Notifications_UseTransformers_CustomMappersTakePrecedence_AndRawStreamSeesTransformedMethodAndParams()
    {
        var rpc = new FakeJsonRpcConnection();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            NotificationTransformers = new IAppServerNotificationTransformer[]
            {
                new NotificationTransformer((_, _) => ("custom/method", Parse("""{"x":1}""")))
            },
            NotificationMappers = new IAppServerNotificationMapper[]
            {
                new NotificationMapper((method, p) =>
                    method == "custom/method" ? new CustomNotification(method, p) : null)
            }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var rawTask = ReadOneAsync(core.NotificationsRaw(cts.Token), cts.Token);
        var mappedTask = ReadOneAsync(core.Notifications(cts.Token), cts.Token);

        await rpc.EmitNotificationAsync("orig/method", Parse("""{"ignored":true}"""));

        var raw = await rawTask;
        raw.Method.Should().Be("custom/method");
        raw.Params.GetProperty("x").GetInt32().Should().Be(1);

        var mapped = await mappedTask;
        mapped.Should().BeOfType<CustomNotification>();
        mapped.Method.Should().Be("custom/method");
        mapped.Params.GetProperty("x").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TurnHandle_EventsRaw_ReceivesTurnScopedRawNotifications()
    {
        var rpc = new FakeJsonRpcConnection();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            NotificationTransformers = new IAppServerNotificationTransformer[]
            {
                new NotificationTransformer((method, _) =>
                    (method, Parse("""{"threadId":"th","turn":{"id":"turn1"},"tag":"t"}""")))
            }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        await using var handle = new CodexTurnHandle(
            threadId: "th",
            turnId: "turn1",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            steerRaw: null,
            onDispose: () => { },
            bufferCapacity: 10);

        core.RegisterTurnHandle("turn1", handle);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var rawTask = ReadOneAsync(handle.EventsRaw(cts.Token), cts.Token);
        var typedTask = ReadOneAsync(handle.Events(cts.Token), cts.Token);

        await rpc.EmitNotificationAsync("turn/started", Parse("""{"threadId":"th","turn":{"id":"turn1"}}"""));

        var raw = await rawTask;
        raw.Method.Should().Be("turn/started");
        raw.Params.GetProperty("tag").GetString().Should().Be("t");

        var typed = await typedTask;
        typed.Method.Should().Be("turn/started");
    }

    [Fact]
    public async Task TurnHandle_Completes_When_CustomMappedTurnCompletedNotification_HasNoTurnId()
    {
        var rpc = new FakeJsonRpcConnection();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            NotificationMappers = new IAppServerNotificationMapper[]
            {
                new NotificationMapper((method, p) =>
                    method == "turn/completed" ? new CustomNotification(method, p) : null)
            }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        await using var handle = new CodexTurnHandle(
            threadId: "th",
            turnId: "turn1",
            interrupt: _ => Task.CompletedTask,
            steer: null,
            steerRaw: null,
            onDispose: () => { },
            bufferCapacity: 10);

        core.RegisterTurnHandle("turn1", handle);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var rawTask = ReadOneAsync(handle.EventsRaw(cts.Token), cts.Token);
        var typedTask = ReadOneAsync(handle.Events(cts.Token), cts.Token);

        await rpc.EmitNotificationAsync("turn/completed", Parse("""{"threadId":"th","turn":{"id":"turn1","status":"completed"}}"""));

        var raw = await rawTask;
        raw.Method.Should().Be("turn/completed");

        var typed = await typedTask;
        typed.Should().BeOfType<CustomNotification>();
        typed.Method.Should().Be("turn/completed");

        var completed = await handle.Completion.WaitAsync(cts.Token);
        completed.TurnId.Should().Be("turn1");
        completed.Status.Should().Be("completed");
    }

    [Fact]
    public async Task Notifications_SwallowObserverExceptions()
    {
        var rpc = new FakeJsonRpcConnection();

        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 10,
            MessageObservers = new IAppServerMessageObserver[] { new ThrowingObserver() }
        };

        await using var core = new CodexAppServerClientCore(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger.Instance,
            startExitWatcher: false);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var mappedTask = ReadOneAsync(core.Notifications(cts.Token), cts.Token);

        await rpc.EmitNotificationAsync("note", Parse("{}"));

        var mapped = await mappedTask;
        mapped.Method.Should().Be("note");
    }

    private static async Task<T> ReadOneAsync<T>(IAsyncEnumerable<T> stream, CancellationToken ct)
    {
        await using var e = stream.GetAsyncEnumerator(ct);
        if (!await e.MoveNextAsync())
        {
            throw new InvalidOperationException("Stream completed without yielding.");
        }

        return e.Current;
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class RecordingRequestTransformer : IAppServerRequestParamsTransformer
    {
        private readonly Func<string, JsonElement, JsonElement> _transform;
        public List<JsonElement> Seen { get; } = new();

        public RecordingRequestTransformer(Func<string, JsonElement, JsonElement> transform)
        {
            _transform = transform;
        }

        public JsonElement Transform(string method, JsonElement @params)
        {
            Seen.Add(@params);
            return _transform(method, @params);
        }
    }

    private sealed class RecordingResponseTransformer : IAppServerResponseTransformer
    {
        private readonly Func<string, JsonElement, JsonElement> _transform;
        public List<JsonElement> Seen { get; } = new();

        public RecordingResponseTransformer(Func<string, JsonElement, JsonElement> transform)
        {
            _transform = transform;
        }

        public JsonElement Transform(string method, JsonElement result)
        {
            Seen.Add(result);
            return _transform(method, result);
        }
    }

    private sealed class RecordingObserver : IAppServerMessageObserver
    {
        public List<JsonElement> Requests { get; } = new();
        public List<JsonElement> Responses { get; } = new();

        public void OnRequest(string method, JsonElement @params) => Requests.Add(@params);

        public void OnResponse(string method, JsonElement result) => Responses.Add(result);

        public void OnNotification(string method, JsonElement @params) { }
    }

    private sealed class ThrowingObserver : IAppServerMessageObserver
    {
        public void OnRequest(string method, JsonElement @params) => throw new InvalidOperationException("boom");

        public void OnResponse(string method, JsonElement result) => throw new InvalidOperationException("boom");

        public void OnNotification(string method, JsonElement @params) => throw new InvalidOperationException("boom");
    }

    private sealed class NotificationTransformer : IAppServerNotificationTransformer
    {
        private readonly Func<string, JsonElement, (string Method, JsonElement Params)> _transform;

        public NotificationTransformer(Func<string, JsonElement, (string Method, JsonElement Params)> transform)
        {
            _transform = transform;
        }

        public (string Method, JsonElement Params) Transform(string method, JsonElement @params) => _transform(method, @params);
    }

    private sealed class NotificationMapper : IAppServerNotificationMapper
    {
        private readonly Func<string, JsonElement, AppServerNotification?> _map;

        public NotificationMapper(Func<string, JsonElement, AppServerNotification?> map)
        {
            _map = map;
        }

        public AppServerNotification? TryMap(string method, JsonElement @params) => _map(method, @params);
    }

    private sealed record class CustomNotification(string Method, JsonElement Params) : AppServerNotification(Method, Params);

    private sealed class FakeJsonRpcConnection : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public object? LastParams { get; private set; }

        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; init; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastParams = @params;

            if (SendRequestAsyncImpl is null)
            {
                return Task.FromResult(Parse("""{"ok":true}"""));
            }

            return SendRequestAsyncImpl(method, @params, ct);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;

        public async ValueTask EmitNotificationAsync(string method, JsonElement? @params)
        {
            var handler = OnNotification;
            if (handler is null)
            {
                return;
            }

            await handler(new JsonRpcNotification(method, @params));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStdioProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;

        public int? ProcessId => 1;

        public int? ExitCode => 0;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
