using System.Text.Json;
using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Abstractions;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexUncorrelatedSessionDiscoveryHelpers
{
    private static readonly Regex RolloutTimestampedFileNamePattern = new(
        @"^rollout-(\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2})-(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static HashSet<string> CaptureSessionSnapshot(
        IFileSystem fileSystem,
        ILogger logger,
        IEnumerable<string> scanRoots,
        Regex sessionFilePattern)
    {
        var snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var root in scanRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                IEnumerable<string> existingFiles;
                try
                {
                    if (!fileSystem.DirectoryExists(root))
                    {
                        continue;
                    }

                    existingFiles = fileSystem.GetFiles(root, "*.jsonl");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error capturing pre-launch session snapshot in {Directory}", root);
                    continue;
                }

                foreach (var file in existingFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (sessionFilePattern.IsMatch(fileName))
                    {
                        snapshot.Add(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error capturing pre-launch session snapshot");
        }

        return snapshot;
    }

    internal static async Task<string?> FindNewSessionFileAsync(
        IFileSystem fileSystem,
        ILogger logger,
        IEnumerable<string> scanRoots,
        DateTimeOffset startTimeUtc,
        HashSet<string> baseline,
        Regex sessionFilePattern,
        CancellationToken cancellationToken)
    {
        try
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(string Path, DateTimeOffset? TimestampUtc)>();

            foreach (var root in scanRoots)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                if (!fileSystem.DirectoryExists(root))
                {
                    continue;
                }

                IEnumerable<string> jsonlFiles;
                try
                {
                    jsonlFiles = fileSystem.GetFiles(root, "*.jsonl");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error searching for new session files in: {Directory}", root);
                    continue;
                }

                foreach (var filePath in jsonlFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(filePath) || !visited.Add(filePath))
                    {
                        continue;
                    }

                    if (!IsUnderRoot(filePath, root))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(filePath);
                    if (!sessionFilePattern.IsMatch(fileName))
                    {
                        continue;
                    }

                    var existedAtBaseline = baseline.Contains(filePath);

                    DateTimeOffset? timestampUtc = null;
                    if (TryParseRolloutTimestampUtc(filePath, out var parsedTimestamp))
                    {
                        timestampUtc = parsedTimestamp;
                    }
                    else
                    {
                        timestampUtc = await TryReadSessionMetaTimestampAsync(fileSystem, filePath, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    // If this file existed when we took the baseline snapshot and we can't determine when it was created,
                    // treat it as "old" to avoid attaching to unrelated sessions.
                    if (existedAtBaseline && timestampUtc is null)
                    {
                        continue;
                    }

                    if (timestampUtc is { } ts && ts < startTimeUtc)
                    {
                        continue;
                    }

                    candidates.Add((filePath, timestampUtc));
                }
            }

            var earliest = candidates
                .OrderBy(c => c.TimestampUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (earliest != default)
            {
                logger.LogTrace(
                    "Found candidate session file: {FilePath} (timestamp: {Timestamp})",
                    earliest.Path,
                    earliest.TimestampUtc);
                return earliest.Path;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for new session files (uncorrelated discovery)");
        }

        return null;
    }

    internal static bool TryParseRolloutTimestampUtc(string filePath, out DateTimeOffset timestampUtc)
    {
        timestampUtc = default;

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return false;
        }

        var match = RolloutTimestampedFileNamePattern.Match(fileNameWithoutExtension);
        if (!match.Success)
        {
            return false;
        }

        var ts = match.Groups[1].Value;
        if (ts.Length != 19 || ts[10] != 'T')
        {
            return false;
        }

        var date = ts[..10];
        var time = ts[11..].Replace('-', ':');
        var normalized = $"{date}T{time}";

        if (!DateTime.TryParseExact(
                normalized,
                "yyyy-MM-dd'T'HH:mm:ss",
                provider: null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return false;
        }

        timestampUtc = new DateTimeOffset(parsed, TimeSpan.Zero);
        return true;
    }

    private static async Task<DateTimeOffset?> TryReadSessionMetaTimestampAsync(
        IFileSystem fileSystem,
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = fileSystem.OpenRead(filePath);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetString() == "session_meta" &&
                    root.TryGetProperty("timestamp", out var timestampElement))
                {
                    return timestampElement.GetDateTimeOffset();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Best-effort metadata read; ignore and let the caller decide.
        }

        return null;
    }

    private static bool IsUnderRoot(string filePath, string root)
    {
        try
        {
            var rootFull = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                           Path.DirectorySeparatorChar;

            var fileFull = Path.GetFullPath(filePath);
            return fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
