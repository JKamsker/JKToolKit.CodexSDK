using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ItemCompletedContentTokenCountTests
{
    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("42", 42UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void Map_PreservesNativeContentTokenCount(string count, ulong expected)
    {
        using var document = JsonDocument.Parse(
            """{"threadId":"thread-1","turnId":"turn-1","item":{"id":"item-1","type":"agentMessage","text":"done"},"contentTokenCount":COUNT}"""
                .Replace("COUNT", count));

        var notification = AppServerNotificationMapper.Map("item/completed", document.RootElement)
            .Should().BeOfType<ItemCompletedNotification>().Subject;

        notification.ContentTokenCount.Should().Be(expected);
        notification.Item.TryGetProperty("contentTokenCount", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"contentTokenCount\":null}")]
    [InlineData("{\"contentTokenCount\":-1}")]
    [InlineData("{\"contentTokenCount\":1.5}")]
    [InlineData("{\"contentTokenCount\":\"42\"}")]
    [InlineData("{\"contentTokenCount\":18446744073709551616}")]
    public void ContentTokenCount_IsNullWhenUnavailableOrMalformed(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var notification = new ItemCompletedNotification("thread-1", "turn-1", default, document.RootElement);

        notification.ContentTokenCount.Should().BeNull();
    }
}
