using FluentAssertions;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class CodexSdkBuilderTests
{
    [Fact]
    public void CreateEffectiveOptionsSnapshot_GlobalCodexExecutablePath_FlowsToAllModes_WhenPerModePathsAreNull()
    {
        var builder = new CodexSdkBuilder
        {
            CodexExecutablePath = @"C:\bin\codex.exe"
        };

        var (exec, app, mcp) = builder.CreateEffectiveOptionsSnapshot();

        exec.CodexExecutablePath.Should().Be(builder.CodexExecutablePath);
        app.CodexExecutablePath.Should().Be(builder.CodexExecutablePath);
        mcp.CodexExecutablePath.Should().Be(builder.CodexExecutablePath);
    }

    [Fact]
    public void CreateEffectiveOptionsSnapshot_GlobalCodexHomeDirectory_FlowsToAllModes_WhenPerModeHomesAreNull()
    {
        var builder = new CodexSdkBuilder
        {
            CodexHomeDirectory = @"C:\codex\profiles\proaccount"
        };

        var (exec, app, mcp) = builder.CreateEffectiveOptionsSnapshot();

        exec.CodexHomeDirectory.Should().Be(builder.CodexHomeDirectory);
        app.CodexHomeDirectory.Should().Be(builder.CodexHomeDirectory);
        mcp.CodexHomeDirectory.Should().Be(builder.CodexHomeDirectory);
    }

    [Fact]
    public void CreateEffectiveOptionsSnapshot_ModeSpecificCodexExecutablePath_WinsOverGlobal()
    {
        var builder = new CodexSdkBuilder
        {
            CodexExecutablePath = @"C:\bin\global.exe"
        };

        builder.ConfigureExec(o => o.CodexExecutablePath = @"C:\bin\exec.exe");
        builder.ConfigureAppServer(o => o.CodexExecutablePath = @"C:\bin\app.exe");
        builder.ConfigureMcpServer(o => o.CodexExecutablePath = @"C:\bin\mcp.exe");

        var (exec, app, mcp) = builder.CreateEffectiveOptionsSnapshot();

        exec.CodexExecutablePath.Should().Be(@"C:\bin\exec.exe");
        app.CodexExecutablePath.Should().Be(@"C:\bin\app.exe");
        mcp.CodexExecutablePath.Should().Be(@"C:\bin\mcp.exe");
    }

    [Fact]
    public void CreateEffectiveOptionsSnapshot_ModeSpecificCodexHomeDirectory_WinsOverGlobal()
    {
        var builder = new CodexSdkBuilder
        {
            CodexHomeDirectory = @"C:\codex\global"
        };

        builder.ConfigureExec(o => o.CodexHomeDirectory = @"C:\codex\exec");
        builder.ConfigureAppServer(o => o.CodexHomeDirectory = @"C:\codex\app");
        builder.ConfigureMcpServer(o => o.CodexHomeDirectory = @"C:\codex\mcp");

        var (exec, app, mcp) = builder.CreateEffectiveOptionsSnapshot();

        exec.CodexHomeDirectory.Should().Be(@"C:\codex\exec");
        app.CodexHomeDirectory.Should().Be(@"C:\codex\app");
        mcp.CodexHomeDirectory.Should().Be(@"C:\codex\mcp");
    }

    [Fact]
    public void CreateEffectiveOptionsSnapshot_GlobalCodexExecutablePath_AppliesOnlyToNullPerModePaths()
    {
        var builder = new CodexSdkBuilder
        {
            CodexExecutablePath = @"C:\bin\global.exe"
        };

        builder.ConfigureExec(o => o.CodexExecutablePath = null);
        builder.ConfigureAppServer(o => o.CodexExecutablePath = @"C:\bin\app.exe");
        builder.ConfigureMcpServer(o => o.CodexExecutablePath = null);

        var (exec, app, mcp) = builder.CreateEffectiveOptionsSnapshot();

        exec.CodexExecutablePath.Should().Be(@"C:\bin\global.exe");
        app.CodexExecutablePath.Should().Be(@"C:\bin\app.exe");
        mcp.CodexExecutablePath.Should().Be(@"C:\bin\global.exe");
    }
}

