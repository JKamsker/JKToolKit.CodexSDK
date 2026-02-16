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
            sanitized = sanitized[..maxChars];
        }

        return sanitized;
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

