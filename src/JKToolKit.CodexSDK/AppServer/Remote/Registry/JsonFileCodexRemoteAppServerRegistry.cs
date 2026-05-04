using System.Text.Json;
using System.Text.Json.Serialization;
using JKToolKit.CodexSDK.AppServer.Remote;

namespace JKToolKit.CodexSDK.AppServer.Remote.Registry;

/// <summary>
/// Options for <see cref="JsonFileCodexRemoteAppServerRegistry"/>.
/// </summary>
public sealed class JsonFileCodexRemoteAppServerRegistryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether bearer tokens should be written to disk.
    /// </summary>
    public bool PersistSecrets { get; set; }

    /// <summary>
    /// Gets or sets optional serializer options.
    /// </summary>
    public JsonSerializerOptions? SerializerOptions { get; set; }
}

/// <summary>
/// JSON file registry for SDK-managed remote Codex app-server processes.
/// </summary>
public sealed class JsonFileCodexRemoteAppServerRegistry : ICodexRemoteAppServerRegistry
{
    private const int SchemaVersion = 1;
    private readonly string _path;
    private readonly JsonFileCodexRemoteAppServerRegistryOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _sync = new(1, 1);

    /// <summary>
    /// Initializes a new JSON file registry.
    /// </summary>
    /// <param name="path">The JSON registry path.</param>
    /// <param name="options">Optional registry options.</param>
    public JsonFileCodexRemoteAppServerRegistry(
        string path,
        JsonFileCodexRemoteAppServerRegistryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path = path;
        _options = options ?? new JsonFileCodexRemoteAppServerRegistryOptions();
        _jsonOptions = _options.SerializerOptions ?? CreateDefaultJsonOptions();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CodexRemoteAppServerEntry>> ListAsync(CancellationToken ct = default)
    {
        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return (await ReadDocumentAsync(ct).ConfigureAwait(false)).Entries;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CodexRemoteAppServerEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var entries = await ListAsync(ct).ConfigureAwait(false);
        return entries.FirstOrDefault(entry => string.Equals(entry.Id, id, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async Task UpsertAsync(CodexRemoteAppServerEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);

        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(ct).ConfigureAwait(false);
            var stored = SanitizeForPersistence(entry);
            var index = document.Entries.FindIndex(existing => existing.Id == stored.Id);
            if (index >= 0)
            {
                document.Entries[index] = stored;
            }
            else
            {
                document.Entries.Add(stored);
            }

            await WriteDocumentAsync(document, ct).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await _sync.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(ct).ConfigureAwait(false);
            var removed = document.Entries.RemoveAll(entry => entry.Id == id) > 0;
            if (removed)
            {
                await WriteDocumentAsync(document, ct).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _sync.Release();
        }
    }

    private async Task<RegistryDocument> ReadDocumentAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return new RegistryDocument();
        }

        await using var stream = File.OpenRead(_path);
        var document = await JsonSerializer.DeserializeAsync<RegistryDocument>(stream, _jsonOptions, ct)
            .ConfigureAwait(false);
        return document?.SchemaVersion == SchemaVersion
            ? document
            : new RegistryDocument();
    }

    private async Task WriteDocumentAsync(RegistryDocument document, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document.SchemaVersion = SchemaVersion;
        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, _jsonOptions, ct).ConfigureAwait(false);
        }

        if (File.Exists(_path))
        {
            File.Replace(tempPath, _path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _path);
        }
    }

    private CodexRemoteAppServerEntry SanitizeForPersistence(CodexRemoteAppServerEntry entry) =>
        _options.PersistSecrets ? entry : entry with { BearerToken = null };

    private static JsonSerializerOptions CreateDefaultJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class RegistryDocument
    {
        public int SchemaVersion { get; set; } = JsonFileCodexRemoteAppServerRegistry.SchemaVersion;

        public List<CodexRemoteAppServerEntry> Entries { get; set; } = [];
    }
}
