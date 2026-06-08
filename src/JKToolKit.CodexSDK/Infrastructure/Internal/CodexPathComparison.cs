namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexPathComparison
{
    private static readonly char[] DirectorySeparators = ['/', '\\'];

    /// <summary>
    /// Compares two paths after normalizing with <see cref="Path.GetFullPath(string)"/>
    /// and trimming trailing directory separators, matching the upstream
    /// <c>AbsolutePathBuf</c> (path-absolutize) normalization semantics.
    /// </summary>
    internal static bool NormalizedPathEquals(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b);
        }

        try
        {
            a = Path.GetFullPath(RemoveWindowsVerbatimPrefix(a)).TrimEnd(DirectorySeparators);
            b = Path.GetFullPath(RemoveWindowsVerbatimPrefix(b)).TrimEnd(DirectorySeparators);
        }
        catch
        {
            // If normalization fails (invalid chars, etc.) fall through to raw comparison.
        }

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveWindowsVerbatimPrefix(string path)
    {
        const string verbatimUncPrefix = @"\\?\UNC\";
        const string verbatimPrefix = @"\\?\";

        if (path.StartsWith(verbatimUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path[verbatimUncPrefix.Length..];
        }

        return path.StartsWith(verbatimPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[verbatimPrefix.Length..]
            : path;
    }
}
