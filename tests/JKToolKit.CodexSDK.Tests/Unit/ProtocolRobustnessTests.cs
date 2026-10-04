using System.Text;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ProtocolRobustnessTests
{
    [Fact]
    public void NotificationParser_DeterministicRandomShapes_DoNotThrow()
    {
        var random = new Random(43121);
        string[] methods = ["turn/completed", "item/completed", "thread/tokenUsage/updated", "turn/diff/updated", "item/agentMessage/delta", "unknown/future"];
        string[] keys = ["threadId", "turnId", "turn", "item", "id", "type", "status", "text", "delta", "tokenUsage"];
        for (var i = 0; i < 2000; i++)
        {
            object? Shape(int depth) => random.Next(depth > 3 ? 4 : 6) switch
            {
                0 => null, 1 => random.NextInt64(), 2 => "\ud83d\ude80\\\"\n", 3 => random.Next(2) == 0,
                4 => Enumerable.Range(0, random.Next(5)).Select(_ => Shape(depth + 1)).ToArray(),
                _ => keys.OrderBy(_ => random.Next()).Take(random.Next(keys.Length)).ToDictionary(k => k, _ => Shape(depth + 1))
            };
            var json = JsonSerializer.SerializeToElement(Shape(0));
            foreach (var method in methods)
                AppServerNotificationMapper.Map(method, json).Should().NotBeNull($"seed 43121, iteration {i}, method {method}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(97)]
    public async Task LineTransport_PreservesRandomlyFragmentedUnicodeFrames(int seed)
    {
        var frames = Enumerable.Range(0, 300).Select(i => JsonSerializer.Serialize(new { method = "note", @params = new { i, text = "🚀\nquote\"" } })).ToArray();
        using var stream = new FragmentedStream(Encoding.UTF8.GetBytes(string.Join("\r\n", frames) + "\n"), seed);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var transport = new LineJsonRpcMessageTransport(reader, TextWriter.Null);
        var actual = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var frame in transport.ReceiveAsync(timeout.Token)) actual.Add(frame);
        actual.Should().Equal(frames);
        transport.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    private sealed class FragmentedStream(byte[] bytes, int seed) : MemoryStream(bytes)
    {
        private readonly Random _random = new(seed);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, _random.Next(1, 24))], ct);
    }
}
