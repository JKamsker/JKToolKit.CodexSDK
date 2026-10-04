using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Remote.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteAppServerHealthProbeTests
{
    [Theory]
    [InlineData(200, true, false)]
    [InlineData(503, false, true)]
    public async Task Probe_UsesReadyRouteAndHttpStatus(int status, bool expected, bool infiniteTimeout)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var served = ServeAsync();
        var result = await new RemoteAppServerHealthProbe().IsReadyAsync(new Uri($"ws://127.0.0.1:{port}/socket?secret=value#fragment"), infiniteTimeout ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(3), cancellation.Token);
        result.Should().Be(expected);
        (await served).Should().Be("GET /readyz HTTP/1.1");

        async Task<string?> ServeAsync()
        {
            using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            var request = await reader.ReadLineAsync(cancellation.Token);
            while (!string.IsNullOrEmpty(await reader.ReadLineAsync(cancellation.Token))) { }
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"), cancellation.Token);
            return request;
        }
    }

    [Theory]
    [InlineData("ws")]
    [InlineData("wss")]
    public async Task Probe_CancelledRequestReturnsNotReady(string scheme)
    {
        var result = await new RemoteAppServerHealthProbe().IsReadyAsync(new Uri($"{scheme}://127.0.0.1:1/socket"), TimeSpan.FromSeconds(1), new CancellationToken(true));
        result.Should().BeFalse();
    }
}
