using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    public static IEnumerable<object[]> FilesystemInvalidPaths()
    {
        foreach (var operation in new[] { "write", "create", "metadata", "directory", "remove", "copy-source", "copy-destination", "watch" })
        foreach (var path in new[] { " ", "relative-file.txt" })
            yield return [operation, path];
    }

    [Theory]
    [MemberData(nameof(FilesystemInvalidPaths))]
    public async Task Filesystem_InvalidPathNeverReachesTransport(string operation, string path)
    {
        // A valid reply ensures that removing validation would complete the operation,
        // including destructive operations, instead of failing in unrelated response parsing.
        var rpc = new RecordingRpc("""{"isFile":true,"isDirectory":false,"createdAtMs":1,"modifiedAtMs":2,"entries":[],"path":"/workspace"}""");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var validPath = PathForPlatform("/workspace/file.txt");
        Func<Task> call = operation switch
        {
            "write" => () => client.FsWriteFileAsync(new() { Path = path, DataBase64 = "AA==" }),
            "create" => () => client.FsCreateDirectoryAsync(new() { Path = path, Recursive = true }),
            "metadata" => () => client.FsGetMetadataAsync(new() { Path = path }),
            "directory" => () => client.FsReadDirectoryAsync(new() { Path = path }),
            "remove" => () => client.FsRemoveAsync(new() { Path = path, Force = true, Recursive = true }),
            "copy-source" => () => client.FsCopyAsync(new() { SourcePath = path, DestinationPath = validPath }),
            "copy-destination" => () => client.FsCopyAsync(new() { SourcePath = validPath, DestinationPath = path }),
            _ => () => client.FsWatchAsync(new() { Path = path, WatchId = "watch-1" })
        };
        await call.Should().ThrowAsync<ArgumentException>();
        rpc.Requests.Should().BeEmpty("invalid paths must be rejected before sending filesystem requests");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Filesystem_MetadataRetainsKindFlagsAndSigned64BitTimestamps(bool isFile, bool isDirectory)
    {
        const long created = -2_208_988_800_123;
        const long modified = 4_102_444_800_987;
        var payload = JsonSerializer.SerializeToElement(new { isFile, isDirectory, createdAtMs = created, modifiedAtMs = modified, futureMetadata = "preserved" });
        var rpc = new RecordingRpc(payload.GetRawText());
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var path = PathForPlatform("/workspace/item");
        var result = await client.FsGetMetadataAsync(new() { Path = path });
        result.IsFile.Should().Be(isFile);
        result.IsDirectory.Should().Be(isDirectory);
        result.CreatedAtMs.Should().Be(created);
        result.ModifiedAtMs.Should().Be(modified);
        JsonElement.DeepEquals(result.Raw, payload).Should().BeTrue();
        var request = rpc.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be("fs/getMetadata");
        request.Parameters.GetProperty("path").GetString().Should().Be(path);
    }

    [Fact]
    public async Task Filesystem_BinaryFileContentAndRawResponseSurviveReadWriteRoundTrip()
    {
        byte[] bytes = [0, 255, 128, 13, 10, 195, 169];
        var encoded = Convert.ToBase64String(bytes);
        var rpc = new RecordingRpc("{}")
        {
            Respond = method => method == "fs/readFile"
                ? JsonSerializer.SerializeToElement(new { dataBase64 = encoded, etag = "revision-1" })
                : JsonSerializer.SerializeToElement(new { etag = "revision-1" })
        };
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var path = PathForPlatform("/workspace/binary.dat");
        var write = await client.FsWriteFileAsync(new() { Path = path, DataBase64 = encoded });
        var read = await client.FsReadFileAsync(new() { Path = path });
        read.DataBase64.Should().Be(encoded);
        Convert.FromBase64String(read.DataBase64).Should().Equal(bytes);
        write.Raw.GetProperty("etag").GetString().Should().Be("revision-1");
        read.Raw.GetProperty("etag").GetString().Should().Be("revision-1");
        rpc.Requests.Select(request => request.Method).Should().Equal("fs/writeFile", "fs/readFile");
        rpc.Requests.Should().OnlyContain(request => request.Parameters.GetProperty("path").GetString() == path);
        rpc.Requests[0].Parameters.GetProperty("dataBase64").GetString().Should().Be(encoded);
    }

    [Fact]
    public async Task Filesystem_DirectoryEntriesPreserveNamesKindsOrderAndEntryRawPayloads()
    {
        using var payload = JsonDocument.Parse("""{"entries":[{"fileName":"résumé.txt","isFile":true,"isDirectory":false,"entryId":1},{"fileName":"nested","isFile":false,"isDirectory":true,"entryId":2},{"fileName":"other","isFile":false,"isDirectory":false,"entryId":3}],"snapshot":"v1"}""");
        var rpc = new RecordingRpc(payload.RootElement.GetRawText());
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var path = PathForPlatform("/workspace");
        var result = await client.FsReadDirectoryAsync(new() { Path = path });
        result.Entries.Select(entry => (entry.FileName, entry.IsFile, entry.IsDirectory)).Should().Equal(
            ("résumé.txt", true, false), ("nested", false, true), ("other", false, false));
        for (var index = 0; index < result.Entries.Count; index++)
            JsonElement.DeepEquals(result.Entries[index].Raw, payload.RootElement.GetProperty("entries")[index]).Should().BeTrue();
        JsonElement.DeepEquals(result.Raw, payload.RootElement).Should().BeTrue();
        var request = rpc.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be("fs/readDirectory");
        request.Parameters.GetProperty("path").GetString().Should().Be(path);
    }
}
