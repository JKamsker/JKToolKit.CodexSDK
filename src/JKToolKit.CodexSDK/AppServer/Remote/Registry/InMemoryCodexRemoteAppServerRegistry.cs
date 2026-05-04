using System.Collections.Concurrent;
using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AppServer.Remote.Registry;

/// <summary>
/// In-memory registry for SDK-managed remote Codex app-server processes.
/// </summary>
public sealed class InMemoryCodexRemoteAppServerRegistry : ICodexRemoteAppServerRegistry
{
    private readonly ConcurrentDictionary<string, CodexRemoteAppServerEntry> _entries =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<IReadOnlyList<CodexRemoteAppServerEntry>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var entries = _entries.Values
            .OrderBy(entry => entry.CreatedAt)
            .ToArray();
        return Task.FromResult<IReadOnlyList<CodexRemoteAppServerEntry>>(entries);
    }

    /// <inheritdoc />
    public Task<CodexRemoteAppServerEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ct.ThrowIfCancellationRequested();

        _entries.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task UpsertAsync(CodexRemoteAppServerEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);
        ct.ThrowIfCancellationRequested();

        _entries[entry.Id] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RemoveAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_entries.TryRemove(id, out _));
    }
}
