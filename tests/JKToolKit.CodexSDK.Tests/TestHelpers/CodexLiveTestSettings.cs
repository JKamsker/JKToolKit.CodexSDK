using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.Tests.TestHelpers;

internal static class CodexLiveTestSettings
{
    // Omit the model unless explicitly selected: account capabilities and defaults change over time.
    public static CodexModel? Model => Environment.GetEnvironmentVariable("CODEX_E2E_MODEL") is { Length: > 0 } model
        ? CodexModel.Parse(model)
        : (CodexModel?)null;

    public static void AssertCompleted(TurnCompletedNotification completed)
    {
        completed.Status.Should().Be("completed", "turn error: {0}", completed.Error);
        completed.Error.Should().BeNull();
    }
}
