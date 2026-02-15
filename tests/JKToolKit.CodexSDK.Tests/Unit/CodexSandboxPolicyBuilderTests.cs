using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSandboxPolicyBuilderTests
{
    [Fact]
    public void ReadOnlyRestricted_BuildsAccessOverrideShape()
    {
        var policy = CodexSandboxPolicyBuilder.ReadOnlyRestricted(
            readableRoots: [@"C:\repo"],
            includePlatformDefaults: false);

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("readOnly");
        json.GetProperty("access").GetProperty("type").GetString().Should().Be("restricted");
        json.GetProperty("access").GetProperty("includePlatformDefaults").GetBoolean().Should().BeFalse();
        json.GetProperty("access").GetProperty("readableRoots").EnumerateArray().Select(x => x.GetString()).Should().Equal(@"C:\repo");
    }

    [Fact]
    public void WorkspaceWrite_WithReadOnlyAccess_BuildsShape()
    {
        var policy = CodexSandboxPolicyBuilder.WorkspaceWrite(
            writableRoots: [@"C:\repo"],
            networkAccess: true,
            readOnlyAccess: new JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy.ReadOnlyAccess.FullAccess());

        var text = JsonSerializer.Serialize(policy, CodexAppServerClient.CreateDefaultSerializerOptions());
        using var doc = JsonDocument.Parse(text);
        var json = doc.RootElement;

        json.GetProperty("type").GetString().Should().Be("workspaceWrite");
        json.GetProperty("networkAccess").GetBoolean().Should().BeTrue();
        json.GetProperty("writableRoots").EnumerateArray().Select(x => x.GetString()).Should().Equal(@"C:\repo");
        json.GetProperty("readOnlyAccess").GetProperty("type").GetString().Should().Be("fullAccess");
    }
}
