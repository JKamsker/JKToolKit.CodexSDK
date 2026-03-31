using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexProcessLauncherIoTests
{
    [Fact]
    public async Task WritePromptAndCloseStdinAsync_PreservesPromptBytesWithoutTrailingNewline()
    {
        using var process = StartEchoStdinProcess();

        await CodexProcessLauncherIo.WritePromptAndCloseStdinAsync(
            process,
            "alpha\nbeta",
            NullLogger.Instance,
            CancellationToken.None);

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        output.Should().Be("alpha\nbeta");
    }

    [Fact]
    public async Task WriteOptionalStdinPayloadAndCloseStdinAsync_PreservesPayloadBytesWithoutTrailingNewline()
    {
        using var process = StartEchoStdinProcess();

        await CodexProcessLauncherIo.WriteOptionalStdinPayloadAndCloseStdinAsync(
            process,
            "payload-without-newline",
            NullLogger.Instance,
            CancellationToken.None);

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        output.Should().Be("payload-without-newline");
    }

    private static Process StartEchoStdinProcess()
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"$text = [Console]::In.ReadToEnd(); [Console]::Out.Write($text)\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
            : new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = "-lc \"text=$(cat); printf '%s' \\\"$text\\\"\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

        return Process.Start(startInfo)!;
    }
}
