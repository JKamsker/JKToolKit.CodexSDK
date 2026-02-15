using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ReadOnlyAccessJsonConverterTests
{
    [Fact]
    public void ReadOnlyAccessJsonConverter_IgnoresNonStringReadableRoots_ForForwardCompatibility()
    {
        var json = """{"type":"restricted","includePlatformDefaults":true,"readableRoots":["C:\\repo",123,{"x":1},null]}""";

        var access = System.Text.Json.JsonSerializer.Deserialize<ReadOnlyAccess>(json);

        access.Should().BeOfType<ReadOnlyAccess.Restricted>();
        ((ReadOnlyAccess.Restricted)access!).ReadableRoots.Should().Equal("C:\\repo");
    }
}

