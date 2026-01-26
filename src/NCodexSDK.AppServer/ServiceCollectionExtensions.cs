using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCodexSDK.Abstractions;
using NCodexSDK.Infrastructure;
using NCodexSDK.Infrastructure.Stdio;

namespace NCodexSDK.AppServer;

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

        services.TryAddSingleton<IFileSystem, RealFileSystem>();
        services.TryAddSingleton<ICodexPathProvider, DefaultCodexPathProvider>();
        services.TryAddSingleton<StdioProcessFactory>();

        services.TryAddSingleton<ICodexAppServerClientFactory, CodexAppServerClientFactory>();

        return services;
    }
}

