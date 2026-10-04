using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteAppServerRegistryEdgeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registry_ValidatesArgumentsAndHonorsCancellation(bool fileBacked)
    {
        using var directory = new TemporaryDirectory();
        ICodexRemoteAppServerRegistry registry = fileBacked ? new JsonFileCodexRemoteAppServerRegistry(directory.File) : new InMemoryCodexRemoteAppServerRegistry();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => registry.GetAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => registry.RemoveAsync(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => registry.UpsertAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => registry.UpsertAsync(Entry(" ")));
        var ct = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.ListAsync(ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.GetAsync("id", ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.UpsertAsync(Entry("id"), ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registry.RemoveAsync("id", ct));
        (await registry.ListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registry_UsesOrdinalIdsAndUpdatesWithoutDuplicating(bool fileBacked)
    {
        using var directory = new TemporaryDirectory();
        ICodexRemoteAppServerRegistry registry = fileBacked ? new JsonFileCodexRemoteAppServerRegistry(directory.File) : new InMemoryCodexRemoteAppServerRegistry();
        (await registry.GetAsync("id")).Should().BeNull();
        (await registry.RemoveAsync("id")).Should().BeFalse();
        await registry.UpsertAsync(Entry("ID"));
        await registry.UpsertAsync(Entry("id"));
        await registry.UpsertAsync(Entry("id") with { Status = CodexRemoteAppServerStatus.Stopped });
        (await registry.ListAsync()).Should().HaveCount(2);
        (await registry.GetAsync("ID"))!.Status.Should().Be(CodexRemoteAppServerStatus.Running);
        (await registry.GetAsync("id"))!.Status.Should().Be(CodexRemoteAppServerStatus.Stopped);
        (await registry.RemoveAsync("id")).Should().BeTrue();
        (await registry.RemoveAsync("id")).Should().BeFalse();
        (await registry.ListAsync()).Single().Id.Should().Be("ID");
    }

    [Fact]
    public async Task MemoryRegistry_ListsOldestEntriesFirst()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        var now = DateTimeOffset.UtcNow;
        await registry.UpsertAsync(Entry("new") with { CreatedAt = now });
        await registry.UpsertAsync(Entry("old") with { CreatedAt = now.AddDays(-1) });
        (await registry.ListAsync()).Select(x => x.Id).Should().Equal("old", "new");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"entries\":[]}")]
    public async Task FileRegistry_UnknownOrNullDocumentReadsEmpty(string contents)
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.File, contents);
        var registry = new JsonFileCodexRemoteAppServerRegistry(directory.File);
        (await registry.ListAsync()).Should().BeEmpty();
        await registry.UpsertAsync(Entry("id"));
        (await registry.ListAsync()).Single().Id.Should().Be("id");
    }

    [Fact]
    public async Task FileRegistry_MalformedDocumentReleasesGateForRecovery()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.File, "malformed");
        var registry = new JsonFileCodexRemoteAppServerRegistry(directory.File);
        await Assert.ThrowsAnyAsync<JsonException>(() => registry.ListAsync());
        await Assert.ThrowsAnyAsync<JsonException>(() => registry.UpsertAsync(Entry("id")));
        await Assert.ThrowsAnyAsync<JsonException>(() => registry.RemoveAsync("id"));
        await File.WriteAllTextAsync(directory.File, "{\"schemaVersion\":1,\"entries\":[]}");
        await registry.UpsertAsync(Entry("id")).WaitAsync(TimeSpan.FromSeconds(5));
        (await registry.GetAsync("id")).Should().NotBeNull();
    }

    [Fact]
    public async Task FileRegistry_CreatesParentDirectoryAndUsesCustomSerializer()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "registry.json");
        var registry = new JsonFileCodexRemoteAppServerRegistry(path, new() { SerializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower } });
        await registry.UpsertAsync(Entry("id"));
        var contents = await File.ReadAllTextAsync(path);
        contents.Should().Contain("schema_version");
        (await registry.GetAsync("id")).Should().NotBeNull();
        File.Exists(path + ".tmp").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FileRegistry_RequiresPath(string? path) => Assert.ThrowsAny<ArgumentException>(() => new JsonFileCodexRemoteAppServerRegistry(path!));

    private static CodexRemoteAppServerEntry Entry(string id) => new() { Id = id, Kind = CodexRemoteAppServerKind.DockerContainerWebSocket, Status = CodexRemoteAppServerStatus.Running };

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "registry-test-" + Guid.NewGuid().ToString("N"));
        public string File => System.IO.Path.Combine(Path, "registry.json");
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
