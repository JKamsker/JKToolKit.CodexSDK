namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a Codex web search mode identifier.
/// </summary>
/// <remarks>
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// <para>
/// Known values in Codex include <c>disabled</c>, <c>cached</c>, and <c>live</c>.
/// </para>
/// </remarks>
public readonly record struct CodexWebSearchMode
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexWebSearchMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("WebSearchMode cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>disabled</c> web search mode.
    /// </summary>
    public static CodexWebSearchMode Disabled => new("disabled");

    /// <summary>
    /// Gets the <c>cached</c> web search mode.
    /// </summary>
    public static CodexWebSearchMode Cached => new("cached");

    /// <summary>
    /// Gets the <c>live</c> web search mode.
    /// </summary>
    public static CodexWebSearchMode Live => new("live");

    /// <summary>
    /// Parses a web search mode from a wire value.
    /// </summary>
    public static CodexWebSearchMode Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a web search mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexWebSearchMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        mode = new CodexWebSearchMode(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="CodexWebSearchMode"/>.
    /// </summary>
    public static implicit operator CodexWebSearchMode(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="CodexWebSearchMode"/> to its wire value.
    /// </summary>
    public static implicit operator string(CodexWebSearchMode mode) => mode.Value ?? string.Empty;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value ?? string.Empty;
}
