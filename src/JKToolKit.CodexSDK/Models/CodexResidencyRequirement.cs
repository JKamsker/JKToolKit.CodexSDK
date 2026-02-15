namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Represents a Codex residency requirement identifier.
/// </summary>
/// <remarks>
/// Residency requirements are used to constrain where certain services or data processing may occur.
/// This value object is forward-compatible: callers may use any string value supported by their Codex version.
/// </remarks>
public readonly record struct CodexResidencyRequirement
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexResidencyRequirement(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ResidencyRequirement cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>us</c> residency requirement.
    /// </summary>
    public static CodexResidencyRequirement Us => new("us");

    /// <summary>
    /// Parses a residency requirement from a wire value.
    /// </summary>
    public static CodexResidencyRequirement Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a residency requirement from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexResidencyRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            requirement = default;
            return false;
        }

        requirement = new CodexResidencyRequirement(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="CodexResidencyRequirement"/>.
    /// </summary>
    public static implicit operator CodexResidencyRequirement(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="CodexResidencyRequirement"/> to its wire value.
    /// </summary>
    public static implicit operator string(CodexResidencyRequirement requirement) => requirement.Value ?? string.Empty;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value ?? string.Empty;
}
