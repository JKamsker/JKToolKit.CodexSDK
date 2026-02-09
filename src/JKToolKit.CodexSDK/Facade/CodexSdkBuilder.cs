using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Facade;

/// <summary>
/// Builder for creating a <see cref="CodexSdk"/> without using dependency injection.
/// </summary>
/// <remarks>
/// This builder wires together the existing clients/factories with minimal ceremony and
/// supports configuring a single Codex executable path globally, with per-mode overrides.
/// </remarks>
public sealed class CodexSdkBuilder
{
    private readonly CodexClientOptions _execOptions = new();
    private readonly CodexAppServerClientOptions _appServerOptions = new();
    private readonly CodexMcpServerClientOptions _mcpServerOptions = new();

    /// <summary>
    /// Gets or sets the Codex CLI executable path to apply across all modes unless a mode-specific override is set.
    /// </summary>
    public string? CodexExecutablePath { get; set; }

    /// <summary>
    /// Gets or sets the Codex home directory to apply across all modes unless a mode-specific override is set.
    /// </summary>
    public string? CodexHomeDirectory { get; set; }

    /// <summary>
    /// Gets the logger factory to use across all modes, if configured.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; private set; }

    /// <summary>
    /// Sets the logger factory to use across all modes.
    /// </summary>
    public CodexSdkBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        LoggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Applies configuration to the Exec mode client options.
    /// </summary>
    public CodexSdkBuilder ConfigureExec(Action<CodexClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_execOptions);
        return this;
    }

    /// <summary>
    /// Applies configuration to the AppServer mode client options.
    /// </summary>
    public CodexSdkBuilder ConfigureAppServer(Action<CodexAppServerClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_appServerOptions);
        return this;
    }

    /// <summary>
    /// Applies configuration to the McpServer mode client options.
    /// </summary>
    public CodexSdkBuilder ConfigureMcpServer(Action<CodexMcpServerClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_mcpServerOptions);
        return this;
    }

    /// <summary>
    /// Builds a <see cref="CodexSdk"/> with the current configuration.
    /// </summary>
    public CodexSdk Build()
    {
        var loggerFactory = LoggerFactory ?? NullLoggerFactory.Instance;
        var (execOptions, appServerOptions, mcpServerOptions) = CreateEffectiveOptionsSnapshot();

        var exec = new CodexClient(execOptions, loggerFactory: loggerFactory);

        var stdioFactory = CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory);
        var appServerFactory = new CodexAppServerClientFactory(Options.Create(appServerOptions), stdioFactory, loggerFactory);
        var mcpServerFactory = new CodexMcpServerClientFactory(Options.Create(mcpServerOptions), stdioFactory, loggerFactory);

        return CodexSdk.CreateOwned(exec, appServerFactory, mcpServerFactory);
    }

    internal (CodexClientOptions Exec, CodexAppServerClientOptions AppServer, CodexMcpServerClientOptions McpServer)
        CreateEffectiveOptionsSnapshot()
    {
        var exec = _execOptions.Clone();
        var app = Clone(_appServerOptions);
        var mcp = Clone(_mcpServerOptions);

        var globalPath = CodexExecutablePath;
        if (globalPath is not null)
        {
            if (exec.CodexExecutablePath is null)
                exec.CodexExecutablePath = globalPath;

            if (app.CodexExecutablePath is null)
                app.CodexExecutablePath = globalPath;

            if (mcp.CodexExecutablePath is null)
                mcp.CodexExecutablePath = globalPath;
        }

        var globalHome = CodexHomeDirectory;
        if (!string.IsNullOrWhiteSpace(globalHome))
        {
            if (exec.CodexHomeDirectory is null)
                exec.CodexHomeDirectory = globalHome;

            if (app.CodexHomeDirectory is null)
                app.CodexHomeDirectory = globalHome;

            if (mcp.CodexHomeDirectory is null)
                mcp.CodexHomeDirectory = globalHome;
        }

        return (exec, app, mcp);
    }

    private static CodexAppServerClientOptions Clone(CodexAppServerClientOptions options) => new()
    {
        Launch = options.Launch,
        CodexExecutablePath = options.CodexExecutablePath,
        CodexHomeDirectory = options.CodexHomeDirectory,
        StartupTimeout = options.StartupTimeout,
        ShutdownTimeout = options.ShutdownTimeout,
        DefaultClientInfo = options.DefaultClientInfo,
        SerializerOptionsOverride = options.SerializerOptionsOverride,
        NotificationBufferCapacity = options.NotificationBufferCapacity,
        ApprovalHandler = options.ApprovalHandler
    };

    private static CodexMcpServerClientOptions Clone(CodexMcpServerClientOptions options) => new()
    {
        Launch = options.Launch,
        CodexExecutablePath = options.CodexExecutablePath,
        CodexHomeDirectory = options.CodexHomeDirectory,
        StartupTimeout = options.StartupTimeout,
        ShutdownTimeout = options.ShutdownTimeout,
        SerializerOptionsOverride = options.SerializerOptionsOverride,
        NotificationBufferCapacity = options.NotificationBufferCapacity,
        ClientInfo = options.ClientInfo,
        ElicitationHandler = options.ElicitationHandler
    };
}

