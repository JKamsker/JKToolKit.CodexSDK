using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadSetNameParamsSerializationTests
{
    [Fact]
    public void Serialize_IncludesThreadNameNull_ToClear()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(new ThreadSetNameParams { ThreadId = "thr_123", ThreadName = null }, options);

        json.Should().Contain("\"threadName\":null");
    }
}

