using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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

    [Fact]
    public void AddCodexAIAgent_RegistersChatClientAgentOptions()
    {
        var services = new ServiceCollection();

        services.AddCodexAIAgent(
            new ChatClientAgentOptions
            {
                Name = "NativeOptionsAgent",
                ChatOptions = new ChatOptions
                {
                    ModelId = "default-model",
                    Instructions = "Use native Agent Framework options."
                }
            },
            model: "override-model");

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredService<AIAgent>().Should().BeOfType<CodexAIAgent>().Subject;

        agent.Name.Should().Be("NativeOptionsAgent");
        agent.ChatOptions!.ModelId.Should().Be("override-model");
        agent.Instructions.Should().Be("Use native Agent Framework options.");
    }

    [Fact]
    public void AddKeyedCodexAIAgent_RegistersChatClientAgentOptions()
    {
        var services = new ServiceCollection();

        services.AddKeyedCodexAIAgent(
            "native",
            new ChatClientAgentOptions
            {
                Name = "KeyedNativeAgent",
                ChatOptions = new ChatOptions
                {
                    Instructions = "Registered from native options."
                }
            });

        using var provider = services.BuildServiceProvider();
        var agent = provider.GetRequiredKeyedService<AIAgent>("native").Should()
            .BeOfType<CodexAIAgent>().Subject;

        agent.Name.Should().Be("KeyedNativeAgent");
        agent.Instructions.Should().Be("Registered from native options.");
    }

    private sealed record AgentRegistrationMarker(string Value);
}
