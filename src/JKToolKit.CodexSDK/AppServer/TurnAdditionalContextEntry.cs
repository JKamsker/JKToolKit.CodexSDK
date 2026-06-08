namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// A client-provided context fragment sent with <c>turn/start</c> or <c>turn/steer</c>.
/// </summary>
public sealed record class TurnAdditionalContextEntry
{
    /// <summary>
    /// Gets or sets the context text value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets or sets the context kind.
    /// </summary>
    public TurnAdditionalContextKind Kind { get; init; } = TurnAdditionalContextKind.Untrusted;
}

/// <summary>
/// Represents a turn additional-context kind wire value.
/// </summary>
public readonly record struct TurnAdditionalContextKind
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private TurnAdditionalContextKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Additional context kind cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>untrusted</c> kind.
    /// </summary>
    public static TurnAdditionalContextKind Untrusted => new("untrusted");

    /// <summary>
    /// Gets the <c>application</c> kind.
    /// </summary>
    public static TurnAdditionalContextKind Application => new("application");

    /// <summary>
    /// Parses a context kind from a wire value.
    /// </summary>
    public static TurnAdditionalContextKind Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a context kind from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out TurnAdditionalContextKind kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            kind = default;
            return false;
        }

        kind = new TurnAdditionalContextKind(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="TurnAdditionalContextKind"/>.
    /// </summary>
    public static implicit operator TurnAdditionalContextKind(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="TurnAdditionalContextKind"/> to its wire value.
    /// </summary>
    public static implicit operator string(TurnAdditionalContextKind kind) => kind.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
