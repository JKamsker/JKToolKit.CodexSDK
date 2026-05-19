namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a plugin marketplace kind accepted by <c>plugin/list</c>.
/// </summary>
public readonly record struct PluginListMarketplaceKind
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private PluginListMarketplaceKind(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plugin list marketplace kind cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the local marketplace kind.
    /// </summary>
    public static PluginListMarketplaceKind Local => new("local");

    /// <summary>
    /// Gets the workspace-directory marketplace kind.
    /// </summary>
    public static PluginListMarketplaceKind WorkspaceDirectory => new("workspace-directory");

    /// <summary>
    /// Gets the shared-with-me marketplace kind.
    /// </summary>
    public static PluginListMarketplaceKind SharedWithMe => new("shared-with-me");

    /// <summary>
    /// Parses a plugin marketplace kind from a wire value.
    /// </summary>
    public static PluginListMarketplaceKind Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a plugin marketplace kind from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out PluginListMarketplaceKind kind)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            kind = default;
            return false;
        }

        kind = new PluginListMarketplaceKind(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="PluginListMarketplaceKind"/>.
    /// </summary>
    public static implicit operator PluginListMarketplaceKind(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="PluginListMarketplaceKind"/> to its wire value.
    /// </summary>
    public static implicit operator string(PluginListMarketplaceKind kind) => kind.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
