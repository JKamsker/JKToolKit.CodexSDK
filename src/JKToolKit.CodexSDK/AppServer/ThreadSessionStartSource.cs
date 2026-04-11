namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Identifies why a new thread session was started.
/// </summary>
public readonly record struct ThreadSessionStartSource
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private ThreadSessionStartSource(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Session start source cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Marks a normal startup-created session.
    /// </summary>
    public static ThreadSessionStartSource Startup => new("startup");

    /// <summary>
    /// Marks a replacement session created after clearing the current session.
    /// </summary>
    public static ThreadSessionStartSource Clear => new("clear");

    /// <summary>
    /// Parses a session-start source from the wire value.
    /// </summary>
    public static ThreadSessionStartSource Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a session-start source from the wire value.
    /// </summary>
    public static bool TryParse(string? value, out ThreadSessionStartSource source)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            source = default;
            return false;
        }

        source = new ThreadSessionStartSource(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a session-start source.
    /// </summary>
    public static implicit operator ThreadSessionStartSource(string value) => Parse(value);

    /// <summary>
    /// Converts a session-start source to its wire value.
    /// </summary>
    public static implicit operator string(ThreadSessionStartSource source) => source.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
