using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadSetNameParamsSerializationTests
{
    [Fact]
    public void Serialize_UsesNameFieldName()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(new ThreadSetNameParams { ThreadId = "thr_123", Name = "Hello" }, options);

        json.Should().Contain("\"name\":\"Hello\"");
        json.Should().NotContain("threadName");
    }
}
