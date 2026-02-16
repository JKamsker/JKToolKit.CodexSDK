using JKToolKit.CodexSDK.Exec.Internal;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexDiagnosticsSanitizer
{
    internal static string Sanitize(string? input, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sanitized = input.TrimEnd();

        sanitized = CodexClientRegexes.BearerRegex().Replace(sanitized, "$1[REDACTED]");
        sanitized = CodexClientRegexes.KeyValueSecretRegex().Replace(sanitized, m => $"{m.Groups[1].Value}=[REDACTED]");
        sanitized = CodexClientRegexes.OpenAiSkRegex().Replace(sanitized, "sk-[REDACTED]");
        sanitized = CodexClientRegexes.GitHubTokenRegex().Replace(sanitized, "[REDACTED_TOKEN]");
        sanitized = CodexClientRegexes.AwsAccessKeyRegex().Replace(sanitized, "AKIA[REDACTED]");
        sanitized = CodexClientRegexes.EmailRegex().Replace(sanitized, "[REDACTED_EMAIL]");

        if (maxChars > 0 && sanitized.Length > maxChars)
        {
            sanitized = TruncateWithoutSplittingMarkers(sanitized, maxChars);
        }

        return sanitized;
    }

    private static string TruncateWithoutSplittingMarkers(string sanitized, int maxChars)
    {
        if (maxChars <= 0 || sanitized.Length <= maxChars)
        {
            return sanitized;
        }

        var cut = maxChars;

        // Avoid cutting in the middle of any "[...]" placeholder such as "[REDACTED]" or "[REDACTED_EMAIL]".
        var open = sanitized.LastIndexOf('[', cut - 1);
        if (open >= 0)
        {
            var closeBeforeCut = sanitized.IndexOf(']', open);
            if (closeBeforeCut < 0 || closeBeforeCut >= cut)
            {
                var close = sanitized.IndexOf(']', cut);
                if (close >= 0 && close - open <= 64)
                {
                    cut = close + 1;
                }
                else
                {
                    cut = open;
                }
            }
        }

        if (cut <= 0)
        {
            return string.Empty;
        }

        if (cut > sanitized.Length)
        {
            cut = sanitized.Length;
        }

        return sanitized[..cut];
    }

    internal static string[] SanitizeLines(IEnumerable<string> lines, int maxLines, int maxCharsPerLine)
    {
        if (lines is null)
        {
            return Array.Empty<string>();
        }

        if (maxLines <= 0)
        {
            return Array.Empty<string>();
        }

        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(maxLines)
            .Select(l => Sanitize(l, maxCharsPerLine))
            .ToArray();
    }
}
