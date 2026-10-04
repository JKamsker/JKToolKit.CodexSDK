using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class McpServerE2ETests
{
    [CodexE2EFact]
    public async Task CurrentCli_ReportsRemovedMcpServerCommandExplicitly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var startInfo = new ProcessStartInfo("codex")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("help");
        startInfo.ArgumentList.Add("mcp-server");
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = process.StandardError.ReadToEndAsync(cts.Token);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            process.ExitCode.Should().Be(2);
            (await stderr).Should().Contain("unrecognized subcommand 'mcp-server'");
            (await stdout).Should().BeEmpty();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    [CodexLegacyMcpE2EFact]
    public async Task LegacyMcpServer_Starts_AndListsCodexTools()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        // Tools/list needs no credentials. A current config can contain values a legacy CLI cannot parse.
        var codexHome = Path.Combine(Path.GetTempPath(), $"codex-sdk-legacy-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(codexHome);
        try
        {
            await using var sdk = CodexSdk.Create(builder => builder.ConfigureMcpServer(options =>
            {
                options.CodexExecutablePath = Environment.GetEnvironmentVariable("CODEX_E2E_MCP_EXECUTABLE");
                options.CodexHomeDirectory = codexHome;
            }));
            await using var client = await sdk.McpServer.StartAsync(cts.Token);

            var tools = await client.ListToolsAsync(cts.Token);
            tools.Should().Contain(tool => tool.Name == "codex");
            tools.Should().Contain(tool => tool.Name == "codex-reply");
        }
        finally
        {
            Directory.Delete(codexHome, recursive: true);
        }
    }
}
