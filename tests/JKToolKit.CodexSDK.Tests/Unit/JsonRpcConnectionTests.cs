using System.IO.Pipes;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

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

        var result = await rpc.SendRequestAsync("ping", @params: null, CancellationToken.None);
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
            respDoc.RootElement.GetProperty("id").GetInt32().Should().Be(42);
            respDoc.RootElement.GetProperty("result").GetProperty("approved").GetBoolean().Should().BeTrue();
        });

        await serverTask.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task InvalidJson_PropagatesProtocolError_ToPendingRequests()
    {
        await using var harness = await PipeHarness.CreateAsync();

        await using var rpc = new JsonRpcConnection(
            reader: harness.ClientReader,
            writer: harness.ClientWriter,
            includeJsonRpcHeader: true,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        await harness.ServerWriter.WriteLineAsync("not-json");

        var act = async () => await rpc.SendRequestAsync("ping", @params: null, CancellationToken.None);
        await act.Should().ThrowAsync<JsonRpcProtocolException>();
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
