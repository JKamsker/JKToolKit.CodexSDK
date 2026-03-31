namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the canonical permission value for a managed network domain entry.
/// </summary>
public readonly record struct NetworkDomainPermission
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private NetworkDomainPermission(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Network domain permission cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>allow</c> permission.
    /// </summary>
    public static NetworkDomainPermission Allow => new("allow");

    /// <summary>
    /// Gets the <c>deny</c> permission.
    /// </summary>
    public static NetworkDomainPermission Deny => new("deny");

    /// <summary>
    /// Parses a network domain permission from a wire value.
    /// </summary>
    public static NetworkDomainPermission Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a network domain permission from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out NetworkDomainPermission permission)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            permission = default;
            return false;
        }

        permission = new NetworkDomainPermission(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="NetworkDomainPermission"/>.
    /// </summary>
    public static implicit operator NetworkDomainPermission(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="NetworkDomainPermission"/> to its wire value.
    /// </summary>
    public static implicit operator string(NetworkDomainPermission permission) => permission.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
