using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.Tests.TestHelpers;

internal static class SessionLogPathTestHelper
{
    public static string BuildNestedRolloutPath(string sessionsRoot, DateTimeOffset timestamp, SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionsRoot);

        return Path.Combine(
            sessionsRoot,
            timestamp.UtcDateTime.Year.ToString("0000"),
            timestamp.UtcDateTime.Month.ToString("00"),
            timestamp.UtcDateTime.Day.ToString("00"),
            $"rollout-{timestamp.UtcDateTime:yyyy-MM-ddTHH-mm-ss}-{sessionId.Value}.jsonl");
    }
}
