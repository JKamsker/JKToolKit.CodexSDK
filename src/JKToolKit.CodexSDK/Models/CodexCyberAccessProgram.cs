namespace JKToolKit.CodexSDK.Models;

/// <summary>
/// Identifies an upstream Cyber access program requested for a turn.
/// </summary>
public readonly record struct CodexCyberAccessProgram
{
    /// <summary>
    /// Gets the underlying wire and CLI value.
    /// </summary>
    public string Value { get; }

    private CodexCyberAccessProgram(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Cyber access program cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the standard Cyber program.
    /// </summary>
    public static CodexCyberAccessProgram Standard => new("standard");

    /// <summary>
    /// Gets the Daybreak blue-team program.
    /// </summary>
    public static CodexCyberAccessProgram DaybreakBlue => new("daybreak_blue");

    /// <summary>
    /// Gets the Daybreak red-team program.
    /// </summary>
    public static CodexCyberAccessProgram DaybreakRed => new("daybreak_red");

    /// <summary>
    /// Returns the underlying wire and CLI value.
    /// </summary>
    public override string ToString() => Value;
}
