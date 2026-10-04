using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Exec.Notifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class CollabStatusConverterCoverageTests
{
    [Theory]
    [InlineData(null, "Unknown", "unknown")]
    [InlineData("", "Unknown", "unknown")]
    [InlineData(" \t", "Unknown", "unknown")]
    [InlineData("future", "Unknown", "unknown")]
    [InlineData("pendingInit", "PendingInit", "pending_init")]
    [InlineData("pending_init", "PendingInit", "pending_init")]
    [InlineData("pending-init", "PendingInit", "pending_init")]
    [InlineData("running", "Running", "running")]
    [InlineData("interrupted", "Interrupted", "interrupted")]
    [InlineData("completed", "Completed", "completed")]
    [InlineData("errored", "Errored", "errored")]
    [InlineData("error", "Errored", "errored")]
    [InlineData("shutdown", "Shutdown", "shutdown")]
    [InlineData("notFound", "NotFound", "not_found")]
    [InlineData("not_found", "NotFound", "not_found")]
    [InlineData("not-found", "NotFound", "not_found")]
    public void StatusAliases_NormalizeCaseWhitespaceAndWireValue(string? alias, string enumName, string wire)
    {
        foreach (var value in new[] { alias, alias is null ? null : " " + alias.ToUpperInvariant() + "\t" })
        {
            var agent = CollabAgentStatusJsonConverter.ParseOrUnknown(value);
            agent.Should().Be(Enum.Parse<CollabAgentStatus>(enumName));
            CollabAgentStatusJsonConverter.ToWireValue(agent).Should().Be(wire);
            var receiver = CollabReceiverStatusJsonConverter.ParseOrUnknown(value);
            receiver.Should().Be(Enum.Parse<CollabReceiverStatus>(enumName));
            CollabReceiverStatusJsonConverter.ToWireValue(receiver).Should().Be(wire);
            JsonSerializer.Deserialize<CollabReceiverStatus>(JsonSerializer.Serialize(value)).Should().Be(receiver);
            JsonSerializer.Serialize(receiver).Should().Be(JsonSerializer.Serialize(wire));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[1,{\"nested\":[2]}]")]
    public void NonStringStatus_IsUnknownAndConsumesTheEntireValue(string json)
    {
        var values = JsonSerializer.Deserialize<CollabReceiverStatus[]>("[" + json + ",\"running\"]");
        values.Should().Equal(CollabReceiverStatus.Unknown, CollabReceiverStatus.Running);
    }

    [Fact]
    public void OutOfRangeEnum_WritesUnknown()
    {
        CollabAgentStatusJsonConverter.ToWireValue((CollabAgentStatus)999).Should().Be("unknown");
        JsonSerializer.Serialize((CollabReceiverStatus)999).Should().Be("\"unknown\"");
        var act = () => new CollabReceiverStatusJsonConverter().Write(null!, CollabReceiverStatus.Running, new());
        act.Should().Throw<ArgumentNullException>().WithParameterName("writer");
    }
}
