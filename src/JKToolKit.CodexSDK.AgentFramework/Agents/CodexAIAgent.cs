using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Internal;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.AgentFramework.Agents;

/// <summary>
/// Microsoft Agent Framework agent backed by Codex app-server.
/// </summary>
public sealed class CodexAIAgent : AIAgent
{
    private readonly CodexAgentClient _client;
    private readonly CodexAgentContextPipeline _contextPipeline;
    private readonly FunctionInvokingChatClient _functionInvokingChatClient;
    private readonly CodexAIAgentOptions _options;

    internal CodexAIAgent(CodexAgentClient client, CodexAIAgentOptions options)
    {
        _client = client;
        _options = options;
        _contextPipeline = new CodexAgentContextPipeline(this, options.ChatHistoryProvider, options.AIContextProviders);
        _functionInvokingChatClient = new FunctionInvokingChatClient(
            CodexAgentNoOpChatClient.Instance,
            functionInvocationServices: options.FunctionInvocationServices);
    }

    /// <inheritdoc />
    protected override string IdCore => _options.Id ?? "codex";

    /// <inheritdoc />
    public override string Name => _options.Name ?? "Codex";

    /// <inheritdoc />
    public override string Description => _options.Description ?? "Codex CLI agent.";

    /// <summary>
    /// Gets the Agent Framework chat history provider used by this agent, if configured.
    /// </summary>
    public ChatHistoryProvider? ChatHistoryProvider => _contextPipeline.ChatHistoryProvider;

