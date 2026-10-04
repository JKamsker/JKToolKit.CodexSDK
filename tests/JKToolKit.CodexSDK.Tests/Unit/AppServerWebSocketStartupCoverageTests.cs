using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerWebSocketStartupCoverageTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task PublicStartup_InitializesWebSocketAndPreservesCallerOptions(bool useConvenienceMethod, bool customSerializer)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var uri = new Uri($"ws://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/app-server");
        var options = new CodexAppServerClientOptions
        {
            StartupTimeout = TimeSpan.FromSeconds(5),
            SerializerOptionsOverride = customSerializer ? new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower } : null
        };
        var server = ServeAsync(listener, async (socket, headers, ct) =>
        {
            headers.Should().Contain("Authorization: Bearer local-test-token");
            var initialize = await ReceiveJsonAsync(socket, ct);
            initialize.GetProperty("method").GetString().Should().Be("initialize");
            initialize.TryGetProperty("jsonrpc", out _).Should().BeFalse();
            initialize.GetProperty("params").GetProperty("clientInfo").GetProperty("name").GetString().Should().NotBeNullOrEmpty();
            await ReplyAsync(socket, initialize, new { userAgent = "loopback-server" }, ct);
            var initialized = await ReceiveJsonAsync(socket, ct);
            initialized.GetProperty("method").GetString().Should().Be("initialized");
            initialized.TryGetProperty("id", out _).Should().BeFalse();
            var request = await ReceiveJsonAsync(socket, ct);
            request.GetProperty("method").GetString().Should().Be("custom/read");
            request.GetProperty("params").GetProperty(customSerializer ? "sample_value" : "sampleValue").GetInt32().Should().Be(42);
            await ReplyAsync(socket, request, new { echoed = 42 }, ct);
            await ObserveDisconnectAsync(socket, ct);
        }, deadline.Token);
        try
        {
            CodexAppServerClient client;
            if (useConvenienceMethod)
            {
                client = await CodexAppServerClient.ConnectWebSocketAsync(new()
                {
                    Uri = uri, BearerToken = "local-test-token", ClientOptions = options
                }, deadline.Token);
                options.Endpoint.Should().BeNull("the convenience method clones the supplied client options");
            }
            else
            {
                options.Endpoint = new CodexAppServerWebSocketEndpoint(uri, "local-test-token");
                client = await CodexAppServerClient.StartAsync(options, deadline.Token);
            }
            await using (client)
            {
                client.InitializeResult.Should().NotBeNull();
                var response = await client.CallAsync("custom/read", new { SampleValue = 42 }, deadline.Token);
                response.GetProperty("echoed").GetInt32().Should().Be(42);
            }
            await server.WaitAsync(deadline.Token);
        }
        finally
        {
            await deadline.CancelAsync();
            try { await server; } catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }

    [Fact]
    public async Task PublicStartup_RemoteInitializeFailureClosesWebSocketAndPreservesError()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var uri = new Uri($"ws://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        var server = ServeAsync(listener, async (socket, _, ct) =>
        {
            var request = await ReceiveJsonAsync(socket, ct);
            await SendJsonAsync(socket, new { id = request.GetProperty("id"), error = new { code = -32602, message = "unsupported initialize" } }, ct);
            await ObserveDisconnectAsync(socket, ct);
        }, deadline.Token);
        try
        {
            var start = () => CodexAppServerClient.ConnectWebSocketAsync(new() { Uri = uri }, deadline.Token);
            var error = (await start.Should().ThrowAsync<CodexAppServerInitializeException>()).Which;
            error.Message.Should().Contain("unsupported initialize");
            await server.WaitAsync(deadline.Token);
        }
        finally
        {
            await deadline.CancelAsync();
            try { await server; } catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }

    [Fact]
    public async Task Startup_RejectsMissingOptionsAndUnknownEndpointBeforeLaunching()
    {
        var start = () => CodexAppServerClient.StartAsync(null!);
        await start.Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
        var connect = () => CodexAppServerClient.ConnectWebSocketAsync(null!);
        await connect.Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
        connect = () => CodexAppServerClient.ConnectWebSocketAsync(new());
        await connect.Should().ThrowAsync<ArgumentException>().WithParameterName("options");
        var loggerFactory = NullLoggerFactory.Instance;
        var stdioFactory = CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory);
        var unknown = () => CodexAppServerClient.StartAsync(new() { Endpoint = new UnknownEndpoint() }, loggerFactory, stdioFactory, CancellationToken.None);
        await unknown.Should().ThrowAsync<InvalidOperationException>().WithMessage("*UnknownEndpoint*");
        var missingLogger = () => CodexAppServerClient.StartAsync(new(), null!, stdioFactory, CancellationToken.None);
        await missingLogger.Should().ThrowAsync<ArgumentNullException>().WithParameterName("loggerFactory");
        var missingFactory = () => CodexAppServerClient.StartAsync(new(), loggerFactory, null!, CancellationToken.None);
        await missingFactory.Should().ThrowAsync<ArgumentNullException>().WithParameterName("stdioFactory");
    }

    private sealed record UnknownEndpoint : CodexAppServerEndpoint;

    private static async Task ServeAsync(TcpListener listener, Func<WebSocket, string, CancellationToken, Task> conversation, CancellationToken ct)
    {
        using var tcp = await listener.AcceptTcpClientAsync(ct);
        await using var stream = tcp.GetStream();
        var header = new StringBuilder();
        var single = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            (await stream.ReadAsync(single, ct)).Should().Be(1);
            header.Append((char)single[0]);
            header.Length.Should().BeLessThan(16384);
        }
        var headers = header.ToString();
        var keyLine = headers.Split("\r\n").Single(line => line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase));
        var key = keyLine[(keyLine.IndexOf(':') + 1)..].Trim();
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"), ct);
        using var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
        await conversation(socket, headers, ct);
    }

    private static async Task ObserveDisconnectAsync(WebSocket socket, CancellationToken ct)
    {
        try
        {
            var close = await socket.ReceiveAsync(new ArraySegment<byte>(new byte[256]), ct);
            close.MessageType.Should().Be(WebSocketMessageType.Close);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "finished", ct);
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            // Canceling the client's pending receive may abort the socket during disposal.
            socket.State.Should().Be(WebSocketState.Aborted);
        }
    }

    private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket, CancellationToken ct)
    {
        using var message = new MemoryStream();
        var bytes = new byte[2048];
        WebSocketReceiveResult part;
        do
        {
            part = await socket.ReceiveAsync(new ArraySegment<byte>(bytes), ct);
            part.MessageType.Should().Be(WebSocketMessageType.Text);
            message.Write(bytes, 0, part.Count);
        } while (!part.EndOfMessage);
        using var document = JsonDocument.Parse(message.ToArray());
        return document.RootElement.Clone();
    }

    private static Task ReplyAsync(WebSocket socket, JsonElement request, object result, CancellationToken ct) =>
        SendJsonAsync(socket, new { id = request.GetProperty("id"), result }, ct);

    private static Task SendJsonAsync(WebSocket socket, object value, CancellationToken ct) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(value)), WebSocketMessageType.Text, true, ct);
}
