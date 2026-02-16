using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexSessionLocatorHelpers
{
    internal static HashSet<string> CaptureSessionSnapshot(
        IFileSystem fileSystem,
        ILogger logger,
        string sessionsRoot,
        Regex sessionFilePattern)
    {
        var snapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var existingFiles = fileSystem.GetFiles(sessionsRoot, "*.jsonl");
            foreach (var file in existingFiles)
            {
                var fileName = Path.GetFileName(file);
                if (sessionFilePattern.IsMatch(fileName))
                {
                    snapshot.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error capturing pre-launch session snapshot in {Directory}", sessionsRoot);
        }

        return snapshot;
    }

    internal static string? FindNewSessionFile(
        IFileSystem fileSystem,
        ILogger logger,
        string sessionsRoot,
        DateTime startTimeUtc,
        HashSet<string> baseline,
        Regex sessionFilePattern)
    {
        try
        {
            var jsonlFiles = fileSystem.GetFiles(sessionsRoot, "*.jsonl");

            var candidates = new List<(string Path, DateTime CreatedUtc)>();

            foreach (var filePath in jsonlFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(filePath);

                    if (!sessionFilePattern.IsMatch(fileName))
                    {
                        continue;
                    }

                    if (baseline.Contains(filePath))
                    {
                        continue;
                    }

                    var creationTimeUtc = fileSystem.GetFileCreationTimeUtc(filePath);

                    if (creationTimeUtc >= startTimeUtc)
                    {
                        candidates.Add((filePath, creationTimeUtc));
                    }
                }
                catch (Exception ex)
                {
                    logger.LogTrace(ex, "Error checking file: {FilePath}", filePath);
                }
            }

            var earliest = candidates
                .OrderBy(c => c.CreatedUtc)
                .FirstOrDefault();

            if (earliest != default)
            {
                logger.LogTrace(
                    "Found candidate session file: {FilePath} (created: {CreationTime})",
                    earliest.Path,
                    earliest.CreatedUtc);
                return earliest.Path;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for new session files in: {Directory}", sessionsRoot);
        }

        return null;
    }

    internal static async Task<CodexSessionInfo?> ParseSessionInfoAsync(
        IFileSystem fileSystem,
        ILogger logger,
        string filePath,
        DateTime? creationTimeUtc,
        CancellationToken cancellationToken)
    {
        SessionId? sessionId = null;
        DateTimeOffset? createdAt = null;
        string? workingDirectory = null;
        CodexModel? model = null;

        using var stream = fileSystem.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetString() == "session_meta")
                {
                    if (root.TryGetProperty("timestamp", out var timestampElement))
                    {
                        createdAt = timestampElement.GetDateTimeOffset();
                    }

                    if (root.TryGetProperty("payload", out var payload))
                    {
                        if (payload.TryGetProperty("id", out var idElement))
                        {
                            var idString = idElement.GetString();
                            if (!string.IsNullOrWhiteSpace(idString))
                            {
                                if (SessionId.TryParse(idString, out var parsed))
                                {
                                    sessionId = parsed;
                                }
                            }
                        }

                        if (payload.TryGetProperty("cwd", out var cwdElement))
                        {
                            workingDirectory = cwdElement.GetString();
                        }

                        if (payload.TryGetProperty("model", out var modelElement))
                        {
                            var modelString = modelElement.GetString();
                            if (!string.IsNullOrWhiteSpace(modelString) &&
                                CodexModel.TryParse(modelString, out var parsedModel))
                            {
                                model = parsedModel;
                            }
                        }
                    }

                    break;
                }
            }
            catch (JsonException ex)
            {
                logger.LogTrace(ex, "Error parsing JSON line in file: {FilePath}", filePath);
            }
        }

        if (!sessionId.HasValue)
        {
            sessionId = TryExtractSessionIdFromFilePath(logger, filePath);

            if (!sessionId.HasValue)
            {
                return null;
            }
        }

        var effectiveCreatedAt = createdAt
            ?? (creationTimeUtc.HasValue
                ? new DateTimeOffset(creationTimeUtc.Value, TimeSpan.Zero)
                : DateTimeOffset.UtcNow);

        return new CodexSessionInfo(
            Id: sessionId.Value,
            LogPath: filePath,
            CreatedAt: effectiveCreatedAt,
            WorkingDirectory: workingDirectory,
            Model: model);
    }

    internal static bool MatchesFilter(CodexSessionInfo sessionInfo, SessionFilter filter)
    {
        if (filter.FromDate.HasValue && sessionInfo.CreatedAt < filter.FromDate.Value)
        {
            return false;
        }

        if (filter.ToDate.HasValue && sessionInfo.CreatedAt > filter.ToDate.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkingDirectory) &&
            !string.Equals(sessionInfo.WorkingDirectory, filter.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Model.HasValue)
        {
            if (!sessionInfo.Model.HasValue)
            {
                return false;
            }

            if (!string.Equals(sessionInfo.Model.Value.Value, filter.Model.Value.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.SessionIdPattern))
        {
            if (!MatchesPattern(sessionInfo.Id.Value, filter.SessionIdPattern))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MatchesPattern(string value, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    internal static IEnumerable<(string FilePath, DateTime? CreatedAtUtc)> EnumerateSessionFiles(
        IFileSystem fileSystem,
        ILogger logger,
        string sessionsRoot,
        Regex sessionFilePattern)
    {
        IEnumerable<string> files;

        try
        {
            files = fileSystem.GetFiles(sessionsRoot, "*.jsonl");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate session files in {Directory}", sessionsRoot);
            yield break;
        }

        var candidates = new List<(string FilePath, DateTime? CreatedAtUtc)>();

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            if (!sessionFilePattern.IsMatch(fileName))
            {
                logger.LogTrace("Skipping non-session file {FilePath}", file);
                continue;
            }

            var createdAtUtc = TryGetCreationTimeUtc(fileSystem, logger, file);
            candidates.Add((file, createdAtUtc));
        }

        foreach (var c in candidates
                     .OrderByDescending(c => c.CreatedAtUtc ?? DateTime.MinValue)
                     .ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            yield return c;
        }
    }

    internal static DateTime? TryGetCreationTimeUtc(IFileSystem fileSystem, ILogger logger, string filePath)
    {
        try
        {
            return fileSystem.GetFileCreationTimeUtc(filePath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException or IOException)
        {
            logger.LogTrace(ex, "Unable to read creation time for {FilePath}", filePath);
            return null;
        }
    }

    internal static SessionId? TryExtractSessionIdFromFilePath(ILogger logger, string filePath)
    {
        try
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return null;
            }

            var lastDash = fileNameWithoutExtension.LastIndexOf('-');
            if (lastDash < 0 || lastDash == fileNameWithoutExtension.Length - 1)
            {
                return null;
            }

            var candidate = fileNameWithoutExtension[(lastDash + 1)..];
            return SessionId.Parse(candidate);
        }
        catch (Exception ex)
        {
            logger.LogTrace(ex, "Failed to extract session id from file name {FilePath}", filePath);
            return null;
        }
    }
}
