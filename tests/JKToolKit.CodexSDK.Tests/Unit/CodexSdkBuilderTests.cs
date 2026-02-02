using FluentAssertions;

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

