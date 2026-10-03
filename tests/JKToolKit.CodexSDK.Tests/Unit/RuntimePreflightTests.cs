using System.Runtime.InteropServices;
using FluentAssertions;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RuntimePreflightTests
{
    [Fact]
    public void BundledResolution_IsVersionAndArchitectureScoped()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var binary = Path.Combine(root, "codex-runtime", CodexRuntime.ExpectedVersion, $"{os}-{arch}", "bin", OperatingSystem.IsWindows() ? "codex.exe" : "codex");
        Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
        try
        {
            var provider = new DefaultCodexPathProvider(new RealFileSystem(), NullLogger<DefaultCodexPathProvider>.Instance);
            provider.GetBundledExecutablePath(root).Should().BeNull();
            File.WriteAllText(binary, "test");
            provider.GetBundledExecutablePath(root).Should().Be(binary);
            var explicitPath = Path.Combine(root, "explicit");
            File.WriteAllText(explicitPath, "test");
            provider.GetCodexExecutablePath(explicitPath).Should().Be(explicitPath);
            File.Delete(binary);
            provider.GetBundledExecutablePath(root).Should().BeNull();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MissingBinary_IsReportedWithoutStartingServer()
    {
        await using var sdk = CodexSdk.Create(b => b.CodexExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var info = await sdk.Runtime.GetInfoAsync(includeServer: false);
        info.ActualVersion.Should().BeNull();
        info.IsVersionMatch.Should().BeNull();
        info.Diagnostics.Should().ContainSingle().Which.Should().Contain("Executable preflight failed");
    }

    [Fact]
    public async Task CanceledPreflight_PropagatesCancellation()
    {
        await using var sdk = CodexSdk.Create();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sdk.Runtime.GetInfoAsync(ct: cts.Token));
    }

    [Theory]
    [InlineData("0.160.0", true)]
    [InlineData("0.153.2", false)]
    [InlineData("0.160.0-alpha.1", false)]
    public void VersionMatch_IsExact(string actual, bool expected) =>
        new CodexRuntimeInfo { ActualVersion = actual, ExpectedVersion = "0.160.0" }.IsVersionMatch.Should().Be(expected);
}
