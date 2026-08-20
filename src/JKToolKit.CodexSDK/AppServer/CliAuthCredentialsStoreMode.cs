namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the CLI auth credentials store mode reported by config requirements.
/// </summary>
public readonly record struct CliAuthCredentialsStoreMode
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CliAuthCredentialsStoreMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("CLI auth credentials store mode cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the <c>file</c> credentials store mode.
    /// </summary>
    public static CliAuthCredentialsStoreMode File => new("file");

    /// <summary>
    /// Gets the <c>keyring</c> credentials store mode.
    /// </summary>
    public static CliAuthCredentialsStoreMode Keyring => new("keyring");

    /// <summary>
    /// Gets the <c>auto</c> credentials store mode.
    /// </summary>
    public static CliAuthCredentialsStoreMode Auto => new("auto");

    /// <summary>
    /// Gets the <c>ephemeral</c> credentials store mode.
    /// </summary>
    public static CliAuthCredentialsStoreMode Ephemeral => new("ephemeral");

    /// <summary>
    /// Parses a credentials store mode from a wire value.
    /// </summary>
    public static CliAuthCredentialsStoreMode Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a credentials store mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CliAuthCredentialsStoreMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        mode = new CliAuthCredentialsStoreMode(value);
        return true;
    }

    /// <summary>
    /// Converts a wire string to a <see cref="CliAuthCredentialsStoreMode"/>.
    /// </summary>
    public static implicit operator CliAuthCredentialsStoreMode(string value) => Parse(value);

    /// <summary>
    /// Converts a mode to its wire representation.
    /// </summary>
    public static implicit operator string(CliAuthCredentialsStoreMode mode) => mode.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
