using System.Text.Json;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexSessionThreadNameIndex
{
    private const string SessionIndexFileName = "session_index.jsonl";

    internal static IReadOnlyDictionary<string, string> LoadLatestThreadNames(
        string sessionsRoot,
        string? codexHomeDirectory = null)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateCandidatePaths(sessionsRoot, codexHomeDirectory))
        {
            MergeLatestThreadNames(path, names);
        }

        return names;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string sessionsRoot, string? codexHomeDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        AddCandidate(codexHomeDirectory);

        if (!string.IsNullOrWhiteSpace(sessionsRoot))
        {
            var normalizedSessionsRoot = sessionsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var leaf = Path.GetFileName(normalizedSessionsRoot);
            if (string.Equals(leaf, "sessions", StringComparison.OrdinalIgnoreCase))
            {
                var codexHome = Directory.GetParent(normalizedSessionsRoot)?.FullName;
                AddCandidate(codexHome);
            }
        }

        AddCandidate(Environment.GetEnvironmentVariable("CODEX_HOME"));
        return candidates;

        void AddCandidate(string? codexHome)
        {
            if (string.IsNullOrWhiteSpace(codexHome))
            {
                return;
            }

            var candidate = Path.Combine(codexHome, SessionIndexFileName);
            if (seen.Add(candidate))
            {
                candidates.Add(candidate);
            }
        }
    }

    private static void MergeLatestThreadNames(string sessionIndexPath, IDictionary<string, string> names)
    {
        if (!File.Exists(sessionIndexPath))
        {
            return;
        }

        try
        {
            using var stream = File.Open(sessionIndexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!TryParseEntry(line, out var sessionId, out var threadName))
                {
                    continue;
                }

                names[sessionId] = threadName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort only; session listing must still work when the index is unavailable.
        }
    }

    private static bool TryParseEntry(string line, out string sessionId, out string threadName)
    {
        sessionId = string.Empty;
        threadName = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!TryGetRequiredString(root, "id", out sessionId) ||
                !TryGetRequiredString(root, "thread_name", out threadName))
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetRequiredString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }
}
