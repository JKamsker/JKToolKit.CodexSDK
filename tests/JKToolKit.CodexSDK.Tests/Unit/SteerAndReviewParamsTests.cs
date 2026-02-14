using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SteerAndReviewParamsTests
{
    [Fact]
    public void BuildTurnSteerParams_Serializes_AsExpected()
    {
        var p = CodexAppServerClient.BuildTurnSteerParams(new TurnSteerOptions
        {
            ThreadId = "thr_123",
            ExpectedTurnId = "turn_456",
            Input = [TurnInputItem.Text("hello")]
        });

        var json = JsonSerializer.Serialize(p, CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"threadId\":\"thr_123\"");
        json.Should().Contain("\"expectedTurnId\":\"turn_456\"");
        json.Should().Contain("\"input\":[");
        json.Should().Contain("\"type\":\"text\"");
        json.Should().Contain("\"text\":\"hello\"");
    }

    [Fact]
    public void BuildReviewStartParams_Uncommitted_Serializes_AsExpected()
    {
        var p = CodexAppServerClient.BuildReviewStartParams(new ReviewStartOptions
        {
            ThreadId = "thr_123",
            Delivery = ReviewDelivery.Inline,
            Target = new ReviewTarget.UncommittedChanges()
        });

        var json = JsonSerializer.Serialize(p, CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"threadId\":\"thr_123\"");
        json.Should().Contain("\"delivery\":\"inline\"");
        json.Should().Contain("\"target\":{");
        json.Should().Contain("\"type\":\"uncommittedChanges\"");
    }

    [Fact]
    public void BuildReviewStartParams_Commit_Serializes_AsExpected()
    {
        var p = CodexAppServerClient.BuildReviewStartParams(new ReviewStartOptions
        {
            ThreadId = "thr_123",
            Delivery = ReviewDelivery.Detached,
            Target = new ReviewTarget.Commit("abc1234", "Title")
        });

        var json = JsonSerializer.Serialize(p, CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"delivery\":\"detached\"");
        json.Should().Contain("\"type\":\"commit\"");
        json.Should().Contain("\"sha\":\"abc1234\"");
        json.Should().Contain("\"title\":\"Title\"");
    }

    [Fact]
    public void BuildReviewStartParams_BaseBranch_Serializes_AsExpected()
    {
        var p = CodexAppServerClient.BuildReviewStartParams(new ReviewStartOptions
        {
            ThreadId = "thr_123",
            Target = new ReviewTarget.BaseBranch("main")
        });

        var json = JsonSerializer.Serialize(p, CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"baseBranch\"");
        json.Should().Contain("\"branch\":\"main\"");
        json.Should().NotContain("\"delivery\":");
    }

    [Fact]
    public void BuildReviewStartParams_Custom_Serializes_AsExpected()
    {
        var p = CodexAppServerClient.BuildReviewStartParams(new ReviewStartOptions
        {
            ThreadId = "thr_123",
            Target = new ReviewTarget.Custom("do it")
        });

        var json = JsonSerializer.Serialize(p, CodexAppServerClient.CreateDefaultSerializerOptions());

        json.Should().Contain("\"type\":\"custom\"");
        json.Should().Contain("\"instructions\":\"do it\"");
    }
}

