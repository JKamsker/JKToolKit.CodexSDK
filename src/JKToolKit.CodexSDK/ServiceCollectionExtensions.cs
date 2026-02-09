using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JKToolKit.CodexSDK;

/// <summary>
/// Service registration helpers for Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Codex SDK facade (<see cref="CodexSdk"/>) and its underlying mode registrations.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="exec">Optional configuration for <see cref="CodexClientOptions"/>.</param>
    /// <param name="appServer">Optional configuration for <see cref="CodexAppServerClientOptions"/>.</param>
    /// <param name="mcpServer">Optional configuration for <see cref="CodexMcpServerClientOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCodexSdk(
        this IServiceCollection services,
        Action<CodexClientOptions>? exec = null,
        Action<CodexAppServerClientOptions>? appServer = null,
        Action<CodexMcpServerClientOptions>? mcpServer = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCodexClient(exec);
        services.AddCodexAppServerClient(appServer);
        services.AddCodexMcpServerClient(mcpServer);

        services.TryAddSingleton(sp =>
            new CodexSdk(
                sp.GetRequiredService<ICodexClient>(),
                sp.GetRequiredService<ICodexAppServerClientFactory>(),
                sp.GetRequiredService<ICodexMcpServerClientFactory>()));

        return services;
    }
}

