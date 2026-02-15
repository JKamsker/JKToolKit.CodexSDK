using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Tests.TestHelpers;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ThreadApiParsingTests
{
    [Fact]
    public void ParseThreadListThreads_ParsesIds_AndNextCursor()
    {
        var raw = JsonFixtures.Load("thread-list-response.json");

        var threads = CodexAppServerClient.ParseThreadListThreads(raw);
        var cursor = CodexAppServerClient.ExtractNextCursor(raw);

        threads.Select(t => t.ThreadId).Should().Equal("t_1", "t_2");
        cursor.Should().Be("cursor_2");
        threads[0].Raw.TryGetProperty("unknownField", out _).Should().BeTrue();
        threads[1].Raw.TryGetProperty("wrapperUnknown", out _).Should().BeTrue();
    }

    [Fact]
    public void ReadThreadParsing_ExtractsThreadId_FromThreadObject()
    {
        var raw = JsonFixtures.Load("thread-read-response.json");
        raw.TryGetProperty("thread", out var threadObj).Should().BeTrue();

        CodexAppServerClient.ExtractThreadId(threadObj).Should().Be("t_read");

        var summary = CodexAppServerClient.ParseThreadSummary(threadObj);
        summary.Should().NotBeNull();
        summary!.Name.Should().Be("Read Me");
    }

    [Fact]
    public void ExtractThreadId_HandlesCommonShapes()
    {
        CodexAppServerClient.ExtractThreadId(JsonFixtures.Load("thread-fork-response.json")).Should().Be("t_forked");
        CodexAppServerClient.ExtractThreadId(JsonFixtures.Load("thread-archive-response.json")).Should().Be("t_arch");
        CodexAppServerClient.ExtractThreadId(JsonFixtures.Load("thread-unarchive-response.json")).Should().Be("t_arch");
    }

    [Fact]
    public void ParseThreadLoadedListThreadIds_ParsesIds_AndNextCursor()
    {
        var raw = JsonFixtures.Load("thread-loaded-list-response.json");

        var ids = CodexAppServerClient.ParseThreadLoadedListThreadIds(raw);
        var cursor = CodexAppServerClient.ExtractNextCursor(raw);

        ids.Should().Equal("t_loaded_1", "t_loaded_2");
        cursor.Should().Be("cursor_loaded_3");
    }

    [Fact]
    public void ThreadRollbackResponse_ExtractsThreadId_FromThreadObject()
    {
        var raw = JsonFixtures.Load("thread-rollback-response.json");
        raw.TryGetProperty("thread", out var threadObj).Should().BeTrue();

        CodexAppServerClient.ExtractThreadId(threadObj).Should().Be("t_rb");
    }

    [Fact]
    public void ThreadUnarchiveResponse_CapturesExtensionData_ForForwardCompatibility()
    {
        var json = JsonFixtures.LoadText("thread-unarchive-response.json");
        var response = JsonSerializer.Deserialize<ThreadUnarchiveResponse>(json);

        response.Should().NotBeNull();
        response!.Thread.Should().NotBeNull();
        response.ExtensionData.Should().NotBeNull();
        response.ExtensionData!.Should().ContainKey("futureField");
    }
}

