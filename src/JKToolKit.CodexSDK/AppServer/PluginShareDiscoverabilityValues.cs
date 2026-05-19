namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents plugin share discoverability.
/// </summary>
public readonly record struct PluginShareDiscoverability
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginShareDiscoverability(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin share discoverability cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the listed discoverability value.
    /// </summary>
    public static PluginShareDiscoverability Listed => new("LISTED");

    /// <summary>
    /// Gets the unlisted discoverability value.
    /// </summary>
    public static PluginShareDiscoverability Unlisted => new("UNLISTED");

    /// <summary>
    /// Gets the private discoverability value.
    /// </summary>
    public static PluginShareDiscoverability Private => new("PRIVATE");

    /// <summary>
    /// Parses a plugin share discoverability from a wire value.
    /// </summary>
    public static PluginShareDiscoverability Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin share discoverability from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginShareDiscoverability discoverability)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            discoverability = default;
            return false;
        }

        discoverability = new PluginShareDiscoverability(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginShareDiscoverability"/>.
    /// </summary>
    public static implicit operator PluginShareDiscoverability(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginShareDiscoverability"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginShareDiscoverability discoverability) => discoverability.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Represents plugin share discoverability values accepted by <c>plugin/share/updateTargets</c>.
/// </summary>
public readonly record struct PluginShareUpdateDiscoverability
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginShareUpdateDiscoverability(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin share update discoverability cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the unlisted discoverability value.
    /// </summary>
    public static PluginShareUpdateDiscoverability Unlisted => new("UNLISTED");

    /// <summary>
    /// Gets the private discoverability value.
    /// </summary>
    public static PluginShareUpdateDiscoverability Private => new("PRIVATE");

    /// <summary>
    /// Parses a plugin share update discoverability from a wire value.
    /// </summary>
    public static PluginShareUpdateDiscoverability Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin share update discoverability from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginShareUpdateDiscoverability discoverability)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            discoverability = default;
            return false;
        }

        discoverability = new PluginShareUpdateDiscoverability(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginShareUpdateDiscoverability"/>.
    /// </summary>
    public static implicit operator PluginShareUpdateDiscoverability(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginShareUpdateDiscoverability"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginShareUpdateDiscoverability discoverability) => discoverability.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
