using System.IO.Pipes;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class JsonRpcConnectionTests
{
    [Fact]
    public async Task SendRequestAsync_CorrelatesResponse()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            reqDoc.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
            reqDoc.RootElement.GetProperty("method").GetString().Should().Be("ping");
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        await serverTask;
    }

    [Fact]
    public async Task Notifications_AreDispatched()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var onNotificationTcs = new TaskCompletionSource<JsonRpcNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.OnNotification += n =>
        {
            onNotificationTcs.TrySetResult(n);
            return ValueTask.CompletedTask;
        };

        await harness.ServerWriter.WriteLineAsync(
            JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "note", @params = new { message = "hi" } }));

        var fromEvent = await onNotificationTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        fromEvent.Method.Should().Be("note");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var note in rpc.Notifications(cts.Token))
        {
            note.Method.Should().Be("note");
            break;
        }
    }

    [Fact]
    public async Task ServerRequests_AreHandled_AndResponded()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        rpc.OnServerRequest = req =>
        {
            using var doc = JsonDocument.Parse("""{"approved":true}""");
            return ValueTask.FromResult(new JsonRpcResponse(req.Id, doc.RootElement.Clone(), Error: null));
        };

        var serverTask = Task.Run(async () =>
        {
            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 42, method = "approval/request", @params = new { } }));

            var responseLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            responseLine.Should().NotBeNull();

            using var respDoc = JsonDocument.Parse(responseLine!);
            respDoc.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
            respDoc.RootElement.GetProperty("id").GetInt32().Should().Be(42);
            respDoc.RootElement.TryGetProperty("error", out _).Should().BeFalse();
            respDoc.RootElement.GetProperty("result").GetProperty("approved").GetBoolean().Should().BeTrue();
        });

        await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task InvalidJson_IsIgnored_AndConnectionContinues()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            await harness.ServerWriter.WriteLineAsync("not-json");

            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        await serverTask;
    }

    [Fact]
    public async Task BogusNotifications_AreDropped_WithoutFaulting()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", method = 123, @params = new { } }));
            await harness.ServerWriter.WriteLineAsync("""{"jsonrpc":"2.0","method":"note","params":{"ok":true}}""");

            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();
            using var doc = JsonDocument.Parse(line!);
            doc.RootElement.GetProperty("method").GetString().Should().Be("still-alive");
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var note in rpc.Notifications(cts.Token))
        {
            note.Method.Should().Be("note");
            break;
        }

        // should not fault the connection
        using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await rpc.SendNotificationAsync("still-alive", @params: null, sendCts.Token);

        await serverTask;
    }

    [Fact]
    public async Task ResponseWithNonStringErrorMessage_CompletesRequestWithRemoteException()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code = -32601, message = 123 } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var act = async () => await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        var ex = await act.Should().ThrowAsync<JsonRpcRemoteException>();
        ex.Which.Error.Code.Should().Be(-32601);
        ex.Which.Error.Message.Should().Be("Remote error");

        await serverTask;
    }

    [Fact]
    public async Task ResponseForUnknownId_IsIgnored_AndPendingRequestStillCompletes()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id + 1, result = new { ok = false } }));
            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        await serverTask;
    }

    [Fact]
    public async Task NotificationHandlerThrows_IsIgnored_AndConnectionContinues()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        rpc.OnNotification += _ => throw new InvalidOperationException("boom");

        var serverTask = Task.Run(async () =>
        {
            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "note", @params = new { ok = true } }));

            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        await serverTask;
    }

    [Fact]
    public async Task ServerRequestHandlerThrows_ReturnsErrorResponse_AndConnectionContinues()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        rpc.OnServerRequest = _ => throw new InvalidOperationException("kaboom");

        var serverTask = Task.Run(async () =>
        {
            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 42, method = "approval/request", @params = new { } }));

            // The client may send its own request before responding to the server request.
            // Read lines until we find the response (no "method" property).
            string? pendingClientRequestLine = null;
            while (true)
            {
                var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
                line.Should().NotBeNull();

                using var doc = JsonDocument.Parse(line!);
                var root = doc.RootElement;
                if (root.TryGetProperty("method", out _))
                {
                    pendingClientRequestLine ??= line;
                    continue;
                }

                root.GetProperty("id").GetInt32().Should().Be(42);
                root.GetProperty("jsonrpc").GetString().Should().Be("2.0");
                var err = root.GetProperty("error");
                err.GetProperty("code").GetInt32().Should().Be(-32000);
                err.GetProperty("message").GetString().Should().Be("kaboom");
                root.TryGetProperty("result", out _).Should().BeFalse();
                break;
            }

            pendingClientRequestLine ??= await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            pendingClientRequestLine.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(pendingClientRequestLine!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(
                JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        await serverTask;
    }

    [Fact]
    public async Task SendNotificationAsync_WhenParamsNull_OmitsParamsProperty()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            using var doc = JsonDocument.Parse(line!);
            doc.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
            doc.RootElement.GetProperty("method").GetString().Should().Be("initialized");
            doc.RootElement.TryGetProperty("params", out _).Should().BeFalse();
        });

        await rpc.SendNotificationAsync("initialized", @params: null, CancellationToken.None);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SlowServerRequestHandler_DoesNotStallResponseCorrelation()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.OnServerRequest = async req =>
        {
            handlerStarted.TrySetResult();
            await Task.Delay(500);
            using var doc = JsonDocument.Parse("""{"ok":true}""");
            return new JsonRpcResponse(req.Id, doc.RootElement.Clone(), Error: null);
        };

        var serverTask = Task.Run(async () =>
        {
            // Send a server request that triggers a slow handler.
            await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method = "srv/slow" }));

            // Read the client's ping request and respond quickly.
            var pingLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            pingLine.Should().NotBeNull();
            using var pingDoc = JsonDocument.Parse(pingLine!);
            pingDoc.RootElement.GetProperty("method").GetString().Should().Be("ping");
            var pingId = pingDoc.RootElement.GetProperty("id").GetInt64();

            await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = pingId, result = new { ok = true } }));

            // Eventually, the client responds to the server request.
            while (true)
            {
                var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                line.Should().NotBeNull();

                using var doc = JsonDocument.Parse(line!);
                if (doc.RootElement.TryGetProperty("method", out _))
                {
                    continue;
                }

                doc.RootElement.GetProperty("id").GetInt32().Should().Be(1);
                doc.RootElement.GetProperty("result").GetProperty("ok").GetBoolean().Should().BeTrue();
                break;
            }
        });

        await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        sw.Stop();

        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(400));

        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCorruptTheWire()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var requestLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            requestLine.Should().NotBeNull();
            using var _ = JsonDocument.Parse(requestLine!);

            // Delay the response past the client's cancellation.
            await Task.Delay(800);

            using var reqDoc = JsonDocument.Parse(requestLine!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();
            await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result = new { ok = true } }));

            // Client may send a best-effort cancellation notification, and then later send an unrelated notification.
            for (var i = 0; i < 2; i++)
            {
                var notificationLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
                notificationLine.Should().NotBeNull();
                using var __ = JsonDocument.Parse(notificationLine!);
            }
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = async () => await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        await rpc.SendNotificationAsync("note", @params: null, CancellationToken.None);

        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelledRequest_EmitsCancelledNotification_BestEffort()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var requestIdTcs = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverTask = Task.Run(async () =>
        {
            var requestLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            requestLine.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(requestLine!);
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();
            requestIdTcs.TrySetResult(id);

            var cancelLine = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            cancelLine.Should().NotBeNull();

            using var cancelDoc = JsonDocument.Parse(cancelLine!);
            cancelDoc.RootElement.GetProperty("method").GetString().Should().Be("notifications/cancelled");
            cancelDoc.RootElement.GetProperty("params").GetProperty("requestId").GetInt64().Should().Be(id);
        });

        using var cts = new CancellationTokenSource();
        var sendTask = rpc.SendRequestAsync("tools/call", @params: new { name = "codex", arguments = new { } }, cts.Token);

        await requestIdTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        var act = async () => await sendTask;
        await act.Should().ThrowAsync<OperationCanceledException>();

        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task NotificationBufferDropsOldest_AndConnectionRemainsUsable()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 1,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var gotN3Tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.OnNotification += n =>
        {
            if (n.Method == "n3")
                gotN3Tcs.TrySetResult();
            return ValueTask.CompletedTask;
        };

        await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "n1" }));
        await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "n2" }));
        await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "n3" }));

        await gotN3Tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var note in rpc.Notifications(cts.Token))
        {
            note.Method.Should().Be("n3");
            break;
        }

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();
            using var doc = JsonDocument.Parse(line!);
            doc.RootElement.GetProperty("method").GetString().Should().Be("still-alive");
        });

        using var sendCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await rpc.SendNotificationAsync("still-alive", @params: null, sendCts.Token);

        await serverTask;
    }

    [Fact]
    public async Task RemoteClose_FaultsConnection_AndFailsPendingRequests()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(2));
            line.Should().NotBeNull();

            harness.CloseServer();
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var act = async () => await rpc.SendRequestAsync("ping", @params: null, cts.Token);
        await act.Should().ThrowAsync<JsonRpcConnectionClosedException>();

        await serverTask;
    }

    [Fact]
    public async Task ConcurrentOutboundWrites_DoNotCorruptJsonLines()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        rpc.OnServerRequest = req =>
        {
            using var doc = JsonDocument.Parse("""{"ok":true}""");
            return ValueTask.FromResult(new JsonRpcResponse(req.Id, doc.RootElement.Clone(), Error: null));
        };

        const int notificationCount = 200;
        const int serverRequestCount = 50;
        var expectedClientLines = notificationCount + serverRequestCount;

        var readTask = Task.Run(async () =>
        {
            var lines = new List<string>(capacity: expectedClientLines);

            while (lines.Count < expectedClientLines)
            {
                var line = await harness.ServerReader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));
                line.Should().NotBeNull();
                lines.Add(line!);

                using var _ = JsonDocument.Parse(line!);
            }
        });

        var sendServerRequestsTask = Task.Run(async () =>
        {
            for (var i = 0; i < serverRequestCount; i++)
            {
                await harness.ServerWriter.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = i,
                    method = "srv/request",
                    @params = new { i }
                }));
            }
        });

        var sendNotificationsTask = Task.WhenAll(Enumerable.Range(0, notificationCount).Select(i =>
            rpc.SendNotificationAsync("note", new { i }, CancellationToken.None)));

        await Task.WhenAll(sendNotificationsTask, sendServerRequestsTask);
        await readTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class PipeHarness : IAsyncDisposable
    {
        private readonly NamedPipeServerStream _server;
        private readonly NamedPipeClientStream _client;

        public StreamReader ClientReader { get; }
        public StreamWriter ClientWriter { get; }
        public StreamReader ServerReader { get; }
        public StreamWriter ServerWriter { get; }

        private PipeHarness(NamedPipeServerStream server, NamedPipeClientStream client)
        {
            _server = server;
            _client = client;

            ClientReader = new StreamReader(_client);
            ClientWriter = new StreamWriter(_client) { AutoFlush = true };

            ServerReader = new StreamReader(_server);
            ServerWriter = new StreamWriter(_server) { AutoFlush = true };
        }

        public void CloseServer()
        {
            try { ServerReader.Dispose(); } catch { }
            try { ServerWriter.Dispose(); } catch { }
            try { _server.Dispose(); } catch { }
        }

        public static async Task<PipeHarness> CreateAsync()
        {
            var name = $"ncodexsdk-jsonrpc-{Guid.NewGuid():N}";

            var server = new NamedPipeServerStream(
                name,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            var client = new NamedPipeClientStream(
                ".",
                name,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            var serverWait = server.WaitForConnectionAsync();
            await client.ConnectAsync(5000);
            await serverWait;

            return new PipeHarness(server, client);
        }

        public ValueTask DisposeAsync()
        {
            try { ClientReader.Dispose(); } catch { }
            try { ClientWriter.Dispose(); } catch { }
            try { ServerReader.Dispose(); } catch { }
            try { ServerWriter.Dispose(); } catch { }

            try { _client.Dispose(); } catch { }
            try { _server.Dispose(); } catch { }

            return ValueTask.CompletedTask;
        }
    }
}
