using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AgentFramework.Tools;
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
    [Fact]
    public void ConfigureCodex_InvalidReceiver_DoesNotInvokeConfigurationCallback()
    {
        var calls = 0;
        var configure = () => ((CodexAgentRunOptions)null!).ConfigureCodex(_ => calls++);
        configure.Should().Throw<ArgumentNullException>().WithParameterName("options");
        calls.Should().Be(0, "invalid input must be rejected before executing caller code");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedFunctionInvocations_PreserveOuterContextAcrossAsyncCompletionAndFailure(bool innerFails)
    {
        var callerContext = FunctionInvokingChatClient.CurrentContext;
        var inner = AIFunctionFactory.Create((Func<Task<string>>)(async () =>
        {
            await Task.Yield();
            FunctionInvokingChatClient.CurrentContext!.CallContent.CallId.Should().Be("inner-call");
            if (innerFails) throw new InvalidOperationException("inner failed");
            return "inner-result";
        }), "inner");
        var outer = AIFunctionFactory.Create((Func<Task<string>>)(async () =>
        {
            var outerContext = FunctionInvokingChatClient.CurrentContext;
            try
            {
                var result = await AgentFrameworkFunctionInvoker.InvokeAsync(inner, new(), new("inner-call", "inner", null), default);
                result!.ToString().Should().Be("inner-result");
            }
            catch (InvalidOperationException ex) when (innerFails && ex.Message == "inner failed") { }
            FunctionInvokingChatClient.CurrentContext.Should().BeSameAs(outerContext);
            FunctionInvokingChatClient.CurrentContext!.CallContent.CallId.Should().Be("outer-call");
            return "outer-result";
        }), "outer");

        var response = await AgentFrameworkFunctionInvoker.InvokeAsync(outer, new(), new("outer-call", "outer", null), default);

        response!.ToString().Should().Be("outer-result");
        FunctionInvokingChatClient.CurrentContext.Should().BeSameAs(callerContext);
    }

    [Fact]
    public async Task LocallyOwnedSdk_HandshakeFailure_TerminatesStartedProcess()
    {
        var pidPath = Path.Combine(Path.GetTempPath(), "codex-agent-pid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var launch = OperatingSystem.IsWindows()
                ? CodexLaunch.FromFileName("pwsh").WithArgs("-NoLogo", "-NoProfile", "-NonInteractive", "-Command",
                    "[IO.File]::WriteAllText($env:AGENT_RUNTIME_PID_PATH, $PID.ToString()); [Console]::In.ReadLine() | Out-Null; [Console]::Out.WriteLine('{\"id\":1,\"error\":{\"code\":-32603,\"message\":\"handshake rejected\"}}'); [Console]::In.ReadToEnd() | Out-Null")
                : CodexLaunch.FromFileName("/bin/bash").WithArgs("-c",
                    "printf '%s' \"$$\" > \"$AGENT_RUNTIME_PID_PATH\"; read -r request; printf '%s\\n' '{\"id\":1,\"error\":{\"code\":-32603,\"message\":\"handshake rejected\"}}'; while read -r request; do :; done");
            launch = launch.WithEnvironment("AGENT_RUNTIME_PID_PATH", pidPath);
            var client = new CodexAgentClient(builder => builder.ConfigureAppServer(options =>
            {
                options.Launch = launch;
                options.StartupTimeout = TimeSpan.FromSeconds(10);
                options.ShutdownTimeout = TimeSpan.FromSeconds(2);
            }));

            var start = async () => await client.StartAppServerAsync(null, new(), default);
            await start.Should().ThrowAsync<Exception>().WithMessage("*handshake rejected*");
            var pid = int.Parse(await File.ReadAllTextAsync(pidPath));
            Process? process;
            try { process = Process.GetProcessById(pid); }
            catch (ArgumentException) { process = null; }
            if (process is not null)
            {
                using (process)
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    process.HasExited.Should().BeTrue("failed initialization must release the local app-server process");
                }
            }
        }
        finally { File.Delete(pidPath); }
    }

    [Fact]
    public async Task ContextProvider_CanClearMessagesWhileRetainingUnrelatedChatOptions()
    {
        var pipeline = new CodexAgentContextPipeline(new CodexAgentClient().AsAIAgent(), null, [new EmptyContext()]);
        var prepared = await pipeline.PrepareAsync(new(), [new(ChatRole.User, "discard")],
            new ChatOptions { ModelId = "preserved", Temperature = 0.25f }, default);
        prepared.Messages.Should().BeEmpty();
        prepared.ChatOptions!.ModelId.Should().Be("preserved");
        prepared.ChatOptions.Temperature.Should().Be(0.25f);
    }

    private sealed class EmptyContext : AIContextProvider
    {
        protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AIContext());
    }

    [Fact]
    public async Task EffectiveOptionsScopes_RestoreOuterOptionsAfterNestedInvocation()
    {
        var function = AIFunctionFactory.Create(() => FunctionInvokingChatClient.CurrentContext?.Options?.ModelId ?? "none", "read_model");
        var call = new FunctionCallContent("call", "read_model", null);
        async Task<string?> Invoke() => (await AgentFrameworkFunctionInvoker.InvokeAsync(function, new(), call, default))?.ToString();
        (await Invoke()).Should().Be("none");
        using (AgentFrameworkFunctionInvoker.PushEffectiveChatOptions(new() { ModelId = "outer" }))
        {
            using (AgentFrameworkFunctionInvoker.PushEffectiveChatOptions(new() { ModelId = "inner" }))
                (await Invoke()).Should().Be("inner");
            (await Invoke()).Should().Be("outer");
        }
        (await Invoke()).Should().Be("none");
    }

    [Fact]
    public void ContextServices_PreferContextProviderOverHistoryProvider()
    {
        var contextService = new object();
        var historyService = new object();
        var agent = new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
        {
            AIContextProviders = [new ServiceContext(contextService)],
            ChatHistoryProvider = new ServiceHistory(historyService)
        });
        agent.GetService(typeof(object)).Should().BeSameAs(contextService);
        agent.GetService(typeof(object), "history").Should().BeSameAs(historyService);
    }

    private sealed class ServiceContext(object service) : AIContextProvider
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(object) && serviceKey is null ? service : null;
    }

    private sealed class ServiceHistory(object service) : ChatHistoryProvider
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(object) ? service : null;
    }

    [Fact]
    public void PublicFactoryAndConfigurationGuards_RejectNullArgumentsAtCallTime()
    {
        var client = new CodexAgentClient();
        var services = new ServiceCollection();
        (string Parameter, Action Call)[] cases =
        [
            ("sdk", () => new CodexAgentClient((CodexSdk)null!)),
            ("client", () => ((CodexAgentClient)null!).AsAIAgent(new CodexAIAgentOptions())),
            ("options", () => client.AsAIAgent((CodexAIAgentOptions)null!)),
            ("client", () => ((CodexAgentClient)null!).AsAIAgent()),
            ("client", () => ((CodexAgentClient)null!).AsAIAgent(new ChatClientAgentOptions())),
            ("client", () => ((CodexAgentClient)null!).AsAIAgent("model", new ChatClientAgentOptions())),
            ("options", () => client.AsAIAgent((ChatClientAgentOptions)null!)),
            ("options", () => client.AsAIAgent("model", (ChatClientAgentOptions)null!)),
            ("sdk", () => ((CodexSdk)null!).AsAIAgent(new CodexAIAgentOptions())),
            ("sdk", () => ((CodexSdk)null!).AsAIAgent()),
            ("sdk", () => ((CodexSdk)null!).AsAIAgent(new ChatClientAgentOptions())),
            ("sdk", () => ((CodexSdk)null!).AsAIAgent("model", new ChatClientAgentOptions())),
            ("options", () => ((CodexAgentRunOptions)null!).WithCodex(new())),
            ("configuration", () => new CodexAgentRunOptions().WithCodex(null!)),
            ("options", () => ((CodexAgentRunOptions)null!).ConfigureCodex(_ => { })),
            ("configure", () => new CodexAgentRunOptions().ConfigureCodex(null!)),
            ("configureAgent", () => services.AddCodexAIAgent((Action<CodexAIAgentOptions>)null!)),
            ("options", () => services.AddCodexAIAgent((ChatClientAgentOptions)null!)),
            ("optionsFactory", () => services.AddCodexAIAgent((Func<IServiceProvider, CodexAIAgentOptions>)null!)),
            ("configureAgent", () => services.AddKeyedCodexAIAgent("key", (Action<CodexAIAgentOptions>)null!)),
            ("options", () => services.AddKeyedCodexAIAgent("key", (ChatClientAgentOptions)null!)),
            ("optionsFactory", () => services.AddKeyedCodexAIAgent("key", (Func<IServiceProvider, object?, CodexAIAgentOptions>)null!)),
            ("services", () => ((IServiceCollection)null!).AddCodexAgentClient()),
            ("services", () => ((IServiceCollection)null!).AddCodexAIAgent(_ => { })),
            ("services", () => ((IServiceCollection)null!).AddCodexAIAgent(new ChatClientAgentOptions())),
            ("services", () => ((IServiceCollection)null!).AddCodexAIAgent(_ => new CodexAIAgentOptions())),
            ("services", () => ((IServiceCollection)null!).AddKeyedCodexAIAgent("key", _ => { })),
            ("services", () => ((IServiceCollection)null!).AddKeyedCodexAIAgent("key", new ChatClientAgentOptions())),
            ("services", () => ((IServiceCollection)null!).AddKeyedCodexAIAgent("key", (_, _) => new CodexAIAgentOptions())),
            ("client", () => new CodexAgentAppServerLease(null!, null)),
            ("serviceType", () => CodexAgentNoOpChatClient.Instance.GetService(null!))
        ];
        foreach (var (parameter, call) in cases)
            call.Should().Throw<ArgumentNullException>().WithParameterName(parameter);
    }

    [Fact]
    public async Task RuntimeConfiguration_PreservesUnrelatedPropertiesAndValidatesFactories()
    {
        var options = new CodexAgentRunOptions { AdditionalProperties = new() { ["caller"] = "retained" } };
        options.WithCodex(new() { Model = "first" }).ConfigureCodex(config => config.Model = "second");
        options.AdditionalProperties!["caller"].Should().Be("retained");
        options.GetCodexConfiguration()!.Model.Should().Be("second");
        ((CodexAgentRunOptions)options.Clone()).Tools.Should().BeNull();
        var services = new ServiceCollection();
        services.AddCodexAIAgent(_ => (CodexAIAgentOptions)null!);
        using var provider = services.BuildServiceProvider();
        var resolve = () => provider.GetRequiredService<AIAgent>();
        resolve.Should().Throw<ArgumentNullException>().WithParameterName("options");
        var start = async () => await new CodexAgentClient().StartAppServerAsync(null, null!, default);
        await start.Should().ThrowAsync<ArgumentNullException>().WithParameterName("agentOptions");
    }

    [Fact]
    public async Task SessionSerialization_HonorsCallerFormattingOptions()
    {
        var agent = new CodexAgentClient().AsAIAgent();
        var serialized = await agent.SerializeSessionAsync(new CodexAgentSession { ThreadId = "thread" }, new() { WriteIndented = true });
        serialized.GetRawText().Should().Contain("\n");
        serialized.GetProperty("threadId").GetString().Should().Be("thread");
    }

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
        prepared.ChatOptions!.AdditionalProperties![typeof(ChatHistoryProvider).FullName!].Should().BeSameAs(perRun);
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
        Action nullNative = () => sdk.AsAIAgent((ChatClientAgentOptions)null!);
        nullNative.Should().Throw<ArgumentNullException>().WithParameterName("options");
        Action nullConfigured = () => sdk.AsAIAgent((CodexAIAgentOptions)null!);
        nullConfigured.Should().Throw<ArgumentNullException>().WithParameterName("options");
        Action nullOverride = () => sdk.AsAIAgent("model", (ChatClientAgentOptions)null!);
        nullOverride.Should().Throw<ArgumentNullException>().WithParameterName("options");
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
                "$request = [Console]::In.ReadLine(); [Console]::Out.WriteLine('{\"id\":1,\"result\":{\"request\":' + $request + '}}'); [Console]::In.ReadToEnd() | Out-Null")
            : CodexLaunch.FromFileName("/bin/bash").WithArgs("-c",
                "read -r request; printf '{\"id\":1,\"result\":{\"request\":%s}}\\n' \"$request\"; while read -r request; do :; done");
        var client = new CodexAgentClient(builder => builder.ConfigureAppServer(options =>
        {
            options.Launch = launch;
            options.StartupTimeout = TimeSpan.FromSeconds(10);
            options.ShutdownTimeout = TimeSpan.FromSeconds(2);
        }));
        var tools = AgentFrameworkCodexToolAdapter.Create([AIFunctionFactory.Create(() => "ok", "read")]);
        await using var lease = await client.StartAppServerAsync(tools.ApprovalHandler, new(), default);
        lease.Client.InitializeResult.Should().NotBeNull();
        lease.Client.InitializeResult!.Raw.GetProperty("request").GetProperty("params").GetProperty("capabilities")
            .GetProperty("experimentalApi").GetBoolean().Should().BeTrue();
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
