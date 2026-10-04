using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    public static IEnumerable<object[]> SandboxPolicyPayloads()
    {
        foreach (var command in new[] { false, true })
        {
            yield return [command, new SandboxPolicy.DangerFullAccess(), """{"type":"dangerFullAccess"}"""];
            yield return [command, new SandboxPolicy.ExternalSandbox { NetworkAccess = SandboxNetworkAccess.Enabled }, """{"type":"externalSandbox","networkAccess":"enabled"}"""];
            yield return [command, new SandboxPolicy.ReadOnly
            {
                NetworkAccess = true,
                Access = new ReadOnlyAccess.Restricted { IncludePlatformDefaults = false, ReadableRoots = [PathForPlatform("/readable")] }
            }, """{"type":"readOnly","networkAccess":true,"access":{"type":"restricted","includePlatformDefaults":false,"readableRoots":["/readable"]}}"""];
            yield return [command, new SandboxPolicy.WorkspaceWrite
            {
                WritableRoots = [PathForPlatform("/writable")], NetworkAccess = true,
                ExcludeTmpdirEnvVar = true, ExcludeSlashTmp = true,
                ReadOnlyAccess = new ReadOnlyAccess.FullAccess()
            }, """{"type":"workspaceWrite","writableRoots":["/writable"],"networkAccess":true,"excludeTmpdirEnvVar":true,"excludeSlashTmp":true,"readOnlyAccess":{"type":"fullAccess"}}"""];
        }
    }

    [Theory]
    [MemberData(nameof(SandboxPolicyPayloads))]
    public async Task SandboxPolicy_VariantFieldsReachRealTransport(bool command, SandboxPolicy policy, string expected)
    {
        var rpc = new RecordingRpc(command
            ? """{"exitCode":0,"stdout":"","stderr":""}"""
            : """{"turn":{"id":"turn-1"}}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        if (command)
        {
            var result = await client.CommandExecAsync(new() { Command = ["echo", "hello"], SandboxPolicy = policy });
            result.ExitCode.Should().Be(0);
        }
        else
        {
            await using var turn = await client.StartTurnAsync("thread-1", new() { SandboxPolicy = policy });
            turn.TurnId.Should().Be("turn-1");
        }
        var request = rpc.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be(command ? "command/exec" : "turn/start");
        var actual = request.Parameters.GetProperty("sandboxPolicy");
        using var expectedDocument = JsonDocument.Parse(WireJsonForPlatform(expected));
        JsonElement.DeepEquals(actual, expectedDocument.RootElement).Should().BeTrue(
            "the configured {0} policy must arrive intact; actual payload: {1}", policy.Type, actual.GetRawText());
    }
}
