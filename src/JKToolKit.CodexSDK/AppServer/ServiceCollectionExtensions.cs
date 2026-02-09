using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JKToolKit.CodexSDK.Infrastructure;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Dependency injection extensions for registering Codex app-server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Codex app-server client and related infrastructure.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddCodexAppServerClient(
        this IServiceCollection services,
        Action<CodexAppServerClientOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddCodexStdioInfrastructure();

        services.TryAddSingleton<ICodexAppServerClientFactory, CodexAppServerClientFactory>();

        return services;
    }
}
