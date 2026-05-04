using System.IO.Pipes;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerRemoteEndpointTests
{
    [Fact]
    public void CodexLaunchRemote_DockerAppServer_UsesCleanStdioArgs()
    {
        var launch = CodexLaunchRemote.DockerAppServer(
            "codex-dev",
            workingDirectory: "/workspace",
            codexHome: "/home/codex/.codex");

        launch.FileName.Should().Be("docker");
        launch.Arguments.Should().Equal(
            "exec",
            "-i",
            "-w",
            "/workspace",
            "-e",
            "CODEX_HOME=/home/codex/.codex",
            "codex-dev",
            "codex",
            "app-server");
    }

    [Fact]
    public void CodexLaunchRemote_SshAppServer_QuotesRemoteWorkingDirectory()
    {
        var launch = CodexLaunchRemote.SshAppServer("devbox", "/home/me/project's repo");

        launch.FileName.Should().Be("ssh");
        launch.Arguments.Should().Equal(
            "-T",
            "devbox",
            "bash",
            "-lc",
            "cd '/home/me/project'\"'\"'s repo' && exec codex app-server");
    }

    [Fact]
    public void CodexAppServerClientOptions_Clone_CopiesEndpoint()
    {
        var endpoint = new CodexAppServerWebSocketEndpoint(new Uri("ws://127.0.0.1:4500"), "token");
        var options = new CodexAppServerClientOptions { Endpoint = endpoint };

        var clone = options.Clone();

        clone.Endpoint.Should().BeSameAs(endpoint);
    }

    [Fact]
    public void CodexAppServerWebSocketEndpoint_RejectsNonWebSocketUri()
    {
        var act = () => new CodexAppServerWebSocketEndpoint(new Uri("http://127.0.0.1:4500"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ws:// or wss://*");
    }

    [Fact]
    public async Task JsonRpcConnection_OverWebSocketTransport_CorrelatesResponse()
    {
        await using var harness = await PipeHarness.CreateAsync();
        using var clientSocket = WebSocket.CreateFromStream(
            harness.ClientStream,
            isServer: false,
            subProtocol: null,
            keepAliveInterval: TimeSpan.FromSeconds(30));
        using var serverSocket = WebSocket.CreateFromStream(
            harness.ServerStream,
            isServer: true,
            subProtocol: null,
            keepAliveInterval: TimeSpan.FromSeconds(30));

        var transport = new WebSocketJsonRpcMessageTransport(
            clientSocket,
            new Uri("ws://localhost/app-server"),
            NullLogger.Instance);

        await using var rpc = new JsonRpcConnection(
            transport,
            includeJsonRpcHeader: false,
            notificationBufferCapacity: 10,
            serializerOptions: null,
            logger: NullLogger.Instance);

        var serverTask = Task.Run(async () =>
        {
            var line = await ReceiveTextAsync(serverSocket);
            line.Should().NotBeNull();

            using var reqDoc = JsonDocument.Parse(line!);
            reqDoc.RootElement.GetProperty("method").GetString().Should().Be("initialize");
            var id = reqDoc.RootElement.GetProperty("id").GetInt64();

            await SendTextAsync(
                serverSocket,
                JsonSerializer.Serialize(new { id, result = new { userAgent = "codex-test" } }));
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await rpc.SendRequestAsync("initialize", @params: null, cts.Token);

        result.GetProperty("userAgent").GetString().Should().Be("codex-test");
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task SendTextAsync(WebSocket socket, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            result.MessageType.Should().Be(WebSocketMessageType.Text);
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(message.ToArray());
            }
        }
    }

    private sealed class PipeHarness : IAsyncDisposable
    {
        private readonly NamedPipeServerStream _server;
        private readonly NamedPipeClientStream _client;

        private PipeHarness(NamedPipeServerStream server, NamedPipeClientStream client)
        {
            _server = server;
            _client = client;
        }

        public Stream ServerStream => _server;

        public Stream ClientStream => _client;

        public static async Task<PipeHarness> CreateAsync()
        {
            var name = $"ncodexsdk-ws-{Guid.NewGuid():N}";
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
            try { _client.Dispose(); } catch { }
            try { _server.Dispose(); } catch { }
            return ValueTask.CompletedTask;
        }
    }
}
