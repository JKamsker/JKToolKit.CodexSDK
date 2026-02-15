using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the response payload from the <c>initialize</c> request.
/// </summary>
public sealed record AppServerInitializeResult
{
    /// <summary>
    /// Gets the raw JSON result payload.
    /// </summary>
    public JsonElement Raw { get; }

    /// <summary>
    /// Gets the server-provided user agent string, if present.
    /// </summary>
    public string? UserAgent { get; }

    /// <summary>
    /// Gets a best-effort parsed Codex build version extracted from <see cref="UserAgent"/>, when available.
    /// </summary>
    /// <remarks>
    /// This is best-effort only. User agent formats may change and may include non-semver tokens.
    /// </remarks>
    public Version? CodexBuildVersion { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppServerInitializeResult"/> from the raw JSON payload.
    /// </summary>
    public AppServerInitializeResult(JsonElement raw)
    {
        Raw = raw;

        UserAgent = raw.ValueKind == JsonValueKind.Object &&
                    raw.TryGetProperty("userAgent", out var ua) &&
                    ua.ValueKind == JsonValueKind.String
            ? ua.GetString()
            : null;

        CodexBuildVersion = TryParseCodexBuildVersion(UserAgent);
    }

    private static Version? TryParseCodexBuildVersion(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        // Codex user agents typically start with: "<originator>/<build_version> ...".
        // Example: "codex_vscode/0.0.0 (Windows 11; x86_64) wezterm/20240203 (codex_vscode; 0.1.0)".
        var slash = userAgent.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0 || slash + 1 >= userAgent.Length)
        {
            return null;
        }

        var afterSlash = userAgent.AsSpan(slash + 1);
        var end = afterSlash.IndexOf(' ');
        if (end >= 0)
        {
            afterSlash = afterSlash[..end];
        }

        // Allow semver-like tokens with suffixes (e.g. "1.2.3-alpha.1") by taking the numeric prefix.
        var token = afterSlash.ToString();
        var numeric = token;
        var cut = 0;
        while (cut < numeric.Length && (char.IsDigit(numeric[cut]) || numeric[cut] == '.'))
        {
            cut++;
        }

        numeric = cut > 0 ? numeric[..cut] : numeric;

        return Version.TryParse(numeric, out var v) ? v : null;
    }
}

