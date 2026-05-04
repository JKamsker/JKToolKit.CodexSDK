using System.Diagnostics;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class RemoteAppServerDockerE2ETests
{
    private const string ImageName = "jktoolkit-codexsdk-remote-e2e:latest";

    [CodexDockerE2EFact]
    public async Task DockerRemoteAppServer_Stdio_ManagedContainerWebSocket_AndExecWebSocket_RoundTrip()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        var repoRoot = GetRepoRoot();
        var codexHome = await CreateCodexHomeCopyAsync(cts.Token);
        var registryPath = Path.Combine(Path.GetTempPath(), $"codexsdk-registry-{Guid.NewGuid():N}.json");
        var baseContainer = $"codexsdk-base-{Guid.NewGuid():N}";
        var execContainer = $"codexsdk-exec-{Guid.NewGuid():N}";

        try
        {
            await EnsureImageAsync(cts.Token);
            await RunDockerAsync([
                "run", "-d", "--name", baseContainer,
                "-v", $"{repoRoot}:/workspace",
                "-v", $"{codexHome}:/home/codex/.codex",
                "-w", "/workspace",
                "-e", "CODEX_HOME=/home/codex/.codex",
                ImageName,
                "sleep", "infinity"
            ], cts.Token);
            await using (var stdio = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
            {
                Launch = CodexLaunchRemote.DockerAppServer(baseContainer, "/workspace", "/home/codex/.codex")
            }, cts.Token))
            {
                await AssertRemotePwdAsync(stdio, cts.Token);
            }

            var registry = new JsonFileCodexRemoteAppServerRegistry(registryPath);
            var manager = new CodexRemoteAppServerManager(registry);
            var managed = await manager.StartDockerContainerWebSocketAsync(new CodexDockerContainerWebSocketAppServerOptions
            {
                Image = ImageName,
                WorkingDirectory = "/workspace",
                CodexHome = "/home/codex/.codex",
                AdditionalDockerRunArguments =
                [
                    "-v", $"{repoRoot}:/workspace",
                    "-v", $"{codexHome}:/home/codex/.codex"
                ]
            }, cts.Token);
            await using (var attached = await manager.AttachAsync(managed.Id, ct: cts.Token))
            {
                await AssertRemotePwdAsync(attached.Client, cts.Token);
            }

            var manager2 = new CodexRemoteAppServerManager(new JsonFileCodexRemoteAppServerRegistry(registryPath));
            await using (var reattached = await manager2.AttachAsync(managed.Id, ct: cts.Token))
            {
                await AssertRemotePwdAsync(reattached.Client, cts.Token);
            }
            await manager.StopAsync(managed.Id, new CodexRemoteStopOptions { RemoveFromRegistry = true }, cts.Token);

            await RunDockerAsync([
                "run", "-d", "--name", execContainer,
                "-p", "127.0.0.1::4500",
                "-v", $"{repoRoot}:/workspace",
                "-v", $"{codexHome}:/home/codex/.codex",
                "-w", "/workspace",
                "-e", "CODEX_HOME=/home/codex/.codex",
                ImageName,
                "sleep", "infinity"
            ], cts.Token);
            var publicUri = new Uri($"ws://127.0.0.1:{await GetPublishedPortAsync(execContainer, cts.Token)}");
            var execEntry = await manager.StartDockerExecWebSocketAsync(new CodexDockerExecWebSocketAppServerOptions
            {
                Container = execContainer,
                PublicUri = publicUri,
                WorkingDirectory = "/workspace",
                CodexHome = "/home/codex/.codex"
            }, cts.Token);
            await using (var execAttached = await manager.AttachAsync(execEntry.Id, ct: cts.Token))
            {
                await AssertRemotePwdAsync(execAttached.Client, cts.Token);
            }
            await manager.StopAsync(execEntry.Id, new CodexRemoteStopOptions { RemoveFromRegistry = true }, cts.Token);
        }
        finally
        {
            await TryDockerAsync(["rm", "-f", baseContainer], CancellationToken.None);
            await TryDockerAsync(["rm", "-f", execContainer], CancellationToken.None);
            try { Directory.Delete(codexHome, recursive: true); } catch { }
            try { File.Delete(registryPath); } catch { }
        }
    }

    private static async Task AssertRemotePwdAsync(CodexAppServerClient client, CancellationToken ct)
    {
        var result = await client.CommandExecAsync(new CommandExecOptions
        {
            Command = ["/bin/sh", "-lc", "pwd"],
            Cwd = "/workspace"
        }, ct);

        result.ExitCode.Should().Be(0);
        result.Stdout.Trim().Should().Be("/workspace");
    }

    private static async Task EnsureImageAsync(CancellationToken ct)
    {
        var dockerfile = Path.Combine(Path.GetTempPath(), $"codexsdk-dockerfile-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(
            dockerfile,
            """
            FROM node:22-bookworm-slim
            RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates ripgrep bash && rm -rf /var/lib/apt/lists/*
            RUN npm install -g @openai/codex@0.128.0
            WORKDIR /workspace
            """,
            ct);
        try
        {
            await RunDockerAsync(["build", "-t", ImageName, "-f", dockerfile, Path.GetDirectoryName(dockerfile)!], ct);
        }
        finally
        {
            try { File.Delete(dockerfile); } catch { }
        }
    }

    private static async Task<string> CreateCodexHomeCopyAsync(CancellationToken ct)
    {
        var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        var target = Path.Combine(Path.GetTempPath(), $"codexsdk-home-{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(source, "auth.json"), Path.Combine(target, "auth.json"));
        File.Copy(Path.Combine(source, "config.toml"), Path.Combine(target, "config.toml"));
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return target;
    }

    private static async Task<int> GetPublishedPortAsync(string container, CancellationToken ct)
    {
        var output = await RunDockerAsync(["port", container, "4500/tcp"], ct);
        var token = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).First();
        return int.Parse(token[(token.LastIndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JKToolKit.CodexSDK.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static Task<string> TryDockerAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        RunProcessAsync("docker", args, throwOnError: false, ct);

    private static Task<string> RunDockerAsync(IReadOnlyList<string> args, CancellationToken ct) =>
        RunProcessAsync("docker", args, throwOnError: true, ct);

    private static async Task<string> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> args,
        bool throwOnError,
        CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (throwOnError && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"docker {string.Join(" ", args)} failed with {process.ExitCode}: {stderr}");
        }

        return stdout;
    }
}
