using System.Text.RegularExpressions;

namespace JKToolKit.CodexSDK.Exec.Internal;

internal static partial class CodexClientRegexes
{
    [GeneratedRegex(@"(?:session(?:[_\s-]?id)?|sid)\s*[:=]\s*([0-9a-fA-F\-]+)", RegexOptions.IgnoreCase)]
    internal static partial Regex SessionIdRegex();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    internal static partial Regex EmailRegex();

    [GeneratedRegex(@"(authorization\s*[:=]\s*bearer\s+)([^\s""]+)", RegexOptions.IgnoreCase)]
    internal static partial Regex BearerRegex();

    [GeneratedRegex(@"\b(api[_-]?key|token|access[_-]?token|refresh[_-]?token|openai[_-]?api[_-]?key|github[_-]?token)\b\s*[:=]\s*([^\s""]+)", RegexOptions.IgnoreCase)]
    internal static partial Regex KeyValueSecretRegex();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9]{20,}\b")]
    internal static partial Regex OpenAiSkRegex();

    [GeneratedRegex(@"\b(gho|ghp|ghu|ghs|ghr)_[A-Za-z0-9_]{20,}\b|\bgithub_pat_[A-Za-z0-9_]{20,}\b")]
    internal static partial Regex GitHubTokenRegex();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    internal static partial Regex AwsAccessKeyRegex();
}

