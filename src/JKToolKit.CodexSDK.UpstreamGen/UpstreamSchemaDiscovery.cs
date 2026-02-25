using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKToolKit.CodexSDK.UpstreamGen;

internal static class UpstreamSchemaDiscovery
{
    private const string UpstreamCodexVersionPinFileName = "UPSTREAM_CODEX_VERSION.txt";

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
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
        var (pinnedVersion, pinnedVersionPath) = TryGetPinnedCodexVersion(repoRoot);

        var normalized = ComputeNormalizedSha256AndBytes(schemaPath);

        return new UpstreamSchemaMetadata
        {
            SchemaPath = MakeRepoRelativePath(repoRoot, schemaPath),
            SchemaByteCount = normalized.NormalizedByteCount,
            SchemaSha256 = normalized.Sha256,
            CodexCliVersion = pinnedVersion ?? codexVersion,
            CodexCliVersionPinPath = pinnedVersionPath is null ? null : MakeRepoRelativePath(repoRoot, pinnedVersionPath),
            CodexCliPackageJsonPath = codexPackageJsonPath is null ? null : MakeRepoRelativePath(repoRoot, codexPackageJsonPath),
        };
    }

    private static string MakeRepoRelativePath(string repoRoot, string path)
    {
        try
        {
            var relative = Path.GetRelativePath(repoRoot, path);
            return relative.Replace('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return path.Replace('\\', '/');
        }
    }

    private static (long NormalizedByteCount, string Sha256) ComputeNormalizedSha256AndBytes(string path)
    {
        var bytes = File.ReadAllBytes(path);

        var normalized = NormalizeNewlines(bytes);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(normalized);
        return (normalized.Length, Convert.ToHexString(hash).ToLowerInvariant());
    }

    private static byte[] NormalizeNewlines(byte[] bytes)
    {
        var containsCr = false;
        foreach (var b in bytes)
        {
            if (b == (byte)'\r')
            {
                containsCr = true;
                break;
            }
        }

        if (!containsCr)
        {
            return bytes;
        }

        var output = new byte[bytes.Length];
        var outIndex = 0;

        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];

            if (b == (byte)'\r')
            {
                if (i + 1 < bytes.Length && bytes[i + 1] == (byte)'\n')
                {
                    i++;
                }

                output[outIndex++] = (byte)'\n';
                continue;
            }

            output[outIndex++] = b;
        }

        if (outIndex == output.Length)
        {
            return output;
        }

        return output.AsSpan(0, outIndex).ToArray();
    }

    private static (string? Version, string? VersionPinPath) TryGetPinnedCodexVersion(string repoRoot)
    {
        var versionPath = Path.Combine(repoRoot, UpstreamCodexVersionPinFileName);
        if (!File.Exists(versionPath))
        {
            return (null, null);
        }

        foreach (var rawLine in File.ReadLines(versionPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            return (line, versionPath);
        }

        return (null, null);
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
    public required string SchemaPath { get; init; }
    public required long SchemaByteCount { get; init; }
    public required string SchemaSha256 { get; init; }
    public string? CodexCliVersion { get; init; }
    public string? CodexCliVersionPinPath { get; init; }
    public string? CodexCliPackageJsonPath { get; init; }
}
