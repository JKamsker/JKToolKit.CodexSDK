using System.Text.Json;
using System.IO;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Reads the effective merged configuration (with optional layer details).
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>config/read</c>. If <see cref="ConfigReadOptions.Cwd"/> is set,
    /// Codex resolves project config layers as seen from that directory.
    /// </remarks>
    public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct = default) =>
        _configClient.ReadConfigAsync(options, ct);

    /// <summary>
    /// Detects external agent configuration that can be migrated into Codex.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>externalAgentConfig/detect</c>.
    /// </remarks>
    public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct = default) =>
        _configClient.DetectExternalAgentConfigAsync(options, ct);

    /// <summary>
    /// Imports external agent configuration into Codex.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>externalAgentConfig/import</c>.
    /// </remarks>
    public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct = default) =>
        _configClient.ImportExternalAgentConfigAsync(migrationItems, ct);

    /// <summary>
    /// Reads account information.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/read</c>.
    /// </remarks>
    public Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct = default) =>
        _configClient.ReadAccountAsync(options, ct);

    /// <summary>
    /// Reads account information with default options.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/read</c>.
    /// </remarks>
    public Task<AccountReadResult> ReadAccountAsync(CancellationToken ct = default) =>
        _configClient.ReadAccountAsync(new AccountReadOptions(), ct);

    /// <summary>
    /// Reads current account rate limits.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/rateLimits/read</c>.
    /// </remarks>
    public Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct = default) =>
        _configClient.ReadAccountRateLimitsAsync(ct);

    /// <summary>
    /// Starts the Windows sandbox setup flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>windowsSandbox/setupStart</c>.
    /// Known modes include <c>elevated</c> and <c>unelevated</c>.
    /// </remarks>
    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct = default) =>
        _configClient.StartWindowsSandboxSetupAsync(options, ct);

    /// <summary>
    /// Starts the Windows sandbox setup flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>windowsSandbox/setupStart</c>.
    /// </remarks>
    public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd = null, CancellationToken ct = default) =>
        _configClient.StartWindowsSandboxSetupAsync(
            new WindowsSandboxSetupStartOptions(mode)
            {
                Cwd = cwd
            },
            ct);

    /// <summary>
    /// Starts the Windows sandbox setup flow.
    /// </summary>
    /// <remarks>
    /// This string-based overload is kept for compatibility. Prefer
    /// <see cref="StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions,System.Threading.CancellationToken)"/>
    /// or <see cref="StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode,string?,System.Threading.CancellationToken)"/>.
    /// </remarks>
    public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct = default) =>
        StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode.Parse(mode), cwd: null, ct);

    /// <summary>
    /// Reloads MCP server configuration from disk and queues a refresh for loaded threads.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>config/mcpServer/reload</c>.
    /// Refresh is applied on each thread's next active turn.
    /// </remarks>
    public Task ReloadMcpServersAsync(CancellationToken ct = default) =>
        _mcpClient.ReloadMcpServersAsync(ct);

    /// <summary>
    /// Lists MCP servers with their tools/resources and auth status.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServerStatus/list</c>.
    /// </remarks>
    public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct = default) =>
        _mcpClient.ListMcpServerStatusAsync(options, ct);

    /// <summary>
    /// Starts an OAuth login flow for a configured MCP server.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>mcpServer/oauth/login</c>.
    /// The server later emits <c>mcpServer/oauthLogin/completed</c>.
    /// </remarks>
    public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct = default) =>
        _mcpClient.StartMcpServerOauthLoginAsync(options, ct);

    /// <summary>
    /// Starts an account login flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/login/start</c>.
    /// Supported flows include API-key login, browser ChatGPT login, device-code ChatGPT login, and externally managed
    /// ChatGPT auth-token submission.
    /// </remarks>
    public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct = default) =>
        _configClient.StartAccountLoginAsync(options, ct);

    /// <summary>
    /// Cancels a pending account login flow.
    /// </summary>
    /// <remarks>
    /// This calls the app-server method <c>account/login/cancel</c>.
    /// </remarks>
    public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct = default) =>
        _configClient.CancelAccountLoginAsync(loginId, ct);
}

