using FluentAssertions;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class IndeterminateTurnStartupTests
{
    [Theory]
    [InlineData(-32600)]
    [InlineData(-32602)]
    public async Task WrappedCapabilityRejection_PreservesTypedFailureAndAllowsRetry(int errorCode)
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartFailure = new JsonRpcRemoteException(new JsonRpcError(errorCode,
            "turn/start.someField requires experimentalApi capability"));
        var thread = await fixture.Threads.StartAsync();
        await Assert.ThrowsAsync<CodexExperimentalApiRequiredException>(() => thread.RunStreamedAsync("start"));
        fixture.Rpc.StartFailure = null;
        await using var handle = await thread.RunStreamedAsync("corrected request");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SerializationFailureBeforeDispatch_AllowsCorrectedInput(bool disposedSchema)
    {
        var transport = new RespondingTransport();
        await using var rpc = new JsonRpcConnection(transport, true, 10, null, NullLogger.Instance);
        await using var fixture = new Fixture(connection: rpc);
        var thread = await fixture.Threads.StartAsync();
        JsonElement schema = default;
        if (disposedSchema)
        {
            using var document = JsonDocument.Parse("{}");
            schema = document.RootElement;
        }
        var invalid = new TurnStartOptions { Input = [TurnInputItem.Text("user")], OutputSchema = schema };
        var failure = await Record.ExceptionAsync(() => thread.RunStreamedAsync(invalid));
        failure.Should().NotBeNull();
        failure!.Message.Should().NotContain("indeterminate");
        transport.TurnStarts.Should().Be(0);
        await using var handle = await thread.RunStreamedAsync("valid request");
        transport.TurnStarts.Should().Be(1);
    }

    [Theory]
    [InlineData("missing-id")]
    [InlineData("transport")]
    [InlineData("server-error")]
    public async Task UnknownAcceptance_RetainsReservationAcrossResumedSessions(string failure)
    {
        await using var fixture = new Fixture();
        fixture.Rpc.OmitStartTurnId = failure == "missing-id";
        fixture.Rpc.StartFailure = failure switch
        {
            "transport" => new IOException("response lost after dispatch"),
            "server-error" => new JsonRpcRemoteException(new JsonRpcError(-32603, "server execution error")),
            _ => null
        };
        var thread = await fixture.Threads.StartAsync();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("start"));
        exception.Message.Should().Contain("indeterminate");
        exception.InnerException.Should().NotBeNull();

        fixture.Rpc.OmitStartTurnId = false;
        fixture.Rpc.StartFailure = null;
        var resumed = await fixture.Threads.ResumeAsync(thread.Id);
        var retry = await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.RunAsync("retry"));
        retry.Message.Should().Contain("indeterminate");
        fixture.Rpc.TurnStartCalls.Should().Be(1, "an uncertain accepted turn must not be retried automatically");
    }

    [Theory]
    [InlineData(-32700)]
    [InlineData(-32600)]
    [InlineData(-32601)]
    [InlineData(-32602)]
    public async Task DefiniteProtocolRejection_ReleasesReservation(int errorCode)
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartFailure = new JsonRpcRemoteException(new JsonRpcError(errorCode, "request rejected"));
        var thread = await fixture.Threads.StartAsync();
        await Assert.ThrowsAsync<JsonRpcRemoteException>(() => thread.RunStreamedAsync("start"));
        fixture.Rpc.StartFailure = null;
        await using var handle = await thread.RunStreamedAsync("corrected request");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task LocalValidationFailure_ReleasesReservationWithoutDispatch()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        var invalid = new TurnStartOptions
        {
            Input = [TurnInputItem.Text("user")], ToolOutput = TurnToolOutput.Text("tool", "external")
        };
        await Assert.ThrowsAnyAsync<ArgumentException>(() => thread.RunStreamedAsync(invalid));
        fixture.Rpc.TurnStartCalls.Should().Be(0);
        await using var handle = await thread.RunStreamedAsync("valid request");
        fixture.Rpc.TurnStartCalls.Should().Be(1);
    }

    private sealed class RespondingTransport : IJsonRpcMessageTransport
    {
        private readonly Channel<string> _responses = Channel.CreateUnbounded<string>();
        public Task Completion => _responses.Reader.Completion;
        public int TurnStarts { get; private set; }
        public Task SendAsync(string message, CancellationToken ct)
        {
            using var request = JsonDocument.Parse(message);
            var method = request.RootElement.GetProperty("method").GetString();
            var result = method == "turn/start" ? "{\"turn\":{\"id\":\"u\",\"status\":\"inProgress\"}}" : "{\"thread\":{\"id\":\"t\"}}";
            if (method == "turn/start") TurnStarts++;
            _responses.Writer.TryWrite($"{{\"id\":{request.RootElement.GetProperty("id").GetRawText()},\"result\":{result}}}");
            return Task.CompletedTask;
        }
        public IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct) => _responses.Reader.ReadAllAsync(ct);
        public ValueTask DisposeAsync() { _responses.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
