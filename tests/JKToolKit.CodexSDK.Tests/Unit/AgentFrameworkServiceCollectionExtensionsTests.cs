using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCodexAgentClient_RegistersClient()
    {
        var services = new ServiceCollection();

        services.AddCodexAgentClient();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<CodexAgentClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddCodexAIAgent_RegistersAgent()
    {
        var services = new ServiceCollection();

        services.AddCodexAIAgent(options =>
        {
            options.Name = "CodexDiAgent";
            options.Description = "Registered through DI.";
        });

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<AIAgent>();

        agent.Name.Should().Be("CodexDiAgent");
        agent.Description.Should().Be("Registered through DI.");
        agent.GetService(typeof(AIAgentMetadata)).Should().BeOfType<AIAgentMetadata>()
            .Which.ProviderName.Should().Be("codex");
    }

    [Fact]
    public void AddKeyedCodexAIAgent_RegistersKeyedAgent()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AgentRegistrationMarker("from-services"));

        services.AddKeyedCodexAIAgent(
            "pizza",
            (sp, key) => new CodexAIAgentOptions
            {
                Name = $"{key}Agent",
                Description = sp.GetRequiredService<AgentRegistrationMarker>().Value
            });

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredKeyedService<AIAgent>("pizza");

        agent.Name.Should().Be("pizzaAgent");
        agent.Description.Should().Be("from-services");
    }

    private sealed record AgentRegistrationMarker(string Value);
}
