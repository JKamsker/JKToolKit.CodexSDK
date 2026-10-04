using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using History = JKToolKit.CodexSDK.Tests.Unit.AgentFrameworkRunIntegrationTests.History;
using Context = JKToolKit.CodexSDK.Tests.Unit.AgentFrameworkRunIntegrationTests.Context;

namespace JKToolKit.CodexSDK.Tests.Unit;

#pragma warning disable MAAI001

public sealed class AgentFrameworkRuntimeContractTests
{
    private static ChatOptions HistoryOptions(ChatHistoryProvider history)
    {
        var options = new ChatOptions { AdditionalProperties = new() };
        options.AdditionalProperties.Add<ChatHistoryProvider>(history);
        return options;
    }

    [Fact]
    public async Task PrepareAsync_PerRunHistoryOverride_UsesOverrideForReadAndWrite()
    {
        var configured = new History("configured");
        var perRun = new History("per-run");
        var agent = new CodexAgentClient().AsAIAgent();
        var pipeline = new CodexAgentContextPipeline(agent, configured, [new Context()]);
        var session = new CodexAgentSession();
        var options = HistoryOptions(perRun);

        var prepared = await pipeline.PrepareAsync(session, [new(ChatRole.User, "hello")], options, default);
        await pipeline.NotifySuccessAsync(prepared, session, [new(ChatRole.Assistant, "done")], default);

        prepared.ChatHistoryProvider.Should().BeSameAs(perRun);
        prepared.Messages.Select(x => x.Text).Should().Equal("remembered", "hello", "context message");
        perRun.Completed.Should().ContainSingle();
        configured.Completed.Should().BeEmpty();
        options.Instructions.Should().BeNull("the caller's chat options must not be mutated");
    }

    [Fact]
    public async Task PrepareAsync_PerRunHistoryWithCollidingStateKey_IsRejected()
    {
        var pipeline = new CodexAgentContextPipeline(new CodexAgentClient().AsAIAgent(), null, [new Context("shared")]);
        var act = async () => await pipeline.PrepareAsync(new(), [],
            HistoryOptions(new History("shared")), default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*state key 'shared'*");
    }

    [Fact]
    public void AgentCreation_RejectsDuplicateContextProviderStateKeys()
    {
        var act = () => new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
        {
            AIContextProviders = [new Context("shared"), new Context("shared")]
        });
        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate state key 'shared'*");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"approvalPolicy\":\"\",\"sandbox\":\"\",\"stateBag\":null}")]
    public async Task DeserializeSession_MissingMetadata_CreatesReusableEmptySession(string json)
    {
        var agent = new CodexAgentClient().AsAIAgent();
        var session = (CodexAgentSession)await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(json));
        session.ThreadId.Should().BeNull();
        session.ApprovalPolicy.Should().BeNull();
        session.Sandbox.Should().BeNull();
        session.CreatedAt.Should().BeNull();
        session.StateBag.SetValue("key", "value");
        var roundTrip = (CodexAgentSession)await agent.DeserializeSessionAsync(await agent.SerializeSessionAsync(session));
        roundTrip.StateBag.GetValue<string>("key").Should().Be("value");
    }

    [Fact]
    public async Task FunctionInvocationMiddlewareService_ProvidesEmptyResponsesWithoutCallingCodex()
    {
        var agent = new CodexAgentClient().AsAIAgent();
        using var client = (FunctionInvokingChatClient)agent.GetService(typeof(FunctionInvokingChatClient))!;
        var response = await client.GetResponseAsync([new(ChatRole.User, "hello")]);
        response.Messages.Should().BeEmpty();
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync([new(ChatRole.User, "hello")])) updates.Add(update);
        updates.Should().BeEmpty();
        CodexAgentNoOpChatClient.Instance.GetService(typeof(IChatClient)).Should().BeSameAs(CodexAgentNoOpChatClient.Instance);
        CodexAgentNoOpChatClient.Instance.GetService(typeof(IChatClient), "key").Should().BeNull();
    }

    [Fact]
    public async Task SdkAgentOverloads_UseNativeOptionsAndModelOverride()
    {
        await using var sdk = CodexSdk.Create();
        var options = new ChatClientAgentOptions { Name = "named", ChatOptions = new() { ModelId = "base", Instructions = "instructions" } };
        var inherited = (CodexAIAgent)sdk.AsAIAgent(options);
        var overridden = (CodexAIAgent)sdk.AsAIAgent("override", options);
        inherited.Name.Should().Be("named");
        inherited.ChatOptions!.ModelId.Should().Be("base");
        overridden.ChatOptions!.ModelId.Should().Be("override");
        overridden.Instructions.Should().Be("instructions");
        options.ChatOptions.ModelId.Should().Be("base");
    }

    [Fact]
    public void KeyedAgentRegistration_ActionConfiguration_CreatesIndependentSingletons()
    {
        var services = new ServiceCollection();
        services.AddKeyedCodexAIAgent("one", options => options.Name = "first", _ => { });
        services.AddKeyedCodexAIAgent("two", options => options.Name = "second");
        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredKeyedService<AIAgent>("one");
        first.Name.Should().Be("first");
        provider.GetRequiredKeyedService<AIAgent>("one").Should().BeSameAs(first);
        provider.GetRequiredKeyedService<AIAgent>("two").Name.Should().Be("second");
    }

    [Fact]
    public async Task LocallyOwnedSdk_LeaseDisposal_ClosesInitializedStdioClient()
    {
        // This local process only performs the initialization handshake; it never calls Codex.
        var launch = OperatingSystem.IsWindows()
            ? CodexLaunch.FromFileName("pwsh").WithArgs("-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
                "[Console]::In.ReadLine() | Out-Null; [Console]::Out.WriteLine('{\"id\":1,\"result\":{\"userAgent\":\"local-test\"}}'); [Console]::In.ReadToEnd() | Out-Null")
            : CodexLaunch.FromFileName("/bin/bash").WithArgs("-c",
                "read -r request; printf '%s\\n' '{\"id\":1,\"result\":{\"userAgent\":\"local-test\"}}'; while read -r request; do :; done");
        var client = new CodexAgentClient(builder => builder.ConfigureAppServer(options =>
        {
            options.Launch = launch;
            options.StartupTimeout = TimeSpan.FromSeconds(10);
            options.ShutdownTimeout = TimeSpan.FromSeconds(2);
        }));
        await using var lease = await client.StartAppServerAsync(null, new(), default);
        lease.Client.InitializeResult.Should().NotBeNull();
        await lease.DisposeAsync();
        var requestAfterDisposal = () => lease.Client.StartThreadAsync(new ThreadStartOptions());
        await requestAfterDisposal.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task LocallyOwnedSdk_StartupFailure_PropagatesConfiguredExecutableError()
    {
        var client = new CodexAgentClient(builder => builder.CodexExecutablePath = "/no-such-codex-test-executable");
        var act = async () => await client.StartAppServerAsync(null, new(), default);
        await act.Should().ThrowAsync<Exception>().WithMessage("*/no-such-codex-test-executable*");
    }
}
