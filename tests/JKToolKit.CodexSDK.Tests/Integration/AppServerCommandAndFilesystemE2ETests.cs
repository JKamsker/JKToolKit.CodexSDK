using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Integration;

public sealed class AppServerCommandAndFilesystemE2ETests
{
    [CodexE2EFact]
    public async Task AppServer_CommandAndFilesystem_RoundTrips()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var tempRoot = Path.Combine(Path.GetTempPath(), $"jktoolkit-codexsdk-{Guid.NewGuid():N}");
        var directoryPath = Path.Combine(tempRoot, "fs");
        var filePath = Path.Combine(directoryPath, "hello.txt");
        var copyPath = Path.Combine(directoryPath, "copy.txt");

        Directory.CreateDirectory(tempRoot);

        try
        {
            await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
            {
                DefaultClientInfo = new("jktoolkit_codexsdk_tests", "JKToolKit.CodexSDK.Tests", "1.0.0")
            }, cts.Token);

            await client.FsCreateDirectoryAsync(new FsCreateDirectoryOptions
            {
                Path = directoryPath,
                Recursive = true
            }, cts.Token);

            var dataBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("hello from fs"));
            await client.FsWriteFileAsync(new FsWriteFileOptions
            {
                Path = filePath,
                DataBase64 = dataBase64
            }, cts.Token);

            var read = await client.FsReadFileAsync(new FsReadFileOptions { Path = filePath }, cts.Token);
            Encoding.UTF8.GetString(Convert.FromBase64String(read.DataBase64)).Should().Be("hello from fs");

            var metadata = await client.FsGetMetadataAsync(new FsGetMetadataOptions { Path = filePath }, cts.Token);
            metadata.IsFile.Should().BeTrue();

            var entries = await client.FsReadDirectoryAsync(new FsReadDirectoryOptions { Path = directoryPath }, cts.Token);
            entries.Entries.Should().Contain(x => x.FileName == "hello.txt");

            await client.FsCopyAsync(new FsCopyOptions
            {
                SourcePath = filePath,
                DestinationPath = copyPath
            }, cts.Token);

            var copied = await client.FsReadFileAsync(new FsReadFileOptions { Path = copyPath }, cts.Token);
            Encoding.UTF8.GetString(Convert.FromBase64String(copied.DataBase64)).Should().Be("hello from fs");

            var command = OperatingSystem.IsWindows()
                ? new[] { "cmd", "/c", "type", filePath }
                : new[] { "/bin/sh", "-lc", $"cat '{filePath.Replace("'", "'\\''")}'" };

            var exec = await client.CommandExecAsync(new CommandExecOptions
            {
                Command = command
            }, cts.Token);

            exec.ExitCode.Should().Be(0);
            exec.Stdout.Should().Contain("hello from fs");

            await client.FsRemoveAsync(new FsRemoveOptions
            {
                Path = copyPath,
                Force = true
            }, cts.Token);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
