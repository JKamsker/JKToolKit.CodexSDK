using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Extension methods for attaching Codex-specific settings to Agent Framework run options.
/// </summary>
public static class CodexAgentRunOptionsExtensions
{
    internal const string ConfigurationKey = "JKToolKit.CodexSDK.AgentFramework.CodexAgentRunConfiguration";

    /// <summary>
    /// Attaches Codex-specific settings to any Agent Framework run options instance.
    /// </summary>
    public static T WithCodex<T>(this T options, CodexAgentRunConfiguration configuration)
        where T : AgentRunOptions
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
        options.AdditionalProperties[ConfigurationKey] = configuration;
        return options;
    }

    /// <summary>
    /// Creates and attaches Codex-specific settings to any Agent Framework run options instance.
    /// </summary>
    public static T ConfigureCodex<T>(this T options, Action<CodexAgentRunConfiguration> configure)
        where T : AgentRunOptions
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var configuration = new CodexAgentRunConfiguration();
        configure(configuration);
        return options.WithCodex(configuration);
    }

    /// <summary>
    /// Gets Codex-specific settings attached to the run options, if present.
    /// </summary>
    public static CodexAgentRunConfiguration? GetCodexConfiguration(this AgentRunOptions? options)
    {
        if (options?.AdditionalProperties?.TryGetValue(ConfigurationKey, out var value) == true)
        {
            return value as CodexAgentRunConfiguration;
        }

        return null;
    }
}
