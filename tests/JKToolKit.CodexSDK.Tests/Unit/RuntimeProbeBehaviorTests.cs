using System.Diagnostics;
using JKToolKit.CodexSDK.Facade;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RuntimeProbeBehaviorTests
{
    [Theory]
    [InlineData("matching", 0, true)]
    [InlineData("old", 0, false)]
    [InlineData("malformed", 0, null)]
    [InlineData("matching", 7, null)]
    public async Task UnixProbe_ReportsVersionMismatchAndProcessErrors(string kind, int exitCode, bool? match)
    {
        if (OperatingSystem.IsWindows()) return;
        var path = Path.GetTempFileName();
        try
        {
            var version = kind == "matching" ? CodexRuntime.ExpectedVersion : "0.1.0";
            var output = kind == "malformed" ? "unrecognized" : "codex-cli " + version;
            await File.WriteAllTextAsync(path, $"#!/bin/sh\n[ \"$1\" = \"--version\" ] || exit 9\nprintf '%s\\n' '{output}'\nexit {exitCode}\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await using var sdk = CodexSdk.Create(b => b.CodexExecutablePath = path);
            var info = await sdk.Runtime.GetInfoAsync(includeServer: false);
            Assert.Equal(path, info.ExecutablePath);
            Assert.Equal(match, info.IsVersionMatch);
            if (match == true) Assert.Empty(info.Diagnostics);
            else if (match == false) Assert.Contains("found CLI 0.1.0", Assert.Single(info.Diagnostics));
            else Assert.Contains("preflight failed", Assert.Single(info.Diagnostics));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UnixProbe_CancellationTerminatesStartedProcess()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Directory.CreateTempSubdirectory();
        var path = Path.Combine(directory.FullName, "probe");
        var pidFile = Path.Combine(directory.FullName, "pid");
        try
        {
            await File.WriteAllTextAsync(path, $"#!/bin/sh\nprintf '%s' $$ > '{pidFile}'\nexec sleep 60\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await using var sdk = CodexSdk.Create(b => b.CodexExecutablePath = path);
            using var cancellation = new CancellationTokenSource();
            var probe = sdk.Runtime.GetInfoAsync(false, cancellation.Token);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            int pid;
            while (!File.Exists(pidFile) || !int.TryParse(await File.ReadAllTextAsync(pidFile, timeout.Token), out pid))
                await Task.Delay(10, timeout.Token);
            using var process = Process.GetProcessById(pid);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.WaitAsync(timeout.Token));
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.HasExited);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task ServerProbe_ReportsStartupFailureAndSdkStartPropagatesIt()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        await using var sdk = CodexSdk.Create(b => b.ConfigureAppServer(o => o.Launch = new() { FileName = missing }));
        var info = await sdk.Runtime.GetInfoAsync();
        Assert.Null(info.Initialize);
        Assert.Null(info.Account);
        Assert.Null(info.Capabilities);
        Assert.Contains(info.Diagnostics, d => d.Contains("Server preflight failed"));
        await Assert.ThrowsAnyAsync<Exception>(() => CodexSdk.StartAsync(b => b.ConfigureAppServer(o => o.Launch = new() { FileName = missing })));
    }

    [Theory]
    [InlineData("bad\".cmd")]
    [InlineData("bad\n.cmd")]
    [InlineData("bad\r.cmd")]
    [InlineData("bad\0.cmd")]
    public void WindowsBatchProbe_RejectsUnrepresentablePaths(string path) =>
        Assert.Throws<ArgumentException>(() => CodexRuntime.CreateVersionStartInfo(path, true));
}
