using FluentAssertions;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RuntimeBatchPreflightTests
{
    [Theory]
    [InlineData(".cmd")]
    [InlineData(".CMD")]
    [InlineData(".bat")]
    public void WindowsShim_UsesLiteralEnvironmentPathAndFixedCommand(string extension)
    {
        var path = @"C:\Codex SDK & (tools)\%PATH% !name!\codex" + extension;
        var info = CodexRuntime.CreateVersionStartInfo(path, isWindows: true);
        Path.GetFileName(info.FileName).Should().Be("cmd.exe");
        info.Environment["CODEX_SDK_PREFLIGHT_BINARY"].Should().Be(path);
        info.Arguments.Should().Be("/d /v:off /s /c \"\"%CODEX_SDK_PREFLIGHT_BINARY%\" --version\"");
        info.ArgumentList.Should().BeEmpty();
        info.UseShellExecute.Should().BeFalse();
    }

    [Theory]
    [InlineData("codex.exe", true)]
    [InlineData("codex", false)]
    [InlineData("codex.cmd", false)]
    public void NativeProbe_ExecutesSelectedBinaryDirectly(string path, bool isWindows)
    {
        var info = CodexRuntime.CreateVersionStartInfo(path, isWindows);
        info.FileName.Should().Be(path);
        info.Arguments.Should().BeEmpty();
        info.ArgumentList.Should().Equal("--version");
        info.RedirectStandardOutput.Should().BeTrue();
        info.RedirectStandardError.Should().BeTrue();
    }

    [Theory]
    [InlineData(".cmd", "0.160.0")]
    [InlineData(".CMD", "0.153.2")]
    [InlineData(".bat", "0.160.0")]
    public async Task WindowsShim_PreflightReadsVersionFromPathContainingShellCharacters(string extension, string version)
    {
        if (!OperatingSystem.IsWindows()) return; // Executed by the Windows CI matrix job.
        var root = Path.Combine(Path.GetTempPath(), $"codex preflight & (tools) %PATH% !literal! {Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "codex" + extension);
            await File.WriteAllTextAsync(path, $"@echo off\r\nif not \"%~1\"==\"--version\" exit /b 7\r\nif not \"%~2\"==\"\" exit /b 8\r\necho codex-cli {version}\r\n");
            await using var sdk = CodexSdk.Create(builder => builder.CodexExecutablePath = path);
            var info = await sdk.Runtime.GetInfoAsync(includeServer: false);
            info.ExecutablePath.Should().Be(path);
            info.ActualVersion.Should().Be(version);
            info.IsVersionMatch.Should().Be(version == CodexRuntime.ExpectedVersion);
            info.Diagnostics.Should().NotContain(message => message.Contains("preflight failed"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