    /// <summary>
    /// Gets the Agent Framework context providers used by this agent, if configured.
    /// </summary>
    public IReadOnlyList<AIContextProvider>? AIContextProviders => _contextPipeline.AIContextProviders;

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceKey is null && serviceType == typeof(AIAgentMetadata))
        {
            return new AIAgentMetadata("codex");
        }

        if (serviceKey is null && serviceType == typeof(FunctionInvokingChatClient))
        {
            return _functionInvokingChatClient;
        }

        if (serviceKey is null && serviceType == typeof(CodexAIAgentOptions))
        {
            return _options;
        }

        if (_contextPipeline.GetService(serviceType, serviceKey) is { } contextService)
        {
            return contextService;
        }

        return base.GetService(serviceType, serviceKey);
    }

    /// <inheritdoc />
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<AgentSession>(new CodexAgentSession());
    }

    /// <inheritdoc />
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(CodexAgentSessionJson.Serialize(GetSession(session), jsonSerializerOptions));
    }

    /// <inheritdoc />
    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedSession,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<AgentSession>(CodexAgentSessionJson.Deserialize(serializedSession));
    }

    /// <inheritdoc />
    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return RunCoreStreamingAsync(messages, session, options, cancellationToken)
            .ToAgentResponseAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestMessages = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();
        var codexSession = await ResolveSessionAsync(session, cancellationToken).ConfigureAwait(false);
        CurrentRunContext = new AgentRunContext(this, codexSession, requestMessages, options);

        var chatOptions = await CodexAgentChatOptionsMapper.GetEffectiveChatOptionsAsync(options, cancellationToken)
            .ConfigureAwait(false);
        var preparedRun = await _contextPipeline.PrepareAsync(codexSession, requestMessages, chatOptions, cancellationToken)
            .ConfigureAwait(false);
        var preparedRunContext = new AgentRunContext(this, codexSession, preparedRun.Messages, options);
        CurrentRunContext = preparedRunContext;
        chatOptions = preparedRun.ChatOptions;

        var configuredTools = await CodexAgentChatOptionsMapper.TransformToolsAsync(
            _options.Tools,
            options,
            cancellationToken).ConfigureAwait(false);
        var codexRunConfigurationTools = await CodexAgentChatOptionsMapper.TransformToolsAsync(
            options.GetCodexConfiguration()?.Tools,
            options,
            cancellationToken).ConfigureAwait(false);
        var toolSet = CreateToolSet(options, chatOptions, configuredTools, codexRunConfigurationTools, codexSession);
        var approvalHandler = toolSet.DynamicTools.Count == 0 ? null : toolSet.ApprovalHandler;
        using var functionInvocationScope = AgentFrameworkFunctionInvoker.PushEffectiveChatOptions(chatOptions);
        await using var codexLease = await _client.StartAppServerAsync(approvalHandler, cancellationToken)
            .ConfigureAwait(false);
        var codex = codexLease.Client;
        var thread = await ResolveThreadAsync(codex, codexSession, toolSet, options, chatOptions, cancellationToken)
            .ConfigureAwait(false);
        codexSession.ThreadId = thread.Id;

        await using var turn = await codex.StartTurnAsync(thread.Id, CreateTurnOptions(preparedRun.Messages, options, chatOptions), cancellationToken)
            .ConfigureAwait(false);

        var responseUpdates = new List<AgentResponseUpdate>();
        await using var updates = CodexAgentResponseMapper.StreamUpdatesAsync(turn, Id, Name, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            AgentResponseUpdate update;
            try
            {
                CurrentRunContext = preparedRunContext;
                if (!await updates.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                update = updates.Current;
            }
            catch (Exception ex)
            {
                await _contextPipeline.NotifyFailureAsync(preparedRun, codexSession, ex, cancellationToken)
                    .ConfigureAwait(false);
                throw;
            }

            responseUpdates.Add(update);
            yield return update;
        }

        await _contextPipeline.NotifySuccessAsync(
            preparedRun,
            codexSession,
            responseUpdates.ToAgentResponse().Messages,
            cancellationToken).ConfigureAwait(false);
    }

    private static CodexAgentSession GetSession(AgentSession session)
    {
        return session as CodexAgentSession
            ?? throw new ArgumentException("Session was not created by this Codex agent.", nameof(session));
    }

    private async ValueTask<CodexAgentSession> ResolveSessionAsync(AgentSession? session, CancellationToken cancellationToken)
    {
        return session is null
            ? (CodexAgentSession)await CreateSessionAsync(cancellationToken).ConfigureAwait(false)
            : GetSession(session);
    }

    private AgentFrameworkCodexToolSet CreateToolSet(
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions,
        IReadOnlyList<AITool>? configuredTools,
        IReadOnlyList<AITool>? codexRunConfigurationTools,
        CodexAgentSession session)
    {
        var functions = CodexAgentToolMapper.GetAIFunctions(
            configuredTools,
            codexRunConfigurationTools,
            runOptions,
            chatOptions).ToArray();
        if (session.ThreadId is not null && CodexAgentToolMapper.HasRunTools(runOptions, chatOptions))
        {
            throw new NotSupportedException(
                "Codex dynamic tools are configured when the Codex thread is created. Create a new AgentSession to use different per-run tools.");
        }

        return AgentFrameworkCodexToolAdapter.Create(
            functions,
            new AgentFrameworkCodexToolAdapterOptions
            {
                FunctionInvocationServices = CodexAgentOptionsMapper.GetFunctionInvocationServices(_options, runOptions),
                ToolApprovalHandler = CodexAgentOptionsMapper.GetToolApprovalHandler(_options, runOptions)
            });
    }

    private async Task<CodexThread> ResolveThreadAsync(
        CodexAppServerClient codex,
        CodexAgentSession session,
        AgentFrameworkCodexToolSet toolSet,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.ThreadId))
        {
            return await codex.ResumeThreadAsync(session.ThreadId, cancellationToken).ConfigureAwait(false);
        }

        return await codex.StartThreadAsync(CreateThreadOptions(toolSet, runOptions, chatOptions), cancellationToken)
            .ConfigureAwait(false);
    }

    private ThreadStartOptions CreateThreadOptions(
        AgentFrameworkCodexToolSet toolSet,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        var options = new ThreadStartOptions
        {
            Model = ParseModel(CodexAgentOptionsMapper.GetModel(_options, runOptions, chatOptions)),
            Cwd = CodexAgentOptionsMapper.GetCwd(_options, runOptions),
            ApprovalPolicy = CodexAgentOptionsMapper.GetApprovalPolicy(_options, runOptions),
            Sandbox = CodexAgentOptionsMapper.GetSandbox(_options, runOptions),
            DeveloperInstructions = CodexAgentOptionsMapper.GetInstructions(_options, chatOptions),
            DynamicTools = toolSet.DynamicTools.Count == 0 ? null : toolSet.DynamicTools
        };

        _options.ConfigureThread?.Invoke(options);
        return options;
    }

    private TurnStartOptions CreateTurnOptions(
        IEnumerable<ChatMessage> messages,
        AgentRunOptions? runOptions,
        ChatOptions? chatOptions)
    {
        var options = new TurnStartOptions
        {
            Input = CodexAgentMessageMapper.ToTurnInputItems(messages),
            Model = ParseModel(CodexAgentOptionsMapper.GetRunModel(runOptions, chatOptions)),
            Cwd = CodexAgentOptionsMapper.GetRunCwd(runOptions),
            ApprovalPolicy = CodexAgentOptionsMapper.GetRunApprovalPolicy(runOptions),
            SandboxPolicy = null,
            Effort = CodexAgentOptionsMapper.GetEffort(_options, runOptions, chatOptions),
            Summary = CodexAgentOptionsMapper.GetSummary(_options, runOptions, chatOptions),
            OutputSchema = CodexAgentOptionsMapper.GetOutputSchema(runOptions, chatOptions)
        };

        _options.ConfigureTurn?.Invoke(options);
        CodexAgentOptionsMapper.ConfigureRunTurn(options, runOptions);
        return options;
    }

    private static CodexModel? ParseModel(string? model)
    {
        if (CodexModel.TryParse(model, out var parsed))
        {
            return parsed;
        }

        return default;
    }
}
