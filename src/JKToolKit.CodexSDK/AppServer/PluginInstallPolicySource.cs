namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the source of a plugin install policy when upstream reports one.
/// </summary>
/// <remarks>
/// Known values are exposed as static properties. Unknown non-empty wire values are preserved for forward compatibility.
/// </remarks>
public readonly record struct PluginInstallPolicySource
{
    private readonly string? _value;

    /// <summary>
    /// Gets the underlying wire value, or an empty string for an uninitialized value.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private PluginInstallPolicySource(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin install policy source cannot be empty or whitespace.", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Gets the <c>WORKSPACE_SETTING</c> policy source.
    /// </summary>
    public static PluginInstallPolicySource WorkspaceSetting => new("WORKSPACE_SETTING");

    /// <summary>
    /// Gets the <c>IMPLICIT_CANONICAL_APP</c> policy source.
    /// </summary>
    public static PluginInstallPolicySource ImplicitCanonicalApp => new("IMPLICIT_CANONICAL_APP");

    /// <summary>
    /// Parses a plugin install policy source from a known or future wire value.
    /// </summary>
    public static PluginInstallPolicySource Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin install policy source from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginInstallPolicySource source)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            source = default;
            return false;
        }

        source = new PluginInstallPolicySource(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginInstallPolicySource"/>.
    /// </summary>
    public static implicit operator PluginInstallPolicySource(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginInstallPolicySource"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginInstallPolicySource source) => source.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
