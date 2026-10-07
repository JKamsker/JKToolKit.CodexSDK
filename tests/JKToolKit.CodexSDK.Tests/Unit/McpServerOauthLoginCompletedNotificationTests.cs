using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpServerOauthLoginCompletedNotificationTests
{
    [Fact]
    public void LegacyConstructor_LeavesThreadIdUnset()
    {
        var raw = JsonDocument.Parse("""{"name":"server-1","success":true}""").RootElement;

        var notification = new McpServerOauthLoginCompletedNotification(
            "server-1",
            true,
            null,
            raw);

        notification.Name.Should().Be("server-1");
        notification.ThreadId.Should().BeNull();
        notification.LoginId.Should().BeNull();
        notification.Success.Should().BeTrue();
        notification.Error.Should().BeNull();
        notification.Params.Should().Be(raw);
    }

    [Fact]
    public void CorrelatedConstructor_PreservesLoginId()
    {
        var raw = JsonDocument.Parse("""{"name":"server-1","loginId":"login-1","success":true}""").RootElement;

        var notification = new McpServerOauthLoginCompletedNotification(
            "server-1",
            "thread-1",
            "login-1",
            true,
            null,
            raw);

        notification.LoginId.Should().Be("login-1");
    }
}