/// <summary>
/// Options for <c>account/read</c>.
/// </summary>
public sealed class AccountReadOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the app-server should proactively refresh managed auth tokens.
    /// </summary>
    /// <remarks>
    /// In external auth-token mode, this flag is ignored by upstream and callers are expected to refresh externally.
    /// </remarks>
    public bool RefreshToken { get; set; }
}

/// <summary>
/// Represents the result returned by <c>account/read</c>.
/// </summary>
public sealed record class AccountReadResult
{
    /// <summary>
    /// Gets the raw account object when present.
    /// </summary>
    public JsonElement? Account { get; init; }

    /// <summary>
    /// Gets a value indicating whether OpenAI auth is currently required.
    /// </summary>
    public bool? RequiresOpenaiAuth { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents the result returned by <c>account/rateLimits/read</c>.
/// </summary>
public sealed record class AccountRateLimitsReadResult
{
    /// <summary>
    /// Gets the aggregate/default rate-limit snapshot.
    /// </summary>
    public required JsonElement RateLimits { get; init; }

    /// <summary>
    /// Gets optional per-limit snapshots keyed by metered limit identifier.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? RateLimitsByLimitId { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a Windows sandbox setup mode wire value.
/// </summary>
/// <remarks>
/// Known values include <c>elevated</c> and <c>unelevated</c>.
/// </remarks>
public readonly record struct WindowsSandboxSetupMode
{
    /// <summary>
    /// Gets the underlying wire value.
    /// </summary>
    public string Value { get; }

    private WindowsSandboxSetupMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Mode cannot be empty or whitespace.", nameof(value));

        Value = value;
    }

    /// <summary>
    /// Gets the <c>elevated</c> setup mode.
    /// </summary>
    public static WindowsSandboxSetupMode Elevated => new("elevated");

    /// <summary>
    /// Gets the <c>unelevated</c> setup mode.
    /// </summary>
    public static WindowsSandboxSetupMode Unelevated => new("unelevated");

    /// <summary>
    /// Parses a setup mode from a wire value.
    /// </summary>
    public static WindowsSandboxSetupMode Parse(string value) => new(value);

    /// <summary>
    /// Tries to parse a setup mode from a wire value.
    /// </summary>
    public static bool TryParse(string? value, out WindowsSandboxSetupMode mode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            mode = default;
            return false;
        }

        mode = new WindowsSandboxSetupMode(value);
        return true;
    }

    /// <summary>
    /// Converts a string to a <see cref="WindowsSandboxSetupMode"/>.
    /// </summary>
    public static implicit operator WindowsSandboxSetupMode(string value) => Parse(value);

    /// <summary>
    /// Converts a <see cref="WindowsSandboxSetupMode"/> to its wire value.
    /// </summary>
    public static implicit operator string(WindowsSandboxSetupMode mode) => mode.Value;

    /// <summary>
    /// Returns the underlying wire value.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Options for <c>windowsSandbox/setupStart</c>.
/// </summary>
public sealed class WindowsSandboxSetupStartOptions
{
    private string? _cwd;

    /// <summary>
    /// Initializes a new instance of <see cref="WindowsSandboxSetupStartOptions"/>.
    /// </summary>
    public WindowsSandboxSetupStartOptions(WindowsSandboxSetupMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    /// Gets or sets the setup mode.
    /// </summary>
    public WindowsSandboxSetupMode Mode { get; set; }

    /// <summary>
    /// Gets or sets an optional working directory for setup.
    /// </summary>
    public string? Cwd
    {
        get => _cwd;
        set
        {
            if (value is not null)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Cwd cannot be empty or whitespace when provided.", nameof(value));

                if (!Path.IsPathFullyQualified(value))
                    throw new ArgumentException("Cwd must be an absolute path when provided.", nameof(value));
            }

            _cwd = value;
        }
    }
}

