using System.Text.Json;
using System.Threading.Channels;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class JsonRpcStateFuzzTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(7)]
    [InlineData(43121)]
    public async Task ErrorEnvelopeShapes_AlwaysSettleTheirCorrelatedRequest(int seed)
    {
        var random = new Random(seed);
        object?[] codes = [null, false, "not-a-number", new[] { 1 }, new { nested = 1 }, 1e100, -32602];
        var wire = new ScriptedTransport();
        await using var rpc = new JsonRpcConnection(wire, true, 8, null, NullLogger.Instance);
        for (var iteration = 0; iteration < TurnStateFuzzTests.Cases; iteration++)
        {
            var pending = rpc.SendRequestAsync("fuzz/error", null, default);
            using var request = JsonDocument.Parse(await wire.Outbound.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            var code = codes[iteration < codes.Length ? iteration : random.Next(codes.Length)];
            var response = JsonSerializer.Serialize(new
            {
                id = request.RootElement.GetProperty("id").GetInt64(),
                error = new { code, message = codes[random.Next(codes.Length)], data = new { iteration } }
            });
            wire.Inbound.Writer.TryWrite(response);
            var error = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(error is JsonRpcRemoteException, $"seed={seed}, case={iteration}, response={response}; request stranded or wrong failure: {error}");
            Assert.Equal(code is int ? -32602 : -32000, ((JsonRpcRemoteException)error!).Error.Code);
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(97)]
    [InlineData(43121)]
    [InlineData(8675309)]
    public async Task ReplyPermutations_CancellationDuplicatesAndUnknownIdsRespectRequestIdentity(int seed)
    {
        var random = new Random(seed);
        var rounds = Math.Max(1, TurnStateFuzzTests.Cases / 5);
        for (var round = 0; round < rounds; round++)
        {
            var wire = new ScriptedTransport();
            await using var rpc = new JsonRpcConnection(wire, true, 8, null, NullLogger.Instance);
            var cancellation = Enumerable.Range(0, 16).Select(_ => new CancellationTokenSource()).ToArray();
            try
            {
                var pending = cancellation.Select((source, index) => rpc.SendRequestAsync("fuzz/request", new { index }, source.Token)).ToArray();
                var ids = new long[16];
                for (var i = 0; i < ids.Length; i++)
                {
                    using var sent = JsonDocument.Parse(await wire.Outbound.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
                    ids[sent.RootElement.GetProperty("params").GetProperty("index").GetInt32()] = sent.RootElement.GetProperty("id").GetInt64();
                }
                var canceled = new bool[16];
                var remoteErrors = new bool[16];
                for (var i = 0; i < pending.Length; i++)
                    if (random.Next(4) == 0)
                    {
                        canceled[i] = true;
                        await cancellation[i].CancelAsync();
                        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending[i].WaitAsync(TimeSpan.FromSeconds(5)));
                    }
                var permutation = Enumerable.Range(0, 16).OrderBy(_ => random.Next()).ToArray();
                foreach (var index in permutation)
                {
                    wire.Inbound.Writer.TryWrite($"{{\"id\":{ids[index] + 100000},\"result\":\"unknown\"}}");
                    remoteErrors[index] = random.Next(4) == 0;
                    var response = remoteErrors[index]
                        ? $"{{\"id\":{ids[index]},\"error\":{{\"code\":-32602,\"message\":\"rejected-{index}\"}}}}"
                        : $"{{\"id\":{ids[index]},\"result\":{{\"index\":{index}}}}}";
                    wire.Inbound.Writer.TryWrite(response);
                    wire.Inbound.Writer.TryWrite($"{{\"id\":{ids[index]},\"result\":{{\"index\":-1}}}}");
                }
                for (var i = 0; i < pending.Length; i++)
                {
                    var error = await Record.ExceptionAsync(() => pending[i].WaitAsync(TimeSpan.FromSeconds(5)));
                    var context = $"seed={seed}, round={round}, request={i}, order={string.Join(',', permutation)}";
                    if (canceled[i]) Assert.True(error is OperationCanceledException, context);
                    else if (remoteErrors[i]) Assert.True(error is JsonRpcRemoteException remote && remote.Error.Message == $"rejected-{i}", context);
                    else
                    {
                        Assert.True(error is null, context + "; " + error);
                        Assert.Equal(i, (await pending[i]).GetProperty("index").GetInt32());
                    }
                }
            }
            finally { foreach (var source in cancellation) source.Dispose(); }
        }
        output.WriteLine($"seed={seed}; connections={rounds}; correlated requests={rounds * 16}");
    }

    [Theory]
    [InlineData(19)]
    [InlineData(43121)]
    public async Task CorruptFramesDoNotPoisonReplies_AndEofSettlesRemainingRequests(int seed)
    {
        var random = new Random(seed);
        for (var round = 0; round < TurnStateFuzzTests.Cases; round++)
        {
            var wire = new ScriptedTransport();
            await using var rpc = new JsonRpcConnection(wire, true, 8, null, NullLogger.Instance);
            var pending = Enumerable.Range(0, random.Next(1, 12)).Select(_ => rpc.SendRequestAsync("fuzz/request", null, default)).ToArray();
            var ids = new List<long>();
            for (var i = 0; i < pending.Length; i++)
            {
                using var sent = JsonDocument.Parse(await wire.Outbound.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
                ids.Add(sent.RootElement.GetProperty("id").GetInt64());
            }
            var original = JsonSerializer.Serialize(new { id = ids[0], result = "🚀 utf8 text" });
            var cut = random.Next(1, original.Length);
            var corrupt = original[..cut];
            Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(corrupt));
            wire.Inbound.Writer.TryWrite(corrupt);
            var successful = random.Next(pending.Length + 1);
            for (var i = 0; i < successful; i++) wire.Inbound.Writer.TryWrite($"{{\"id\":{ids[i]},\"result\":{i}}}");
            wire.Inbound.Writer.TryComplete();
            for (var i = 0; i < pending.Length; i++)
            {
                var error = await Record.ExceptionAsync(() => pending[i].WaitAsync(TimeSpan.FromSeconds(5)));
                if (i < successful)
                {
                    Assert.Null(error);
                    Assert.Equal(i, (await pending[i]).GetInt32());
                }
                else Assert.True(error is not null and not TimeoutException, $"seed={seed}, round={round}: pending request did not fault");
            }
            var future = await Record.ExceptionAsync(() => rpc.SendRequestAsync("future", null, default).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(future is not null and not TimeoutException, $"seed={seed}, round={round}: faulted connection accepted a new request");
        }
    }

    private sealed class ScriptedTransport : IJsonRpcMessageTransport
    {
        public Channel<string> Inbound { get; } = Channel.CreateUnbounded<string>();
        public Channel<string> Outbound { get; } = Channel.CreateUnbounded<string>();
        public Task Completion => Inbound.Reader.Completion;
        public Task SendAsync(string message, CancellationToken ct) { Outbound.Writer.TryWrite(message); return Task.CompletedTask; }
        public IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct) => Inbound.Reader.ReadAllAsync(ct);
        public ValueTask DisposeAsync() { Inbound.Writer.TryComplete(); Outbound.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
