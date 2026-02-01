using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using JKToolKit.CodexSDK.Infrastructure;

namespace JKToolKit.CodexSDK.AppServer;

public static class ServiceCollectionExtensions
{
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
