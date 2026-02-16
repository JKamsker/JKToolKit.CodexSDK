using System.Text.RegularExpressions;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static partial class CodexSessionFilePattern
{
    [GeneratedRegex(@"^(rollout-.*\.jsonl|.*-[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\.jsonl)$", RegexOptions.IgnoreCase)]
    private static partial Regex CreateCore();

    internal static Regex Create() => CreateCore();
}

