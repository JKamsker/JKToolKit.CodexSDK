namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the canonical permission value for a managed unix-socket entry.
/// </summary>
public readonly record struct NetworkUnixSocketPermission
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private NetworkUnixSocketPermission(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Network unix-socket permission cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>allow</c> permission.
    /// </summary>
    public static NetworkUnixSocketPermission Allow => new("allow");

    /// <summary>
    /// Gets the <c>none</c> permission.
    /// </summary>
    public static NetworkUnixSocketPermission None => new("none");

    /// <summary>
    /// Parses a network unix-socket permission from a wire value.
    /// </summary>
    public static NetworkUnixSocketPermission Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a network unix-socket permission from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out NetworkUnixSocketPermission permission)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            permission = default;
            return false;
        }

        permission = new NetworkUnixSocketPermission(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="NetworkUnixSocketPermission"/>.
    /// </summary>
    public static implicit operator NetworkUnixSocketPermission(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="NetworkUnixSocketPermission"/> to its wire value.
    /// </summary>
    public static implicit operator string(NetworkUnixSocketPermission permission) => permission.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
