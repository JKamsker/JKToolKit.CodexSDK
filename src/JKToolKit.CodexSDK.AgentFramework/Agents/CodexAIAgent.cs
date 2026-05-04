using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
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
    private readonly CodexAIAgentOptions _options;

    internal CodexAIAgent(CodexAgentClient client, CodexAIAgentOptions options)
    {
        _client = client;
        _options = options;
    }

    /// <inheritdoc />
    protected override string IdCore => _options.Id ?? "codex";

    /// <inheritdoc />
    public override string Name => _options.Name ?? "Codex";

    /// <inheritdoc />
    public override string Description => _options.Description ?? "Codex CLI agent.";

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType == typeof(AIAgentMetadata)
            ? new AIAgentMetadata("codex")
            : base.GetService(serviceType, serviceKey);
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
        var codexSession = await ResolveSessionAsync(session, cancellationToken).ConfigureAwait(false);
        var toolSet = CreateToolSet(options, codexSession);
        var approvalHandler = toolSet.DynamicTools.Count == 0 ? null : toolSet.ApprovalHandler;
        await using var sdk = _client.CreateSdk(approvalHandler);
        await using var codex = await sdk.AppServer.StartAsync(cancellationToken).ConfigureAwait(false);
        var thread = await ResolveThreadAsync(codex, codexSession, toolSet, options, cancellationToken).ConfigureAwait(false);
        codexSession.ThreadId = thread.Id;

        await using var turn = await codex.StartTurnAsync(thread.Id, CreateTurnOptions(messages, options), cancellationToken)
            .ConfigureAwait(false);

        await foreach (var update in StreamUpdatesAsync(turn, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
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

    private AgentFrameworkCodexToolSet CreateToolSet(AgentRunOptions? runOptions, CodexAgentSession session)
    {
        var functions = CodexAgentToolMapper.GetAIFunctions(_options.Tools, runOptions).ToArray();
        if (session.ThreadId is not null && CodexAgentToolMapper.HasRunTools(runOptions))
        {
            throw new NotSupportedException(
                "Codex dynamic tools are configured when the Codex thread is created. Create a new AgentSession to use different per-run tools.");
        }

        return AgentFrameworkCodexToolAdapter.Create(functions);
    }

    private async Task<CodexThread> ResolveThreadAsync(
        CodexAppServerClient codex,
        CodexAgentSession session,
        AgentFrameworkCodexToolSet toolSet,
        AgentRunOptions? runOptions,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.ThreadId))
        {
            return await codex.ResumeThreadAsync(session.ThreadId, cancellationToken).ConfigureAwait(false);
        }

        return await codex.StartThreadAsync(CreateThreadOptions(toolSet, runOptions), cancellationToken).ConfigureAwait(false);
    }

    private ThreadStartOptions CreateThreadOptions(AgentFrameworkCodexToolSet toolSet, AgentRunOptions? runOptions)
    {
        var options = new ThreadStartOptions
        {
            Model = ParseModel(CodexAgentOptionsMapper.GetModel(_options, runOptions)),
            Cwd = CodexAgentOptionsMapper.GetCwd(_options, runOptions),
            ApprovalPolicy = CodexAgentOptionsMapper.GetApprovalPolicy(_options, runOptions),
            Sandbox = CodexAgentOptionsMapper.GetSandbox(_options, runOptions),
            DeveloperInstructions = CodexAgentOptionsMapper.GetInstructions(_options, runOptions),
            DynamicTools = toolSet.DynamicTools.Count == 0 ? null : toolSet.DynamicTools
        };

        _options.ConfigureThread?.Invoke(options);
        return options;
    }

    private TurnStartOptions CreateTurnOptions(IEnumerable<ChatMessage> messages, AgentRunOptions? runOptions)
    {
        var options = new TurnStartOptions
        {
            Input = CodexAgentMessageMapper.ToTurnInputItems(messages),
            Model = ParseModel(CodexAgentOptionsMapper.GetRunModel(runOptions)),
            Cwd = CodexAgentOptionsMapper.GetRunCwd(runOptions),
            ApprovalPolicy = CodexAgentOptionsMapper.GetRunApprovalPolicy(runOptions),
            SandboxPolicy = null,
            OutputSchema = CodexAgentOptionsMapper.GetOutputSchema(runOptions)
        };

        _options.ConfigureTurn?.Invoke(options);
        (runOptions as CodexAgentRunOptions)?.ConfigureTurn?.Invoke(options);
        return options;
    }

    private async IAsyncEnumerable<AgentResponseUpdate> StreamUpdatesAsync(
        CodexTurnHandle turn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var ev in turn.Events(cancellationToken).ConfigureAwait(false))
        {
            switch (ev)
            {
                case AgentMessageDeltaNotification delta:
                    yield return new AgentResponseUpdate(ChatRole.Assistant, delta.Delta)
                    {
                        AgentId = Id,
                        AuthorName = Name,
                        ResponseId = turn.TurnId,
                        MessageId = delta.ItemId,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    break;
                case ErrorNotification error:
                    yield return new AgentResponseUpdate(ChatRole.Assistant, [new ErrorContent(error.Error.GetRawText())])
                    {
                        AgentId = Id,
                        AuthorName = Name,
                        ResponseId = turn.TurnId
                    };
                    break;
            }
        }

        var completed = await turn.Completion.ConfigureAwait(false);
        if (!string.Equals(completed.Status, "completed", StringComparison.OrdinalIgnoreCase) &&
            completed.Error is { } completionError)
        {
            yield return new AgentResponseUpdate(ChatRole.Assistant, [new ErrorContent(completionError.GetRawText())])
            {
                AgentId = Id,
                AuthorName = Name,
                ResponseId = turn.TurnId,
                FinishReason = ChatFinishReason.Stop
            };
        }
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
