using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class InitializeUserAgentVersionParsingTests
{
    [Fact]
    public void AppServerInitializeResult_ParsesCodexBuildVersion_FromUserAgentPrefix()
    {
        var raw = JsonSerializer.SerializeToElement(new
        {
            userAgent = "codex_vscode/1.2.3 (Windows 11; x86_64) wezterm/20240203 (codex_vscode; 0.1.0)"
        });

        var result = new AppServerInitializeResult(raw);
        result.CodexBuildVersion.Should().Be(new Version(1, 2, 3));
    }

    [Fact]
    public void AppServerInitializeResult_ParsesCodexBuildVersion_WhenTokenHasSuffix()
    {
        var raw = JsonSerializer.SerializeToElement(new
        {
            userAgent = "codex_vscode/1.2.3-alpha.1 (Windows 11; x86_64) wezterm/20240203"
        });

        var result = new AppServerInitializeResult(raw);
        result.CodexBuildVersion.Should().Be(new Version(1, 2, 3));
    }
}

