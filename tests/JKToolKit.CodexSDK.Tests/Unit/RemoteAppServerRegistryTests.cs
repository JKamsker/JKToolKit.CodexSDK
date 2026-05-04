using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RemoteAppServerRegistryTests
{
    [Fact]
    public async Task InMemoryRegistry_SupportsAddListUpdateRemove()
    {
        var registry = new InMemoryCodexRemoteAppServerRegistry();
        var entry = CreateEntry("remote-1", CodexRemoteAppServerStatus.Running);

        await registry.UpsertAsync(entry);
        (await registry.ListAsync()).Should().ContainSingle().Which.Status.Should().Be(CodexRemoteAppServerStatus.Running);

        await registry.UpsertAsync(entry with { Status = CodexRemoteAppServerStatus.Stale });

        var updated = await registry.GetAsync("remote-1");
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(CodexRemoteAppServerStatus.Stale);
        (await registry.RemoveAsync("remote-1")).Should().BeTrue();
        (await registry.ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task JsonRegistry_RoundTripsSchemaVersion_AndOmitsSecretsByDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codexsdk-registry-{Guid.NewGuid():N}.json");
        try
        {
            var registry = new JsonFileCodexRemoteAppServerRegistry(path);
            await registry.UpsertAsync(CreateEntry("remote-1", CodexRemoteAppServerStatus.Running) with
            {
                BearerToken = "secret-token"
            });

            var raw = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(raw);
            doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
            raw.Should().NotContain("secret-token");

            var loaded = await new JsonFileCodexRemoteAppServerRegistry(path).GetAsync("remote-1");
            loaded.Should().NotBeNull();
            loaded!.BearerToken.Should().BeNull();
            loaded.WebSocketUri.Should().Be(new Uri("ws://127.0.0.1:4500"));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public async Task JsonRegistry_CanPersistSecrets_WhenExplicitlyEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"codexsdk-registry-{Guid.NewGuid():N}.json");
        try
        {
            var registry = new JsonFileCodexRemoteAppServerRegistry(
                path,
                new JsonFileCodexRemoteAppServerRegistryOptions { PersistSecrets = true });
            await registry.UpsertAsync(CreateEntry("remote-1", CodexRemoteAppServerStatus.Running) with
            {
                BearerToken = "secret-token"
            });

            var loaded = await new JsonFileCodexRemoteAppServerRegistry(path).GetAsync("remote-1");
            loaded.Should().NotBeNull();
            loaded!.BearerToken.Should().Be("secret-token");
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static CodexRemoteAppServerEntry CreateEntry(string id, CodexRemoteAppServerStatus status) => new()
    {
        Id = id,
        Kind = CodexRemoteAppServerKind.DockerContainerWebSocket,
        Status = status,
        WebSocketUri = new Uri("ws://127.0.0.1:4500"),
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        UpdatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        Docker = new CodexRemoteDockerAppServerInfo
        {
            ContainerName = "codexsdk-test",
            ContainerPort = 4500
        }
    };
}
