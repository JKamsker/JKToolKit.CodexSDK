namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a plugin share principal type.
/// </summary>
public readonly record struct PluginSharePrincipalType
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginSharePrincipalType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin share principal type cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the user principal type.
    /// </summary>
    public static PluginSharePrincipalType User => new("user");

    /// <summary>
    /// Gets the group principal type.
    /// </summary>
    public static PluginSharePrincipalType Group => new("group");

    /// <summary>
    /// Gets the workspace principal type.
    /// </summary>
    public static PluginSharePrincipalType Workspace => new("workspace");

    /// <summary>
    /// Parses a plugin share principal type from a wire value.
    /// </summary>
    public static PluginSharePrincipalType Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin share principal type from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginSharePrincipalType type)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            type = default;
            return false;
        }

        type = new PluginSharePrincipalType(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginSharePrincipalType"/>.
    /// </summary>
    public static implicit operator PluginSharePrincipalType(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginSharePrincipalType"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginSharePrincipalType type) => type.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents a role assigned to a plugin share target.
/// </summary>
public readonly record struct PluginShareTargetRole
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginShareTargetRole(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin share target role cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the reader role.
    /// </summary>
    public static PluginShareTargetRole Reader => new("reader");

    /// <summary>
    /// Gets the editor role.
    /// </summary>
    public static PluginShareTargetRole Editor => new("editor");

    /// <summary>
    /// Parses a plugin share target role from a wire value.
    /// </summary>
    public static PluginShareTargetRole Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin share target role from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginShareTargetRole role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = default;
            return false;
        }

        role = new PluginShareTargetRole(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginShareTargetRole"/>.
    /// </summary>
    public static implicit operator PluginShareTargetRole(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginShareTargetRole"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginShareTargetRole role) => role.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents a role returned for a plugin share principal.
/// </summary>
public readonly record struct PluginSharePrincipalRole
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginSharePrincipalRole(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin share principal role cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the reader role.
    /// </summary>
    public static PluginSharePrincipalRole Reader => new("reader");

    /// <summary>
    /// Gets the editor role.
    /// </summary>
    public static PluginSharePrincipalRole Editor => new("editor");

    /// <summary>
    /// Gets the owner role.
    /// </summary>
    public static PluginSharePrincipalRole Owner => new("owner");

    /// <summary>
    /// Parses a plugin share principal role from a wire value.
    /// </summary>
    public static PluginSharePrincipalRole Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin share principal role from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginSharePrincipalRole role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = default;
            return false;
        }

        role = new PluginSharePrincipalRole(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginSharePrincipalRole"/>.
    /// </summary>
    public static implicit operator PluginSharePrincipalRole(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginSharePrincipalRole"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginSharePrincipalRole role) => role.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
