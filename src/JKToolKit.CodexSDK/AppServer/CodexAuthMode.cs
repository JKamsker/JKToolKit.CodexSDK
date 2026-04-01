namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the OpenAI authentication mode reported by the app-server.
/// </summary>
public readonly record struct CodexAuthMode
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private CodexAuthMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Auth mode cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the <c>apikey</c> auth mode.
    /// </summary>
    public static CodexAuthMode ApiKey => new("apikey");

    /// <summary>
    /// Gets the <c>chatgpt</c> auth mode.
    /// </summary>
    public static CodexAuthMode ChatGpt => new("chatgpt");

    /// <summary>
    /// Gets the <c>chatgptAuthTokens</c> auth mode.
    /// </summary>
    public static CodexAuthMode ChatGptAuthTokens => new("chatgptAuthTokens");

    /// <summary>
    /// Parses an auth mode from a wire value.
    /// </summary>
    public static CodexAuthMode Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse an auth mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out CodexAuthMode authMode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            authMode = default;
            return false;
        }

        authMode = new CodexAuthMode(value);
        return true;
    }

    /// <summary>
    /// Converts a wire string to a <see cref="CodexAuthMode"/>.
    /// </summary>
    public static implicit operator CodexAuthMode(string value) => Parse(value);

    /// <summary>
    /// Converts an auth mode to its wire representation.
    /// </summary>
    public static implicit operator string(CodexAuthMode authMode) => authMode.Value;

    /// <inheritdoc />
    public override string ToString() => Value;
}
