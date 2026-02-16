using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Helper for building app-server per-request config overrides (the <c>params.config</c> "dotted key" bag).
/// </summary>
public sealed class CodexConfigOverridesBuilder
{
    private readonly Dictionary<string, object?> _overrides = new(StringComparer.Ordinal);

    /// <summary>
    /// Sets or removes a dotted-path config override.
    /// </summary>
    /// <remarks>
    /// Keys are dotted config paths (for example <c>mcp_servers.mytool.command</c>).
    /// When <paramref name="value"/> is <see langword="null"/>, the key is removed.
    /// </remarks>
    public CodexConfigOverridesBuilder Set(string dottedPath, object? value)
    {
        if (string.IsNullOrWhiteSpace(dottedPath))
        {
            throw new ArgumentException("Dotted path cannot be empty or whitespace.", nameof(dottedPath));
        }

        if (value is null)
        {
            _overrides.Remove(dottedPath);
        }
        else
        {
            _overrides[dottedPath] = value;
        }

        return this;
    }

    /// <summary>
    /// Adds or updates an MCP stdio server entry under <c>mcp_servers</c>.
    /// </summary>
    public CodexConfigOverridesBuilder SetMcpServerStdio(
        string name,
        string command,
        IEnumerable<string>? args = null,
        IDictionary<string, string>? env = null,
        IEnumerable<string>? envVars = null,
        string? cwd = null,
        bool? enabled = null,
        bool? required = null,
        long? startupTimeoutSeconds = null,
        long? toolTimeoutSeconds = null,
        IEnumerable<string>? enabledTools = null,
        IEnumerable<string>? disabledTools = null,
        IEnumerable<string>? scopes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Server name cannot be empty or whitespace.", nameof(name));
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command cannot be empty or whitespace.", nameof(command));

        var prefix = $"mcp_servers.{name}";

        Set($"{prefix}.command", command);
        Set($"{prefix}.args", args?.ToArray());
        Set($"{prefix}.env", env is null ? null : new Dictionary<string, string>(env, StringComparer.Ordinal));
        Set($"{prefix}.env_vars", envVars?.ToArray());
        Set($"{prefix}.cwd", cwd);
        Set($"{prefix}.enabled", enabled);
        Set($"{prefix}.required", required);
        Set($"{prefix}.startup_timeout_sec", startupTimeoutSeconds);
        Set($"{prefix}.tool_timeout_sec", toolTimeoutSeconds);
        Set($"{prefix}.enabled_tools", enabledTools?.ToArray());
        Set($"{prefix}.disabled_tools", disabledTools?.ToArray());
        Set($"{prefix}.scopes", scopes?.ToArray());

        return this;
    }

    /// <summary>
    /// Adds or updates an MCP streamable HTTP server entry under <c>mcp_servers</c>.
    /// </summary>
    public CodexConfigOverridesBuilder SetMcpServerStreamableHttp(
        string name,
        string url,
        string? bearerTokenEnvVar = null,
        IDictionary<string, string>? httpHeaders = null,
        IDictionary<string, string>? envHttpHeaders = null,
        bool? enabled = null,
        bool? required = null,
        long? startupTimeoutSeconds = null,
        long? toolTimeoutSeconds = null,
        IEnumerable<string>? enabledTools = null,
        IEnumerable<string>? disabledTools = null,
        IEnumerable<string>? scopes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Server name cannot be empty or whitespace.", nameof(name));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty or whitespace.", nameof(url));

        var prefix = $"mcp_servers.{name}";

        Set($"{prefix}.url", url);
        Set($"{prefix}.bearer_token_env_var", bearerTokenEnvVar);
        Set($"{prefix}.http_headers", httpHeaders is null ? null : new Dictionary<string, string>(httpHeaders, StringComparer.Ordinal));
        Set($"{prefix}.env_http_headers", envHttpHeaders is null ? null : new Dictionary<string, string>(envHttpHeaders, StringComparer.Ordinal));
        Set($"{prefix}.enabled", enabled);
        Set($"{prefix}.required", required);
        Set($"{prefix}.startup_timeout_sec", startupTimeoutSeconds);
        Set($"{prefix}.tool_timeout_sec", toolTimeoutSeconds);
        Set($"{prefix}.enabled_tools", enabledTools?.ToArray());
        Set($"{prefix}.disabled_tools", disabledTools?.ToArray());
        Set($"{prefix}.scopes", scopes?.ToArray());

        return this;
    }

    /// <summary>
    /// Builds the JSON object to assign to <c>ThreadStartOptions.Config</c> / <c>ThreadResumeOptions.Config</c>.
    /// </summary>
    public JsonElement Build() => JsonSerializer.SerializeToElement(_overrides, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>
    /// Builds the JSON override object, or <see langword="null"/> if no overrides were set.
    /// </summary>
    public JsonElement? BuildOrNull() => _overrides.Count == 0 ? null : Build();
}

