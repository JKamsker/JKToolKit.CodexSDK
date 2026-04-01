using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ResilientCodexAppServerClientTests
{
    private static readonly string[] StableParityMethodNames =
    [
        nameof(CodexAppServerClient.CallAsync),
        nameof(CodexAppServerClient.StartThreadAsync),
        nameof(CodexAppServerClient.ResumeThreadAsync),
        nameof(CodexAppServerClient.ListThreadsAsync),
        nameof(CodexAppServerClient.ReadThreadAsync),
        nameof(CodexAppServerClient.ListLoadedThreadsAsync),
        nameof(CodexAppServerClient.UnsubscribeThreadAsync),
        nameof(CodexAppServerClient.CompactThreadAsync),
        nameof(CodexAppServerClient.RollbackThreadAsync),
        nameof(CodexAppServerClient.CleanThreadBackgroundTerminalsAsync),
        nameof(CodexAppServerClient.ForkThreadAsync),
        nameof(CodexAppServerClient.ArchiveThreadAsync),
        nameof(CodexAppServerClient.UnarchiveThreadAsync),
        nameof(CodexAppServerClient.SetThreadNameAsync),
        nameof(CodexAppServerClient.ListSkillsAsync),
        nameof(CodexAppServerClient.ListAppsAsync),
        nameof(CodexAppServerClient.ReadConfigRequirementsAsync),
        nameof(CodexAppServerClient.ReadRemoteSkillsAsync),
        nameof(CodexAppServerClient.WriteRemoteSkillAsync),
        nameof(CodexAppServerClient.WriteSkillsConfigAsync),
        nameof(CodexAppServerClient.ReadConfigAsync),
        nameof(CodexAppServerClient.DetectExternalAgentConfigAsync),
        nameof(CodexAppServerClient.ImportExternalAgentConfigAsync),
        nameof(CodexAppServerClient.ReadAccountAsync),
        nameof(CodexAppServerClient.ReadAccountRateLimitsAsync),
        nameof(CodexAppServerClient.ListModelsAsync),
        nameof(CodexAppServerClient.ListExperimentalFeaturesAsync),
        nameof(CodexAppServerClient.WriteConfigValueAsync),
        nameof(CodexAppServerClient.WriteConfigBatchAsync),
        nameof(CodexAppServerClient.LogoutAccountAsync),
        nameof(CodexAppServerClient.UploadFeedbackAsync),
        nameof(CodexAppServerClient.StartWindowsSandboxSetupAsync),
        nameof(CodexAppServerClient.ReloadMcpServersAsync),
        nameof(CodexAppServerClient.ListMcpServerStatusAsync),
        nameof(CodexAppServerClient.StartMcpServerOauthLoginAsync),
        nameof(CodexAppServerClient.StartAccountLoginAsync),
        nameof(CodexAppServerClient.CancelAccountLoginAsync),
        nameof(CodexAppServerClient.StartFuzzyFileSearchSessionAsync),
        nameof(CodexAppServerClient.UpdateFuzzyFileSearchSessionAsync),
        nameof(CodexAppServerClient.StopFuzzyFileSearchSessionAsync),
        nameof(CodexAppServerClient.FuzzyFileSearchAsync),
        nameof(CodexAppServerClient.StartTurnAsync),
        nameof(CodexAppServerClient.SteerTurnAsync),
        nameof(CodexAppServerClient.SteerTurnRawAsync),
        nameof(CodexAppServerClient.StartReviewAsync),
        nameof(CodexAppServerClient.ReviewAsync),
        nameof(CodexAppServerClient.ListPluginsAsync),
        nameof(CodexAppServerClient.ReadPluginAsync),
        nameof(CodexAppServerClient.InstallPluginAsync),
        nameof(CodexAppServerClient.UninstallPluginAsync),
        nameof(CodexAppServerClient.CommandExecAsync),
        nameof(CodexAppServerClient.CommandExecWriteAsync),
        nameof(CodexAppServerClient.CommandExecResizeAsync),
        nameof(CodexAppServerClient.CommandExecTerminateAsync),
        nameof(CodexAppServerClient.FsWatchAsync),
        nameof(CodexAppServerClient.FsUnwatchAsync),
        nameof(CodexAppServerClient.ListCollaborationModesAsync),
        nameof(CodexAppServerClient.StartThreadRealtimeAsync),
        nameof(CodexAppServerClient.AppendThreadRealtimeAudioAsync),
        nameof(CodexAppServerClient.AppendThreadRealtimeTextAsync),
        nameof(CodexAppServerClient.StopThreadRealtimeAsync)
    ];

    [Fact]
    public void ResilientClient_ExposesStableDirectClientParityMethods()
    {
        AssertStableMethodParity(typeof(ResilientCodexAppServerClient));
    }

    [Fact]
    public async Task ResilientClient_ExposesInitializeResultAndNotificationDropStats()
    {
        var initResult = new AppServerInitializeResult(
            JsonDocument.Parse("""{"userAgent":"demo/1.0"}""").RootElement);
        var stats = new AppServerNotificationDropStats(1, 2, 3, 4, 5, 6);

        var adapter = new FakeAdapter
        {
            InitializeResultValue = initResult,
            NotificationDropStatsValue = stats
        };

        var factory = new SequenceFactory(adapter);
        await using var client = await StartAsync(factory, new CodexAppServerResilienceOptions());

        client.InitializeResult.Should().Be(initResult);
        client.NotificationDropStats.Should().Be(stats);
    }

    [Fact]
    public void Adapter_ExposesStableDirectClientParityMethods()
    {
        AssertStableMethodParity(typeof(ICodexAppServerClientAdapter));
    }

    [Fact]
    public async Task CallAsync_WhenDisconnected_Restarts_AndCanRetry_WhenPolicyAllows()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 42)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);

        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RetryPolicy = ctx => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var result = await client.CallAsync("ping", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    [Fact]
    public async Task CallAsync_WhenDisconnected_DoesNotRetryByDefault_ButRestartsForNextCall()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 1)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions { AutoRestart = true };

        await using var client = await StartAsync(factory, options);

        var act = async () => await client.CallAsync("ping", @params: null);
        await act.Should().ThrowAsync<CodexAppServerDisconnectedException>();

        var result = await client.CallAsync("ping", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    [Fact]
    public async Task CallAsync_WhenServerOverloaded_RetriedByPolicyWithoutRestart()
    {
        var attempts = 0;
        var adapter = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                if (attempts++ == 0)
                {
                    throw new JsonRpcRemoteException(new JsonRpcError(
                        -32001,
                        "Server overloaded; retry later."));
                }

                using var resultDoc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(resultDoc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(adapter);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = false,
            RetryPolicy = _ => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var result = await client.CallAsync("ping", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        attempts.Should().Be(2);
        factory.StartCount.Should().Be(1);
        client.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task CallAsync_RequestFailedOverload_RetriedByPolicy()
    {
        var attempts = 0;
        var adapter = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                if (attempts++ == 0)
                {
                    using var doc = JsonDocument.Parse("""{"codexErrorInfo":"serverOverloaded"}""");
                    throw new CodexAppServerRequestFailedException(
                        method: "turn/steer",
                        errorCode: -32001,
                        errorMessage: "server overloaded; retry later.",
                        errorData: doc.RootElement.Clone(),
                        userAgent: null);
                }

                using var resultDoc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(resultDoc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(adapter);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = false,
            RetryPolicy = _ => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var result = await client.CallAsync("turn/steer", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        attempts.Should().Be(2);
        factory.StartCount.Should().Be(1);
        client.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task Notifications_ContinueAcrossRestarts_AndEmitRestartMarker()
    {
        var first = new FakeAdapter
        {
            NotificationsImpl = FirstNotifications
        };

        var second = new FakeAdapter
        {
            NotificationsImpl = SecondNotifications
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            NotificationsContinueAcrossRestarts = true,
            EmitRestartMarkerNotifications = true
        };

        await using var client = await StartAsync(factory, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var seen = new List<string>();

        await foreach (var n in client.Notifications(cts.Token))
        {
            seen.Add(n.Method);
            if (seen.Count >= 3)
                break;
        }

        seen.Should().ContainInOrder("note/one", "client/restarted", "note/two");
        factory.StartCount.Should().Be(2);
    }

    [Fact]
    public async Task NotificationsRaw_ContinueAcrossRestarts_AndEmitRestartMarker()
    {
        var first = new FakeAdapter
        {
            NotificationsRawImpl = FirstRawNotifications
        };

        var second = new FakeAdapter
        {
            NotificationsRawImpl = SecondRawNotifications
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            NotificationsContinueAcrossRestarts = true,
            EmitRestartMarkerNotifications = true
        };

        await using var client = await StartAsync(factory, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var seen = new List<string>();

        await foreach (var n in client.NotificationsRaw(cts.Token))
        {
            seen.Add(n.Method);
            if (seen.Count >= 3)
                break;
        }

        seen.Should().ContainInOrder("note/raw-one", "client/restarted", "note/raw-two");
        factory.StartCount.Should().Be(2);
    }

    [Fact]
    public async Task RepresentativeTypedForwarders_InvokeMatchingAdapterMethods()
    {
        var invoked = new List<string>();
        var adapter = new FakeAdapter
        {
            CompactThreadAsyncImpl = (_, _) =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.CompactThreadAsync));
                return Task.CompletedTask;
            },
            ImportExternalAgentConfigAsyncImpl = (_, _) =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.ImportExternalAgentConfigAsync));
                return Task.CompletedTask;
            },
            ReloadMcpServersAsyncImpl = _ =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.ReloadMcpServersAsync));
                return Task.CompletedTask;
            },
            StartFuzzyFileSearchSessionAsyncImpl = (_, _, _) =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.StartFuzzyFileSearchSessionAsync));
                return Task.CompletedTask;
            },
            SteerTurnAsyncImpl = (_, _) =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.SteerTurnAsync));
                return Task.FromResult("steered");
            },
            StartThreadRealtimeAsyncImpl = (_, _, _, _) =>
            {
                invoked.Add(nameof(ICodexAppServerClientAdapter.StartThreadRealtimeAsync));
                return Task.CompletedTask;
            }
        };

        var factory = new SequenceFactory(adapter);
        await using var client = await CreateResilientAsync(factory, new CodexAppServerResilienceOptions());

        await client.CompactThreadAsync("thread-1");
        await client.ImportExternalAgentConfigAsync([]);
        await client.ReloadMcpServersAsync();
        await client.StartFuzzyFileSearchSessionAsync("session-1", ["C:\\repo"]);
        var steerResult = await client.SteerTurnAsync(new TurnSteerOptions
        {
            ThreadId = "thread-1",
            ExpectedTurnId = "turn-1",
            Input = [TurnInputItem.Text("continue")]
        });
        await client.StartThreadRealtimeAsync("thread-1", "speak");

        steerResult.Should().Be("steered");
        invoked.Should().ContainInOrder(
            nameof(ICodexAppServerClientAdapter.CompactThreadAsync),
            nameof(ICodexAppServerClientAdapter.ImportExternalAgentConfigAsync),
            nameof(ICodexAppServerClientAdapter.ReloadMcpServersAsync),
            nameof(ICodexAppServerClientAdapter.StartFuzzyFileSearchSessionAsync),
            nameof(ICodexAppServerClientAdapter.SteerTurnAsync),
            nameof(ICodexAppServerClientAdapter.StartThreadRealtimeAsync));
    }

    private static async IAsyncEnumerable<AppServerNotification> FirstNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new UnknownNotification("note/one", EmptyJson());
        throw Disconnect("nope", exitCode: 7);
    }

    private static async IAsyncEnumerable<AppServerNotification> SecondNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new UnknownNotification("note/two", EmptyJson());
    }

    private static async IAsyncEnumerable<AppServerRpcNotification> FirstRawNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new AppServerRpcNotification("note/raw-one", EmptyJson());
        throw Disconnect("nope", exitCode: 7);
    }

    private static async IAsyncEnumerable<AppServerRpcNotification> SecondRawNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new AppServerRpcNotification("note/raw-two", EmptyJson());
    }

    [Fact]
    public async Task RestartLimit_IsEnforced_AndClientFaults()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 2)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom2", exitCode: 3)
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RestartPolicy = new CodexAppServerRestartPolicy
            {
                MaxRestarts = 1,
                Window = TimeSpan.FromMinutes(1),
                InitialBackoff = TimeSpan.Zero,
                MaxBackoff = TimeSpan.Zero,
                JitterFraction = 0
            }
        };

        await using var client = await StartAsync(factory, options);

        var act1 = async () => await client.CallAsync("ping", @params: null);
        await act1.Should().ThrowAsync<CodexAppServerDisconnectedException>();

        var act2 = async () => await client.CallAsync("ping", @params: null);
        await act2.Should().ThrowAsync<CodexAppServerUnavailableException>();

        client.State.Should().Be(CodexAppServerConnectionState.Faulted);
    }

    [Fact]
    public async Task ConcurrentDisconnect_TriggersSingleRestart()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 1)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RetryPolicy = ctx => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var el = await client.CallAsync("ping", @params: null);
            el.GetProperty("ok").GetBoolean().Should().BeTrue();
        });

        await Task.WhenAll(tasks);

        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    private static async Task<ResilientCodexAppServerClient> StartAsync(
        SequenceFactory factory,
        CodexAppServerResilienceOptions options)
    {
        return await CreateResilientAsync(factory, options);
    }

    private static Task<ResilientCodexAppServerClient> CreateResilientAsync(
        SequenceFactory factory,
        CodexAppServerResilienceOptions options)
    {
        var logger = NullLogger.Instance;

        var resilient = new ResilientCodexAppServerClient(
            startInner: ct => Task.FromResult<ICodexAppServerClientAdapter>(factory.Start()),
            options: options,
            logger: logger);

        return ConnectAsync(resilient);
    }

    private static async Task<ResilientCodexAppServerClient> ConnectAsync(ResilientCodexAppServerClient resilient)
    {
        await resilient.EnsureConnectedAsync();
        return resilient;
    }

    private static CodexAppServerDisconnectedException Disconnect(string message, int exitCode) =>
        new(
            message,
            processId: 123,
            exitCode: exitCode,
            stderrTail: new[] { "stderr: test" });

    private static void AssertStableMethodParity(Type parityType)
    {
        var directMethods = typeof(CodexAppServerClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => StableParityMethodNames.Contains(m.Name, StringComparer.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.GetParameters().Length)
            .ToArray();

        directMethods.Should().NotBeEmpty();

        var parityMethods = parityType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => StableParityMethodNames.Contains(m.Name, StringComparer.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.GetParameters().Length)
            .ToArray();

        parityMethods.Should().HaveCount(directMethods.Length);

        foreach (var directMethod in directMethods)
        {
            var parameterTypes = directMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            var parityMethod = parityType.GetMethod(
                directMethod.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            parityMethod.Should().NotBeNull($"{parityType.Name} should expose {FormatSignature(directMethod)}");
            AssertEquivalentType(parityMethod!.ReturnType, directMethod.ReturnType);
        }
    }

    private static void AssertEquivalentType(Type actual, Type expected)
    {
        actual.IsGenericParameter.Should().Be(expected.IsGenericParameter);

        if (expected.IsGenericParameter)
        {
            actual.GenericParameterPosition.Should().Be(expected.GenericParameterPosition);
            return;
        }

        actual.IsGenericType.Should().Be(expected.IsGenericType);

        if (expected.IsGenericType)
        {
            actual.GetGenericTypeDefinition().Should().Be(expected.GetGenericTypeDefinition());

            var actualArguments = actual.GetGenericArguments();
            var expectedArguments = expected.GetGenericArguments();
            actualArguments.Should().HaveCount(expectedArguments.Length);

            for (var i = 0; i < expectedArguments.Length; i++)
            {
                AssertEquivalentType(actualArguments[i], expectedArguments[i]);
            }

            return;
        }

        actual.Should().Be(expected);
    }

    private static string FormatSignature(MethodInfo methodInfo)
    {
        var parameters = string.Join(", ", methodInfo.GetParameters().Select(p => p.ParameterType.Name));
        return $"{methodInfo.ReturnType.Name} {methodInfo.Name}({parameters})";
    }

    private static JsonElement EmptyJson()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private sealed class SequenceFactory
    {
        private readonly Queue<FakeAdapter> _queue;
        public int StartCount { get; private set; }

        public SequenceFactory(params FakeAdapter[] adapters)
        {
            _queue = new Queue<FakeAdapter>(adapters);
        }

        public FakeAdapter Start()
        {
            StartCount++;
            return _queue.Count > 0 ? _queue.Dequeue() : new FakeAdapter();
        }
    }

    private sealed class FakeAdapter : ICodexAppServerClientAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<string, object?, CancellationToken, Task<JsonElement>>? CallAsyncImpl { get; init; }

        public Func<string, object?, JsonSerializerOptions?, CancellationToken, Task<object?>>? CallAsyncTypedImpl { get; init; }

        public Func<CancellationToken, IAsyncEnumerable<AppServerNotification>>? NotificationsImpl { get; init; }
        public Func<CancellationToken, IAsyncEnumerable<AppServerRpcNotification>>? NotificationsRawImpl { get; init; }

        public Func<string, CancellationToken, Task>? CompactThreadAsyncImpl { get; init; }

        public Func<IReadOnlyList<ExternalAgentConfigMigrationItem>, CancellationToken, Task>? ImportExternalAgentConfigAsyncImpl { get; init; }

        public Func<CancellationToken, Task>? ReloadMcpServersAsyncImpl { get; init; }

        public Func<string, IReadOnlyList<string>, CancellationToken, Task>? StartFuzzyFileSearchSessionAsyncImpl { get; init; }

        public Func<TurnSteerOptions, CancellationToken, Task<string>>? SteerTurnAsyncImpl { get; init; }

        public Func<string, IReadOnlyList<string>, string?, CancellationToken, Task<IReadOnlyList<FuzzyFileSearchResult>>>? FuzzyFileSearchAsyncImpl { get; init; }

        public Func<string, string, string?, CancellationToken, Task>? StartThreadRealtimeAsyncImpl { get; init; }

        public Task ExitTask => _exit.Task;

        public AppServerInitializeResult? InitializeResultValue { get; init; }

        public AppServerNotificationDropStats NotificationDropStatsValue { get; init; } = new(0, 0, 0, 0, 0, 0);

        public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct)
        {
            if (CallAsyncImpl is null)
                throw new InvalidOperationException("CallAsyncImpl not configured.");
            return CallAsyncImpl(method, @params, ct);
        }

        public async Task<TResult?> CallAsync<TResult>(string method, object? @params, JsonSerializerOptions? serializerOptions, CancellationToken ct)
        {
            if (CallAsyncTypedImpl is null)
                throw new InvalidOperationException("CallAsyncTypedImpl not configured.");

            var result = await CallAsyncTypedImpl(method, @params, serializerOptions, ct).ConfigureAwait(false);
            return result is null ? default : (TResult?)result;
        }

        public AppServerInitializeResult? InitializeResult => InitializeResultValue;

        public AppServerNotificationDropStats NotificationDropStats => NotificationDropStatsValue;

        public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct)
        {
            if (NotificationsImpl is null)
                return AsyncEnumerable.Empty<AppServerNotification>();
            return NotificationsImpl(ct);
        }

        public IAsyncEnumerable<AppServerRpcNotification> NotificationsRaw(CancellationToken ct)
        {
            if (NotificationsRawImpl is null)
            {
                return AsyncEnumerable.Empty<AppServerRpcNotification>();
            }

            return NotificationsRawImpl(ct);
        }

        public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task<CodexThreadListPage> ListThreadsAsync(ThreadListOptions options, CancellationToken ct) =>
            NotSupported<CodexThreadListPage>();

        public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, CancellationToken ct) =>
            NotSupported<CodexThreadReadResult>();

        public Task<CodexThreadReadResult> ReadThreadAsync(string threadId, ThreadReadOptions options, CancellationToken ct) =>
            NotSupported<CodexThreadReadResult>();

        public Task<CodexLoadedThreadListPage> ListLoadedThreadsAsync(ThreadLoadedListOptions options, CancellationToken ct) =>
            NotSupported<CodexLoadedThreadListPage>();

        public Task<ThreadUnsubscribeResult> UnsubscribeThreadAsync(string threadId, CancellationToken ct) =>
            NotSupported<ThreadUnsubscribeResult>();

        public Task CompactThreadAsync(string threadId, CancellationToken ct) =>
            CompactThreadAsyncImpl?.Invoke(threadId, ct) ?? Task.CompletedTask;

        public Task<CodexThread> RollbackThreadAsync(string threadId, int numTurns, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task CleanThreadBackgroundTerminalsAsync(string threadId, CancellationToken ct) =>
            NotSupported();

        public Task<CodexThread> ForkThreadAsync(ThreadForkOptions options, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task<ThreadArchiveResult> ArchiveThreadAsync(string threadId, CancellationToken ct) =>
            NotSupported<ThreadArchiveResult>();

        public Task<CodexThread> UnarchiveThreadAsync(string threadId, CancellationToken ct) =>
            NotSupported<CodexThread>();

        public Task SetThreadNameAsync(string threadId, string name, CancellationToken ct) =>
            NotSupported();

        public Task<SkillsListResult> ListSkillsAsync(SkillsListOptions options, CancellationToken ct) =>
            NotSupported<SkillsListResult>();

        public Task<AppsListResult> ListAppsAsync(AppsListOptions options, CancellationToken ct) =>
            NotSupported<AppsListResult>();

        public Task<ConfigRequirementsReadResult> ReadConfigRequirementsAsync(CancellationToken ct) =>
            NotSupported<ConfigRequirementsReadResult>();

        public Task<RemoteSkillsReadResult> ReadRemoteSkillsAsync(CancellationToken ct) =>
            NotSupported<RemoteSkillsReadResult>();

        public Task<RemoteSkillWriteResult> WriteRemoteSkillAsync(string hazelnutId, bool isPreload, CancellationToken ct) =>
            NotSupported<RemoteSkillWriteResult>();

        public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(SkillsConfigWriteOptions options, CancellationToken ct) =>
            NotSupported<SkillsConfigWriteResult>();

        public Task<SkillsConfigWriteResult> WriteSkillsConfigAsync(bool enabled, string path, CancellationToken ct) =>
            NotSupported<SkillsConfigWriteResult>();

        public Task<ConfigReadResult> ReadConfigAsync(ConfigReadOptions options, CancellationToken ct) =>
            NotSupported<ConfigReadResult>();

        public Task<ExternalAgentConfigDetectResult> DetectExternalAgentConfigAsync(ExternalAgentConfigDetectOptions options, CancellationToken ct) =>
            NotSupported<ExternalAgentConfigDetectResult>();

        public Task ImportExternalAgentConfigAsync(IReadOnlyList<ExternalAgentConfigMigrationItem> migrationItems, CancellationToken ct) =>
            ImportExternalAgentConfigAsyncImpl?.Invoke(migrationItems, ct) ?? Task.CompletedTask;

        public Task<AccountReadResult> ReadAccountAsync(AccountReadOptions options, CancellationToken ct) =>
            NotSupported<AccountReadResult>();

        public Task<AccountReadResult> ReadAccountAsync(CancellationToken ct) =>
            NotSupported<AccountReadResult>();

        public Task<AccountRateLimitsReadResult> ReadAccountRateLimitsAsync(CancellationToken ct) =>
            NotSupported<AccountRateLimitsReadResult>();

        public Task<ModelListResult> ListModelsAsync(ModelListOptions options, CancellationToken ct) =>
            NotSupported<ModelListResult>();

        public Task<ModelListResult> ListModelsAsync(CancellationToken ct) =>
            NotSupported<ModelListResult>();

        public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(ExperimentalFeatureListOptions options, CancellationToken ct) =>
            NotSupported<ExperimentalFeatureListResult>();

        public Task<ExperimentalFeatureListResult> ListExperimentalFeaturesAsync(CancellationToken ct) =>
            NotSupported<ExperimentalFeatureListResult>();

        public Task<ConfigWriteResult> WriteConfigValueAsync(ConfigValueWriteOptions options, CancellationToken ct) =>
            NotSupported<ConfigWriteResult>();

        public Task<ConfigWriteResult> WriteConfigBatchAsync(ConfigBatchWriteOptions options, CancellationToken ct) =>
            NotSupported<ConfigWriteResult>();

        public Task<AccountLogoutResult> LogoutAccountAsync(CancellationToken ct) =>
            NotSupported<AccountLogoutResult>();

        public Task<FeedbackUploadResult> UploadFeedbackAsync(FeedbackUploadOptions options, CancellationToken ct) =>
            NotSupported<FeedbackUploadResult>();

        public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupStartOptions options, CancellationToken ct) =>
            NotSupported<bool>();

        public Task<bool> StartWindowsSandboxSetupAsync(WindowsSandboxSetupMode mode, string? cwd, CancellationToken ct) =>
            NotSupported<bool>();

        public Task<bool> StartWindowsSandboxSetupAsync(string mode, CancellationToken ct) =>
            NotSupported<bool>();

        public Task ReloadMcpServersAsync(CancellationToken ct) =>
            ReloadMcpServersAsyncImpl?.Invoke(ct) ?? Task.CompletedTask;

        public Task<McpServerStatusListPage> ListMcpServerStatusAsync(McpServerStatusListOptions options, CancellationToken ct) =>
            NotSupported<McpServerStatusListPage>();

        public Task<McpServerOauthLoginResult> StartMcpServerOauthLoginAsync(McpServerOauthLoginOptions options, CancellationToken ct) =>
            NotSupported<McpServerOauthLoginResult>();

        public Task<AccountLoginStartResult> StartAccountLoginAsync(AccountLoginStartOptions options, CancellationToken ct) =>
            NotSupported<AccountLoginStartResult>();

        public Task<AccountLoginCancelResult> CancelAccountLoginAsync(string loginId, CancellationToken ct) =>
            NotSupported<AccountLoginCancelResult>();

        public Task StartFuzzyFileSearchSessionAsync(string sessionId, IReadOnlyList<string> roots, CancellationToken ct) =>
            StartFuzzyFileSearchSessionAsyncImpl?.Invoke(sessionId, roots, ct) ?? Task.CompletedTask;

        public Task UpdateFuzzyFileSearchSessionAsync(string sessionId, string query, CancellationToken ct) =>
            NotSupported();

        public Task<IReadOnlyList<FuzzyFileSearchResult>> FuzzyFileSearchAsync(string query, IReadOnlyList<string> roots, string? cancellationToken, CancellationToken ct) =>
            FuzzyFileSearchAsyncImpl?.Invoke(query, roots, cancellationToken, ct) ?? NotSupported<IReadOnlyList<FuzzyFileSearchResult>>();

        public Task StopFuzzyFileSearchSessionAsync(string sessionId, CancellationToken ct) =>
            NotSupported();

        public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct) =>
            NotSupported<CodexTurnHandle>();

        public Task<string> SteerTurnAsync(TurnSteerOptions options, CancellationToken ct) =>
            SteerTurnAsyncImpl?.Invoke(options, ct) ?? NotSupported<string>();

        public Task<TurnSteerResult> SteerTurnRawAsync(TurnSteerOptions options, CancellationToken ct) =>
            NotSupported<TurnSteerResult>();

        public Task<ReviewStartResult> StartReviewAsync(ReviewStartOptions options, CancellationToken ct) =>
            NotSupported<ReviewStartResult>();

        public Task<ReviewStartResult> ReviewAsync(ReviewStartOptions options, CancellationToken ct) =>
            NotSupported<ReviewStartResult>();

        public Task<PluginListResult> ListPluginsAsync(PluginListOptions options, CancellationToken ct) =>
            NotSupported<PluginListResult>();

        public Task<PluginReadResult> ReadPluginAsync(PluginReadOptions options, CancellationToken ct) =>
            NotSupported<PluginReadResult>();

        public Task<PluginInstallResult> InstallPluginAsync(PluginInstallOptions options, CancellationToken ct) =>
            NotSupported<PluginInstallResult>();

        public Task<PluginUninstallResult> UninstallPluginAsync(PluginUninstallOptions options, CancellationToken ct) =>
            NotSupported<PluginUninstallResult>();

        public Task<CommandExecResult> CommandExecAsync(CommandExecOptions options, CancellationToken ct) =>
            NotSupported<CommandExecResult>();

        public Task<CommandExecWriteResult> CommandExecWriteAsync(CommandExecWriteOptions options, CancellationToken ct) =>
            NotSupported<CommandExecWriteResult>();

        public Task<CommandExecResizeResult> CommandExecResizeAsync(CommandExecResizeOptions options, CancellationToken ct) =>
            NotSupported<CommandExecResizeResult>();

        public Task<CommandExecTerminateResult> CommandExecTerminateAsync(CommandExecTerminateOptions options, CancellationToken ct) =>
            NotSupported<CommandExecTerminateResult>();

        public Task<FsWatchResult> FsWatchAsync(FsWatchOptions options, CancellationToken ct) =>
            NotSupported<FsWatchResult>();

        public Task<FsUnwatchResult> FsUnwatchAsync(FsUnwatchOptions options, CancellationToken ct) =>
            NotSupported<FsUnwatchResult>();

        public Task<CollaborationModeListResult> ListCollaborationModesAsync(CancellationToken ct) =>
            NotSupported<CollaborationModeListResult>();

        public Task StartThreadRealtimeAsync(string threadId, string prompt, string? sessionId, CancellationToken ct) =>
            StartThreadRealtimeAsyncImpl?.Invoke(threadId, prompt, sessionId, ct) ?? Task.CompletedTask;

        public Task AppendThreadRealtimeAudioAsync(string threadId, ThreadRealtimeAudioChunk audio, CancellationToken ct) =>
            NotSupported();

        public Task AppendThreadRealtimeTextAsync(string threadId, string text, CancellationToken ct) =>
            NotSupported();

        public Task StopThreadRealtimeAsync(string threadId, CancellationToken ct) =>
            NotSupported();

        public ValueTask DisposeAsync()
        {
            _exit.TrySetResult();
            return ValueTask.CompletedTask;
        }

        private static Task NotSupported() =>
            Task.FromException(new NotSupportedException());

        private static Task<T> NotSupported<T>() =>
            Task.FromException<T>(new NotSupportedException());
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>([EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
