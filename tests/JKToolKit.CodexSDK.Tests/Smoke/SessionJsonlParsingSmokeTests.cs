using System.Text.RegularExpressions;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Sdk;

namespace JKToolKit.CodexSDK.Tests.Smoke;

public sealed class SessionJsonlParsingSmokeTests
{
    private static readonly Regex YearRegex = new(@"^\d{4}$", RegexOptions.Compiled);
    private static readonly Regex MonthRegex = new(@"^\d{2}$", RegexOptions.Compiled);
    private static readonly Regex DayRegex = new(@"^\d{2}$", RegexOptions.Compiled);

    [Fact]
    [Trait("Category", "Smoke")]
    public void Parses_All_Rollout_Session_Jsonl_Lines()
    {
        if (!IsEnabled())
        {
            return;
        }

        var sessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex",
            "sessions");

        if (!Directory.Exists(sessionsRoot))
        {
            return;
        }

        var files = Directory.EnumerateFiles(sessionsRoot, "rollout-*.jsonl", SearchOption.AllDirectories)
            .Where(f => IsEligibleRolloutPath(sessionsRoot, f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        files.Length.Should().BeGreaterThan(0, "there should be at least one rollout session log");

        var parser = new JsonlEventParser(NullLogger<JsonlEventParser>.Instance);

        const int maxErrors = 50;
        var errors = new List<string>(capacity: Math.Min(100, maxErrors));

        foreach (var file in files)
        {
            ParseFile(parser, file, errors, maxErrors);
            if (errors.Count >= maxErrors)
                break;
        }

        if (errors.Count > 0)
        {
            throw new XunitException("JSONL parsing smoke test failed:\n" + string.Join("\n", errors));
        }
    }

    private static void ParseFile(JsonlEventParser parser, string file, List<string> errors, int maxErrors)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
        }
        catch (IOException)
        {
            // Session file may be actively written/locked by a running Codex process.
            // Skip it to avoid flaky local runs; older sessions still validate schema coverage.
            return;
        }

        using (stream)
        {
            using var reader = new StreamReader(stream);

            var lineNo = 0;
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                lineNo++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!parser.TryParseLine(line, out var evt, out var error))
                {
                    errors.Add($"{file}:{lineNo}: parse failed: {error}");
                    if (errors.Count >= maxErrors)
                        return;
                    continue;
                }

                if (evt is UnknownCodexEvent)
                {
                    errors.Add($"{file}:{lineNo}: unknown event type: {evt.Type}");
                    if (errors.Count >= maxErrors)
                        return;
                }

                if (evt is ResponseItemEvent responseItem)
                {
                    if (responseItem.Payload is UnknownResponseItemPayload)
                    {
                        errors.Add($"{file}:{lineNo}: unknown response_item payload_type: {responseItem.PayloadType}");
                        if (errors.Count >= maxErrors)
                            return;
                    }

                    if (responseItem.Payload is MessageResponseItemPayload msg &&
                        msg.Content.Any(p => p is UnknownResponseMessageContentPart))
                    {
                        errors.Add($"{file}:{lineNo}: unknown message content part in response_item payload_type=message");
                        if (errors.Count >= maxErrors)
                            return;
                    }
                }

                if (evt is CompactedEvent compacted)
                {
                    if (compacted.ReplacementHistory.Any(p => p is UnknownResponseItemPayload))
                    {
                        errors.Add($"{file}:{lineNo}: unknown replacement_history item type in compacted event");
                        if (errors.Count >= maxErrors)
                            return;
                    }

                    foreach (var item in compacted.ReplacementHistory.OfType<MessageResponseItemPayload>())
                    {
                        if (item.Content.Any(p => p is UnknownResponseMessageContentPart))
                        {
                            errors.Add($"{file}:{lineNo}: unknown message content part in compacted.replacement_history");
                            if (errors.Count >= maxErrors)
                                return;
                        }
                    }
                }
            }
        }
    }

    private static bool IsEnabled()
    {
        var enabled = Environment.GetEnvironmentVariable("CODEX_SESSION_JSONL_SMOKE");
        return enabled == "1" || enabled?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsEligibleRolloutPath(string sessionsRoot, string fullPath)
    {
        var relative = Path.GetRelativePath(sessionsRoot, fullPath);
        var parts = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return false;

        if (!YearRegex.IsMatch(parts[0]) || !MonthRegex.IsMatch(parts[1]) || !DayRegex.IsMatch(parts[2]))
            return false;

        var fileName = parts[3];
        return fileName.StartsWith("rollout-", StringComparison.OrdinalIgnoreCase) &&
               fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);
    }
}
