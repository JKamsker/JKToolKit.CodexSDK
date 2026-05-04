namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Stores SDK-managed remote Codex app-server process entries.
/// </summary>
public interface ICodexRemoteAppServerRegistry
{
    /// <summary>
    /// Gets all registered app-server entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The registered entries.</returns>
    Task<IReadOnlyList<CodexRemoteAppServerEntry>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets one registered app-server entry by identifier.
    /// </summary>
    /// <param name="id">The registry identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The entry, or <see langword="null"/> when no entry exists.</returns>
    Task<CodexRemoteAppServerEntry?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Adds or replaces a registered app-server entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(CodexRemoteAppServerEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Removes a registered app-server entry.
    /// </summary>
    /// <param name="id">The registry identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    Task<bool> RemoveAsync(string id, CancellationToken ct = default);
}
