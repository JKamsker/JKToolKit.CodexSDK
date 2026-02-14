using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadStartParamsSerializationTests
{
    [Fact]
    public void Serialize_OmitsExperimentalRawEvents_WhenFalse()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ExperimentalRawEvents = false },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().NotContain("experimentalRawEvents");
    }

    [Fact]
    public void Serialize_IncludesExperimentalRawEvents_WhenTrue()
    {
        var json = JsonSerializer.Serialize(
            new ThreadStartParams { ExperimentalRawEvents = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("\"experimentalRawEvents\":true");
    }
}

