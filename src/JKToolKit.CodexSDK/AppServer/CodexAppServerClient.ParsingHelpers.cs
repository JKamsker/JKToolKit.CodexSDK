using System.Text.Json;
using JKToolKit.CodexSDK.AppServer.Internal;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    internal static string? ExtractId(JsonElement element, params string[] propertyNames) =>
        CodexAppServerClientJson.ExtractId(element, propertyNames);

    internal static string? ExtractThreadId(JsonElement result) =>
        CodexAppServerClientJson.ExtractThreadId(result);

    internal static string? ExtractTurnId(JsonElement result) =>
        CodexAppServerClientJson.ExtractTurnId(result);

    internal static string? ExtractIdByPath(JsonElement element, string p1, string p2) =>
        CodexAppServerClientJson.ExtractIdByPath(element, p1, p2);

    internal static string? FindStringPropertyRecursive(JsonElement element, string propertyName, int maxDepth) =>
        CodexAppServerClientJson.FindStringPropertyRecursive(element, propertyName, maxDepth);

    internal static IReadOnlyList<CodexThreadSummary> ParseThreadListThreads(JsonElement listResult) =>
        CodexAppServerClientThreadParsers.ParseThreadListThreads(listResult);

    internal static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject) =>
        CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject);

    internal static CodexThreadSummary? ParseThreadSummary(JsonElement threadObject, JsonElement envelope) =>
        CodexAppServerClientThreadParsers.ParseThreadSummary(threadObject, envelope);

    internal static string? ExtractNextCursor(JsonElement listResult) =>
        CodexAppServerClientThreadParsers.ExtractNextCursor(listResult);

    internal static IReadOnlyList<string> ParseThreadLoadedListThreadIds(JsonElement loadedListResult) =>
        CodexAppServerClientThreadParsers.ParseThreadLoadedListThreadIds(loadedListResult);

    internal static IReadOnlyList<SkillsListEntryResult> ParseSkillsListEntries(JsonElement skillsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListEntries(skillsListResult);

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(JsonElement skillsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(skillsListResult);

    internal static IReadOnlyList<SkillDescriptor> ParseSkillsListSkills(IReadOnlyList<SkillsListEntryResult> entries) =>
        CodexAppServerClientSkillsAppsParsers.ParseSkillsListSkills(entries);

    internal static IReadOnlyList<AppDescriptor> ParseAppsListApps(JsonElement appsListResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseAppsListApps(appsListResult);

    internal static IReadOnlyList<RemoteSkillDescriptor> ParseRemoteSkillsReadSkills(JsonElement remoteSkillsResult) =>
        CodexAppServerClientSkillsAppsParsers.ParseRemoteSkillsReadSkills(remoteSkillsResult);

    internal static ConfigRequirements? ParseConfigRequirementsReadRequirements(JsonElement configRequirementsReadResult, bool experimentalApiEnabled) =>
        CodexAppServerClientConfigRequirementsParser.ParseConfigRequirementsReadRequirements(configRequirementsReadResult, experimentalApiEnabled);
}
