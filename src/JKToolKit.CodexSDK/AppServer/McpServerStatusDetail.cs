namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Controls how much MCP inventory data the app-server should include in
/// <c>mcpServerStatus/list</c> responses.
/// </summary>
public readonly record struct McpServerStatusDetail
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private McpServerStatusDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Detail cannot be empty or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Requests full MCP server inventory, including tools, resources, and templates.
    /// </summary>
    public static McpServerStatusDetail Full => new("full");

    /// <summary>
    /// Requests only tool and auth data, omitting resource inventory when supported upstream.
    /// </summary>
    public static McpServerStatusDetail ToolsAndAuthOnly => new("toolsAndAuthOnly");

    /// <summary>
    /// Parses a detail value from the wire representation.
    /// </summary>
    public static McpServerStatusDetail Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a detail value from the wire representation.
    /// </summary>
    public static bool TryParse(string? value, out McpServerStatusDetail detail)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            detail = default;
            return false;
        }

        detail = new McpServerStatusDetail(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a detail value.
    /// </summary>
    public static implicit operator McpServerStatusDetail(string value) => Parse(value);

    /// <summary>
    /// Converts a detail value to its wire representation.
    /// </summary>
    public static implicit operator string(McpServerStatusDetail detail) => detail.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}
