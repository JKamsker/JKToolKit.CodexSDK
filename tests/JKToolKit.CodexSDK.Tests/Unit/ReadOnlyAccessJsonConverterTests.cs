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

        access.Should().NotBeNull();
        access.Should().BeOfType<ReadOnlyAccess.Restricted>()
            .Which.ReadableRoots.Should().Equal("C:\\repo");
    }

    [Fact]
    public void ReadOnlyAccessJsonConverter_ParsesFullAccess()
    {
        var json = """{"type":"fullAccess"}""";

        var access = System.Text.Json.JsonSerializer.Deserialize<ReadOnlyAccess>(json);

        access.Should().BeOfType<ReadOnlyAccess.FullAccess>();
    }

    [Fact]
    public void ReadOnlyAccessJsonConverter_ThrowsOnUnknownType()
    {
        var json = """{"type":"unknown"}""";

        var act = () => System.Text.Json.JsonSerializer.Deserialize<ReadOnlyAccess>(json);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*Unknown ReadOnlyAccess discriminator*");
    }

    [Fact]
    public void ReadOnlyAccessJsonConverter_ThrowsWhenTypeMissing()
    {
        var json = """{"includePlatformDefaults":true}""";

        var act = () => System.Text.Json.JsonSerializer.Deserialize<ReadOnlyAccess>(json);

        act.Should().Throw<System.Text.Json.JsonException>()
            .WithMessage("*must include a string 'type' discriminator*");
    }
}
