using JKToolKit.CodexSDK.Facade;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Dependency injection helpers for Codex-backed Agent Framework agents.
/// </summary>
public static class CodexAgentServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="CodexAgentClient"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configureSdk">
    /// Optional Codex SDK builder configuration used when no <see cref="CodexSdk"/> is resolved from DI.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCodexAgentClient(
        this IServiceCollection services,
        Action<CodexSdkBuilder>? configureSdk = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp => CreateClient(sp, configureSdk));
        return services;
    }

    /// <summary>
    /// Registers a singleton Codex-backed <see cref="AIAgent"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configureAgent">Configures the Codex-backed agent options.</param>
    /// <param name="configureSdk">
    /// Optional Codex SDK builder configuration used when no <see cref="CodexSdk"/> is resolved from DI.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCodexAIAgent(
        this IServiceCollection services,
        Action<CodexAIAgentOptions> configureAgent,
        Action<CodexSdkBuilder>? configureSdk = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureAgent);

        return services.AddCodexAIAgent(
            _ => CreateOptions(configureAgent),
            configureSdk);
    }

    /// <summary>
    /// Registers a singleton Codex-backed <see cref="AIAgent"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="optionsFactory">Creates the Codex-backed agent options from DI.</param>
    /// <param name="configureSdk">
    /// Optional Codex SDK builder configuration used when no <see cref="CodexSdk"/> is resolved from DI.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCodexAIAgent(
        this IServiceCollection services,
        Func<IServiceProvider, CodexAIAgentOptions> optionsFactory,
        Action<CodexSdkBuilder>? configureSdk = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.AddSingleton<AIAgent>(sp => CreateAgent(sp, optionsFactory(sp), configureSdk));
        return services;
    }

    /// <summary>
    /// Registers a keyed singleton Codex-backed <see cref="AIAgent"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="serviceKey">The keyed service identifier.</param>
    /// <param name="configureAgent">Configures the Codex-backed agent options.</param>
    /// <param name="configureSdk">
    /// Optional Codex SDK builder configuration used when no <see cref="CodexSdk"/> is resolved from DI.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKeyedCodexAIAgent(
        this IServiceCollection services,
        object? serviceKey,
        Action<CodexAIAgentOptions> configureAgent,
        Action<CodexSdkBuilder>? configureSdk = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureAgent);

        return services.AddKeyedCodexAIAgent(
            serviceKey,
            (_, _) => CreateOptions(configureAgent),
            configureSdk);
    }

    /// <summary>
    /// Registers a keyed singleton Codex-backed <see cref="AIAgent"/>.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="serviceKey">The keyed service identifier.</param>
    /// <param name="optionsFactory">Creates the Codex-backed agent options from DI and the service key.</param>
    /// <param name="configureSdk">
    /// Optional Codex SDK builder configuration used when no <see cref="CodexSdk"/> is resolved from DI.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKeyedCodexAIAgent(
        this IServiceCollection services,
        object? serviceKey,
        Func<IServiceProvider, object?, CodexAIAgentOptions> optionsFactory,
        Action<CodexSdkBuilder>? configureSdk = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        services.AddKeyedSingleton<AIAgent>(
            serviceKey,
            (sp, key) => CreateAgent(sp, optionsFactory(sp, key), configureSdk));
        return services;
    }

    private static CodexAgentClient CreateClient(
        IServiceProvider services,
        Action<CodexSdkBuilder>? configureSdk)
    {
        if (configureSdk is not null)
        {
            return new CodexAgentClient(configureSdk);
        }

        return services.GetService<CodexSdk>() is { } sdk
            ? new CodexAgentClient(sdk)
            : new CodexAgentClient();
    }

    private static AIAgent CreateAgent(
        IServiceProvider services,
        CodexAIAgentOptions options,
        Action<CodexSdkBuilder>? configureSdk)
    {
        ArgumentNullException.ThrowIfNull(options);
        return CreateClient(services, configureSdk).AsAIAgent(options);
    }

    private static CodexAIAgentOptions CreateOptions(Action<CodexAIAgentOptions> configureAgent)
    {
        var options = new CodexAIAgentOptions();
        configureAgent(options);
        return options;
    }
}
