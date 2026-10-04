using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SandboxWireAndRemoteLaunchEdgeCaseTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("relative/path")]
    public void SandboxRoots_RejectNonAbsoluteValues(string path)
    {
        Assert.Throws<ArgumentException>(() => new ReadOnlyAccess.Restricted { ReadableRoots = [path] }).ParamName.Should().Be("ReadableRoots");
        Assert.Throws<ArgumentException>(() => new SandboxPolicy.WorkspaceWrite { WritableRoots = [path] }).ParamName.Should().Be("WritableRoots");
    }

    [Fact]
    public void SandboxRoots_NullMeansNoExplicitRoots_AndValidRootsRoundTrip()
    {
        new ReadOnlyAccess.Restricted { ReadableRoots = null! }.ReadableRoots.Should().BeEmpty();
        new SandboxPolicy.WorkspaceWrite { WritableRoots = null! }.WritableRoots.Should().BeEmpty();
        var root = Path.GetFullPath(Path.GetTempPath());
        ReadOnlyAccess access = new ReadOnlyAccess.Restricted { IncludePlatformDefaults = false, ReadableRoots = [root] };
        var wire = JsonSerializer.SerializeToElement(access);
        wire.GetProperty("type").GetString().Should().Be("restricted");
        wire.GetProperty("includePlatformDefaults").GetBoolean().Should().BeFalse();
        wire.GetProperty("readableRoots")[0].GetString().Should().Be(root);
        JsonSerializer.Deserialize<ReadOnlyAccess>(wire).Should().BeOfType<ReadOnlyAccess.Restricted>()
            .Which.ReadableRoots.Should().Equal(root);
        JsonSerializer.SerializeToElement(new SandboxPolicy.DangerFullAccess()).GetProperty("type").GetString().Should().Be("dangerFullAccess");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("\"restricted\"")]
    [InlineData("{}")]
    [InlineData("{\"type\":42}")]
    [InlineData("{\"type\":\"future\"}")]
    public void ReadAccess_RejectsInvalidUnionDiscriminators(string wire) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReadOnlyAccess>(wire));

    [Theory]
    [InlineData("{\"type\":\"restricted\"}")]
    [InlineData("{\"type\":\"restricted\",\"includePlatformDefaults\":null,\"readableRoots\":42}")]
    public void RestrictedAccess_DefaultsAreStableWhenOptionalFieldsAreMissingOrUnknown(string wire)
    {
        var access = JsonSerializer.Deserialize<ReadOnlyAccess>(wire).Should().BeOfType<ReadOnlyAccess.Restricted>().Subject;
        access.IncludePlatformDefaults.Should().BeTrue();
        access.ReadableRoots.Should().BeEmpty();
    }

    [Fact]
    public void ReadAccessConverter_RejectsUnsupportedSubclassAndMissingWriterOrValue()
    {
        var converter = new ReadOnlyAccessJsonConverter();
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        Assert.Throws<ArgumentNullException>(() => converter.Write(null!, new ReadOnlyAccess.FullAccess(), new()));
        Assert.Throws<ArgumentNullException>(() => converter.Write(writer, null!, new()));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize<ReadOnlyAccess>(new UnsupportedReadAccess()))
            .Message.Should().Contain("UnsupportedReadAccess");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DockerLaunch_RejectsMissingContainer(string? container) =>
        Assert.Throws<ArgumentException>(() => CodexLaunchRemote.DockerAppServer(container!)).ParamName.Should().Be("container");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void SshLaunch_RejectsPortsOutsideTcpRange(int port) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CodexLaunchRemote.SshAppServer(new CodexSshAppServerOptions { Host = "devbox", Port = port })).ParamName.Should().Be("Port");

    [Fact]
    public void SshLaunch_ValidatesRequiredExecutableConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() => CodexLaunchRemote.SshAppServer((CodexSshAppServerOptions)null!));
        Assert.Throws<ArgumentException>(() => CodexLaunchRemote.SshAppServer(new CodexSshAppServerOptions { Host = " " })).ParamName.Should().Be("Host");
        Assert.Throws<ArgumentException>(() => CodexLaunchRemote.SshAppServer(new CodexSshAppServerOptions { Host = "devbox", SshExecutable = " " })).ParamName.Should().Be("SshExecutable");
        Assert.Throws<ArgumentException>(() => CodexLaunchRemote.SshAppServer(new CodexSshAppServerOptions { Host = "devbox", Password = "test-password", SshpassExecutable = " " })).ParamName.Should().Be("SshpassExecutable");
    }

    [Fact]
    public void SshLaunch_PreservesExtraArgumentsAndBoundaryPorts()
    {
        foreach (var port in new[] { 1, 65535 })
        {
            var launch = CodexLaunchRemote.SshAppServer(new CodexSshAppServerOptions { Host = "devbox", Port = port, AdditionalSshArguments = ["-o", "ConnectTimeout=7"] });
            launch.Arguments.Should().Equal("-T", "-p", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "-o", "ConnectTimeout=7", "devbox", "bash", "-lc", "exec codex app-server");
        }
    }

    private sealed record UnsupportedReadAccess : ReadOnlyAccess { public override string Type => "future"; }
}
