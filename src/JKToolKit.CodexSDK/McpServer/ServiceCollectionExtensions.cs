using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JKToolKit.CodexSDK.Infrastructure;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Dependency injection extensions for registering Codex MCP server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Codex MCP server client and related infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCodexMcpServerClient(
        this IServiceCollection services,
        Action<CodexMcpServerClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddCodexStdioInfrastructure();

        services.TryAddSingleton<ICodexMcpServerClientFactory, CodexMcpServerClientFactory>();

        return services;
    }
}
