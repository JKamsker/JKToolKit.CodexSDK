namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a Codex service-tier identifier.
/// </summary>
/// <remarks>
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// Known values currently include <c>fast</c> and <c>flex</c>.
/// </remarks>
public readonly record struct CodexServiceTier
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexServiceTier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ServiceTier cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>fast</c> service tier.
    /// </summary>
    public static CodexServiceTier Fast => new("fast");

    /// <summary>
    /// Gets the <c>flex</c> service tier.
    /// </summary>
    public static CodexServiceTier Flex => new("flex");

    /// <summary>
    /// Parses a service tier from a wire value.
    /// </summary>
    public static CodexServiceTier Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a service tier from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexServiceTier serviceTier)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            serviceTier = default;
            return false;
        }

        serviceTier = new CodexServiceTier(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="CodexServiceTier"/>.
    /// </summary>
    public static implicit operator CodexServiceTier(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="CodexServiceTier"/> to its wire value.
    /// </summary>
    public static implicit operator string(CodexServiceTier serviceTier) => serviceTier.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
