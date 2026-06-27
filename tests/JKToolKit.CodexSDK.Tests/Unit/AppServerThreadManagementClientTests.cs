using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerThreadManagementClientTests
{
    [Fact]
    public async Task ListPermissionProfiles_SendsPermissionProfileList_AndParsesPage()
    {
        using var doc = JsonDocument.Parse("""
        {
          "data": [
            { "id": ":read-only", "description": "Read-only" },
            { "id": "managed" }
          ],
          "nextCursor": "next-1"
        }
        """);
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var page = await client.ListPermissionProfilesAsync(new PermissionProfileListOptions
        {
            Cwd = XPaths.JsonAbs("repo"),
            Limit = 2
        });

        rpc.LastMethod.Should().Be("permissionProfile/list");
        page.Profiles.Select(p => p.Id).Should().Equal(":read-only", "managed");
        page.NextCursor.Should().Be("next-1");
    }

    [Fact]
    public async Task SearchThreads_SendsThreadSearch_AndParsesSnippets()
    {
        using var doc = JsonDocument.Parse("""
        {
          "data": [
            {
              "snippet": "matched text",
              "thread": { "id": "thr_1", "name": "Build" }
            }
          ],
          "nextCursor": "next-1",
          "backwardsCursor": "back-1"
        }
        """);
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var page = await client.SearchThreadsAsync(new ThreadSearchOptions
        {
            SearchTerm = "matched",
            SortDirection = "desc"
        });

        rpc.LastMethod.Should().Be("thread/search");
        page.Results.Should().ContainSingle();
        page.Results[0].Thread.ThreadId.Should().Be("thr_1");
        page.Results[0].Snippet.Should().Be("matched text");
        page.BackwardsCursor.Should().Be("back-1");
    }

    [Fact]
    public async Task ListThreads_SendsParentThreadAndRecencySort()
    {
        using var doc = JsonDocument.Parse("""{"data":[],"nextCursor":null}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        await client.ListThreadsAsync(new ThreadListOptions
        {
            ParentThreadId = "parent-1",
            SortKey = "recencyAt"
        });

        rpc.LastMethod.Should().Be("thread/list");
        var json = JsonSerializer.Serialize(rpc.LastParams, CodexAppServerClient.CreateDefaultSerializerOptions());
        json.Should().Contain("\"parentThreadId\":\"parent-1\"")
            .And.Contain("\"sortKey\":\"recencyAt\"");
    }

    [Fact]
    public async Task DeleteThreadAsync_SendsThreadDelete()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc);

        var result = await client.DeleteThreadAsync("thr_1");

        rpc.LastMethod.Should().Be("thread/delete");
        var json = JsonSerializer.Serialize(rpc.LastParams, CodexAppServerClient.CreateDefaultSerializerOptions());
        json.Should().Contain("\"threadId\":\"thr_1\"");
        result.Raw.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task UpdateThreadSettings_WhenExperimentalDisabled_ThrowsBeforeSendingRequest()
    {
        var rpc = new FailingRpc();
        await using var client = CreateClient(rpc);

        var act = async () => await client.UpdateThreadSettingsAsync(new ThreadSettingsUpdateOptions
        {
            ThreadId = "thr_1",
            Model = "gpt-5"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task UpdateThreadSettings_WhenExperimentalEnabled_SendsThreadSettingsUpdate()
    {
        using var doc = JsonDocument.Parse("""{}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = CreateClient(rpc, experimentalApi: true);

        await client.UpdateThreadSettingsAsync(new ThreadSettingsUpdateOptions
        {
            ThreadId = "thr_1",
            Model = "gpt-5",
            PermissionProfileId = "managed",
            Effort = CodexReasoningEffort.High
        });

        rpc.LastMethod.Should().Be("thread/settings/update");
        var json = JsonSerializer.Serialize(rpc.LastParams, CodexAppServerClient.CreateDefaultSerializerOptions());
        json.Should().Contain("\"threadId\":\"thr_1\"")
            .And.Contain("\"model\":\"gpt-5\"")
            .And.Contain("\"permissions\":\"managed\"")
            .And.Contain("\"effort\":\"high\"");
    }

    [Fact]
    public async Task ThreadGoalMethods_SendGoalRequests_AndParseResults()
    {
        using var goalDoc = JsonDocument.Parse("""
        {
          "goal": {
            "threadId": "thr_1",
            "objective": "Ship",
            "status": "active",
            "tokenBudget": 100,
            "tokensUsed": 5,
            "timeUsedSeconds": 3,
            "createdAt": 1,
            "updatedAt": 2
          }
        }
        """);
        var rpc = new RecordingRpc { Result = goalDoc.RootElement };

        await using var client = CreateClient(rpc);

        var set = await client.SetThreadGoalAsync(new ThreadGoalSetOptions
        {
            ThreadId = "thr_1",
            Objective = "Ship",
            Status = ThreadGoalStatus.Active,
            TokenBudget = 100
        });

        rpc.LastMethod.Should().Be("thread/goal/set");
        set.Goal.Should().NotBeNull();
        set.Goal!.Status.Should().Be(ThreadGoalStatus.Active);
        set.Goal.TokensUsed.Should().Be(5);

        using var clearDoc = JsonDocument.Parse("""{"cleared":true}""");
        rpc.Result = clearDoc.RootElement;
        var clear = await client.ClearThreadGoalAsync("thr_1");

        rpc.LastMethod.Should().Be("thread/goal/clear");
        clear.Cleared.Should().BeTrue();
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc, bool experimentalApi = false) =>
        new(
            new CodexAppServerClientOptions { ExperimentalApi = experimentalApi },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion { get; } = new TaskCompletionSource().Task;
        public int? ProcessId => 1;
        public int? ExitCode => null;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }
        public required JsonElement Result { get; set; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            throw new InvalidOperationException("Request should not be sent.");
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
