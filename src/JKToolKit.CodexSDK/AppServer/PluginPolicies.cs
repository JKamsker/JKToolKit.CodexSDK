namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the plugin auth policy wire value reported by plugin APIs.
/// </summary>
public readonly record struct PluginAuthPolicy
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginAuthPolicy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin auth policy cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>ON_INSTALL</c> auth policy.
    /// </summary>
    public static PluginAuthPolicy OnInstall => new("ON_INSTALL");

    /// <summary>
    /// Gets the <c>ON_USE</c> auth policy.
    /// </summary>
    public static PluginAuthPolicy OnUse => new("ON_USE");

    /// <summary>
    /// Parses a plugin auth policy from a wire value.
    /// </summary>
    public static PluginAuthPolicy Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin auth policy from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginAuthPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = default;
            return false;
        }

        policy = new PluginAuthPolicy(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginAuthPolicy"/>.
    /// </summary>
    public static implicit operator PluginAuthPolicy(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginAuthPolicy"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginAuthPolicy policy) => policy.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents the plugin install policy wire value reported by plugin APIs.
/// </summary>
public readonly record struct PluginInstallPolicy
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginInstallPolicy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin install policy cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>NOT_AVAILABLE</c> install policy.
    /// </summary>
    public static PluginInstallPolicy NotAvailable => new("NOT_AVAILABLE");

    /// <summary>
    /// Gets the <c>AVAILABLE</c> install policy.
    /// </summary>
    public static PluginInstallPolicy Available => new("AVAILABLE");

    /// <summary>
    /// Gets the <c>INSTALLED_BY_DEFAULT</c> install policy.
    /// </summary>
    public static PluginInstallPolicy InstalledByDefault => new("INSTALLED_BY_DEFAULT");

    /// <summary>
    /// Parses a plugin install policy from a wire value.
    /// </summary>
    public static PluginInstallPolicy Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin install policy from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginInstallPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            policy = default;
            return false;
        }

        policy = new PluginInstallPolicy(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginInstallPolicy"/>.
    /// </summary>
    public static implicit operator PluginInstallPolicy(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginInstallPolicy"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginInstallPolicy policy) => policy.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents the plugin source type wire value reported by plugin APIs.
/// </summary>
public readonly record struct PluginSourceType
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginSourceType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin source type cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>local</c> source type.
    /// </summary>
    public static PluginSourceType Local => new("local");

    /// <summary>
    /// Parses a plugin source type from a wire value.
    /// </summary>
    public static PluginSourceType Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin source type from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginSourceType sourceType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            sourceType = default;
            return false;
        }

        sourceType = new PluginSourceType(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginSourceType"/>.
    /// </summary>
    public static implicit operator PluginSourceType(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginSourceType"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginSourceType sourceType) => sourceType.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
