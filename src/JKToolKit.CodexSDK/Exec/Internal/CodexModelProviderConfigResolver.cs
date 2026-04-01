using System.Text.RegularExpressions;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static partial class CodexModelProviderConfigResolver
{
    private const string ConfigFileName = "config.toml";

    internal static string? ResolveActiveModelProvider(
        CodexClientOptions clientOptions,
        string sessionsRoot)
    {
        ArgumentNullException.ThrowIfNull(clientOptions);
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        var configPath = ResolveConfigPath(clientOptions.CodexHomeDirectory, sessionsRoot);
        if (configPath is null || !File.Exists(configPath))
        {
            return null;
        }

        try
        {
            return ParseActiveModelProvider(File.ReadAllLines(configPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static string? ParseActiveModelProvider(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        string? topLevelProfile = null;
        string? topLevelModelProvider = null;
        string? currentProfileSection = null;
        var insideAnySection = false;
        var profileProviders = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var rawLine in lines)
        {
            if (rawLine is null)
            {
                continue;
            }

            var line = StripComments(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TryParseSection(line, out var sectionName))
            {
                insideAnySection = true;
                currentProfileSection = sectionName;
                continue;
            }

            if (!TryParseStringAssignment(line, out var key, out var value))
            {
                continue;
            }

            // Only treat keys as top-level when we haven't entered any section yet.
            // Non-profile sections (e.g. [model_providers.openai]) set currentProfileSection
            // to null but should NOT be treated as top-level context.
            if (!insideAnySection)
            {
                if (string.Equals(key, "profile", StringComparison.Ordinal))
                {
                    topLevelProfile = value;
                }
                else if (string.Equals(key, "model_provider", StringComparison.Ordinal))
                {
                    topLevelModelProvider = value;
                }

                continue;
            }

            if (currentProfileSection is not null &&
                string.Equals(key, "model_provider", StringComparison.Ordinal))
            {
                profileProviders[currentProfileSection] = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(topLevelProfile) &&
            profileProviders.TryGetValue(topLevelProfile, out var profileProvider) &&
            !string.IsNullOrWhiteSpace(profileProvider))
        {
            return profileProvider;
        }

        return string.IsNullOrWhiteSpace(topLevelModelProvider) ? null : topLevelModelProvider;
    }

    private static string? ResolveConfigPath(string? explicitCodexHomeDirectory, string sessionsRoot)
    {
        var candidates = new[]
        {
            explicitCodexHomeDirectory,
            TryInferCodexHomeFromSessionsRoot(sessionsRoot),
            Environment.GetEnvironmentVariable("CODEX_HOME")
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var configPath = Path.Combine(candidate, ConfigFileName);
            if (File.Exists(configPath))
            {
                return configPath;
            }
        }

        return null;
    }

    private static string? TryInferCodexHomeFromSessionsRoot(string sessionsRoot)
    {
        if (string.IsNullOrWhiteSpace(sessionsRoot))
        {
            return null;
        }

        var normalized = sessionsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(Path.GetFileName(normalized), "sessions", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetParent(normalized)?.FullName
            : null;
    }

    private static string StripComments(string line)
    {
        var result = line;
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inQuotes = !inQuotes;
            }

            if (ch == '#' && !inQuotes)
            {
                result = line[..i];
                break;
            }
        }

        return result;
    }

    private static bool TryParseSection(string line, out string? profileName)
    {
        profileName = null;
        if (!line.StartsWith('[') || !line.EndsWith(']'))
        {
            return false;
        }

        var section = line[1..^1].Trim();
        var match = ProfileSectionRegex().Match(section);
        if (!match.Success)
        {
            return true;
        }

        profileName = match.Groups["profile"].Value;
        return true;
    }

    private static bool TryParseStringAssignment(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return false;
        }

        key = line[..separatorIndex].Trim();
        var rawValue = line[(separatorIndex + 1)..].Trim();
        if (!rawValue.StartsWith('"') || !rawValue.EndsWith('"') || rawValue.Length < 2)
        {
            return false;
        }

        value = Regex.Unescape(rawValue[1..^1]);
        return true;
    }

    [GeneratedRegex("^profiles\\.(?<profile>[^.]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileSectionRegex();
}
