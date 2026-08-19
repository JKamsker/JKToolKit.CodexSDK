namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for starting an OAuth login flow for a configured MCP server.
/// </summary>
public sealed class McpServerOauthLoginOptions
{
    /// <summary>
    /// Gets or sets the configured MCP server name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the thread id that the OAuth flow belongs to, when scoped to a thread.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Gets or sets a per-login OAuth client registration strategy. Omit to let upstream auto-discover.
    /// </summary>
    public McpServerOauthClientRegistration? ClientRegistration { get; set; }

    /// <summary>
    /// Gets or sets optional OAuth scopes to request (overrides server defaults).
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; set; }

    /// <summary>
    /// Gets or sets an optional timeout in seconds for the login flow.
    /// </summary>
    public long? TimeoutSeconds { get; set; }
}

/// <summary>
/// OAuth client registration strategy for <c>mcpServer/oauth/login</c>.
/// </summary>
public readonly record struct McpServerOauthClientRegistration
{
    private readonly string? _value;

    /// <summary>
    /// Gets the underlying wire value, or an empty string for an uninitialized value.
    /// </summary>
    public string Value => _value ?? string.Empty;

    private McpServerOauthClientRegistration(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("MCP OAuth client registration cannot be empty or whitespace.", nameof(value));

        _value = value;
    }

    /// <summary>
    /// Lets upstream choose the registration strategy automatically.
    /// </summary>
    public static McpServerOauthClientRegistration Auto => new("auto");

    /// <summary>
    /// Uses CIMD registration for this OAuth login.
    /// </summary>
    public static McpServerOauthClientRegistration Cimd => new("cimd");

    /// <summary>
    /// Uses dynamic client registration for this OAuth login.
    /// </summary>
    public static McpServerOauthClientRegistration Dcr => new("dcr");

    /// <summary>
    /// Parses a registration strategy from a wire value.
    /// </summary>
    public static McpServerOauthClientRegistration Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a registration strategy from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out McpServerOauthClientRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            registration = default;
            return false;
        }

        registration = new McpServerOauthClientRegistration(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="McpServerOauthClientRegistration"/>.
    /// </summary>
    public static implicit operator McpServerOauthClientRegistration(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="McpServerOauthClientRegistration"/> to its wire value.
    /// </summary>
    public static implicit operator string(McpServerOauthClientRegistration registration) => registration.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
