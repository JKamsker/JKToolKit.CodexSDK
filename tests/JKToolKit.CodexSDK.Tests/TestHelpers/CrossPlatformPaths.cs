namespace JKToolKit.CodexSDK.Tests.TestHelpers;

/// <summary>
/// Provides cross-platform fully-qualified paths for tests that need to pass
/// <see cref="System.IO.Path.IsPathFullyQualified(string)"/> on all platforms.
/// </summary>
internal static class XPaths
{
    /// <summary>Windows: <c>C:\{part}</c>; Linux/macOS: <c>/tmp/{part}</c>.</summary>
    public static string Abs(string part) =>
        OperatingSystem.IsWindows()
            ? @"C:\" + part.Replace('/', '\\')
            : "/tmp/" + part.Replace('\\', '/');

    /// <summary>JSON-friendly absolute path (forward slashes).</summary>
    public static string JsonAbs(string part) =>
        OperatingSystem.IsWindows()
            ? "C:/" + part.Replace('\\', '/')
            : "/tmp/" + part.Replace('\\', '/');

    /// <summary>JSON-escaped absolute path for raw JSON strings.</summary>
    public static string JsonEsc(string part) =>
        OperatingSystem.IsWindows()
            ? "C:\\\\" + part.Replace("/", "\\\\").Replace("\\", "\\\\")
            : "/tmp/" + part.Replace('\\', '/');
}
