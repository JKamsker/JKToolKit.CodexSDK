using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class UpstreamSchemaDiscovery
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetDefaultSchemaPath(string repoRoot)
    {
        return Path.Combine(
            repoRoot,
            "external",
            "codex",
            "codex-rs",
            "app-server-protocol",
            "schema",
            "json",
            "codex_app_server_protocol.schemas.json");
    }

    public static UpstreamSchemaMetadata GetMetadata(string repoRoot, string schemaPath)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            throw new ArgumentException("RepoRoot cannot be empty.", nameof(repoRoot));
        }

        if (string.IsNullOrWhiteSpace(schemaPath))
        {
            throw new ArgumentException("Schema path cannot be empty.", nameof(schemaPath));
        }

        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException($"Schema file not found: {schemaPath}");
        }

        var (codexVersion, codexPackageJsonPath) = TryGetCodexCliVersion(repoRoot);

        return new UpstreamSchemaMetadata
        {
            RepoRoot = repoRoot,
            SchemaPath = schemaPath,
            SchemaBytes = new FileInfo(schemaPath).Length,
            SchemaSha256 = ComputeSha256Hex(schemaPath),
            CodexCliVersion = codexVersion,
            CodexCliPackageJsonPath = codexPackageJsonPath,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string ComputeSha256Hex(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (string? Version, string? PackageJsonPath) TryGetCodexCliVersion(string repoRoot)
    {
        var packageJsonPath = Path.Combine(repoRoot, "external", "codex", "codex-cli", "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return (null, null);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return (null, packageJsonPath);
        }

        if (doc.RootElement.TryGetProperty("name", out var nameProp) &&
            nameProp.ValueKind == JsonValueKind.String &&
            !string.Equals(nameProp.GetString(), "@openai/codex", StringComparison.Ordinal))
        {
            return (null, packageJsonPath);
        }

        if (doc.RootElement.TryGetProperty("version", out var versionProp) &&
            versionProp.ValueKind == JsonValueKind.String)
        {
            return (versionProp.GetString(), packageJsonPath);
        }

        return (null, packageJsonPath);
    }
}

internal sealed record class UpstreamSchemaMetadata
{
    public required string RepoRoot { get; init; }
    public required string SchemaPath { get; init; }
    public required long SchemaBytes { get; init; }
    public required string SchemaSha256 { get; init; }
    public string? CodexCliVersion { get; init; }
    public string? CodexCliPackageJsonPath { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}

