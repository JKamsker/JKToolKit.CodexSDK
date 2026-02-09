using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using JKToolKit.CodexSDK.AppServer;

namespace JKToolKit.CodexSDK.AppServer.Resiliency;

/// <summary>
/// Dependency injection extensions for registering resilient Codex app-server services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a resilient app-server client factory that can auto-restart the <c>codex app-server</c> subprocess.
    /// </summary>
    public static IServiceCollection AddCodexResilientAppServerClient(
        this IServiceCollection services,
        Action<CodexAppServerResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCodexAppServerClient();
        services.AddOptions();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<ICodexResilientAppServerClientFactory, CodexResilientAppServerClientFactory>();

        return services;
    }

    private sealed class CodexResilientAppServerClientFactory : ICodexResilientAppServerClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICodexAppServerClientFactory _innerFactory;
        private readonly IOptions<CodexAppServerResilienceOptions> _options;

        public CodexResilientAppServerClientFactory(
            IServiceProvider serviceProvider,
            ICodexAppServerClientFactory innerFactory,
            IOptions<CodexAppServerResilienceOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public Task<ResilientCodexAppServerClient> StartAsync(CancellationToken ct = default)
        {
            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;

            return ResilientCodexAppServerClient.StartAsync(
                _innerFactory,
                options: _options.Value,
                loggerFactory: loggerFactory,
                ct: ct);
        }
    }
}
