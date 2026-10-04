using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerOperationConstructionTests
{
    private static Task<JsonElement> SendAsync(string method, object? parameters, CancellationToken ct) => throw new InvalidOperationException("Construction must never send requests.");

    public static IEnumerable<object[]> MissingDependencies()
    {
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerMcpClient(null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerCommandExecClient(null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerFilesystemClient(null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerSkillsAppsClient(null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerPluginsClient(null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerThreadsClient(null!, () => false))];
        yield return ["experimentalApiEnabled", (Action)(() => new CodexAppServerThreadsClient(SendAsync, null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerCollaborationModesClient(null!, () => false))];
        yield return ["experimentalApiEnabled", (Action)(() => new CodexAppServerCollaborationModesClient(SendAsync, null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerEnvironmentsClient(null!, () => false))];
        yield return ["experimentalApiEnabled", (Action)(() => new CodexAppServerEnvironmentsClient(SendAsync, null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerFuzzyFileSearchClient(null!, () => false))];
        yield return ["experimentalApiEnabled", (Action)(() => new CodexAppServerFuzzyFileSearchClient(SendAsync, null!))];
        yield return ["sendRequestAsync", (Action)(() => new CodexAppServerConfigClient(null!, () => false, NullLogger.Instance))];
        yield return ["experimentalApiEnabled", (Action)(() => new CodexAppServerConfigClient(SendAsync, null!, NullLogger.Instance))];
        yield return ["logger", (Action)(() => new CodexAppServerConfigClient(SendAsync, () => false, null!))];
    }

    [Theory]
    [MemberData(nameof(MissingDependencies))]
    public void Constructor_RejectsMissingRequiredDependency(string parameter, Action construct) =>
        construct.Should().Throw<ArgumentNullException>().WithParameterName(parameter);

    [Theory]
    [InlineData("options")]
    [InlineData("sendRequestAsync")]
    [InlineData("initializeResult")]
    [InlineData("registerTurnHandle")]
    [InlineData("removeTurnHandle")]
    [InlineData("readOnlyAccessOverridesSupport")]
    [InlineData("experimentalApiEnabled")]
    [InlineData("trackTurnStart")]
    [InlineData("sendTrackedRequestAsync")]
    public void TurnsConstructor_RejectsMissingLifecycleDependency(string missing)
    {
        Action construct = () => new CodexAppServerTurnsClient(
            options: missing == "options" ? null! : new(),
            sendRequestAsync: missing == "sendRequestAsync" ? null! : SendAsync,
            initializeResult: missing == "initializeResult" ? null! : () => null,
            registerTurnHandle: missing == "registerTurnHandle" ? null! : (_, _) => { },
            removeTurnHandle: missing == "removeTurnHandle" ? null! : _ => { },
            readOnlyAccessOverridesSupport: missing == "readOnlyAccessOverridesSupport" ? null! : new(),
            experimentalApiEnabled: missing == "experimentalApiEnabled" ? null! : () => false,
            trackTurnStart: missing == "trackTurnStart" ? null! : _ => throw new InvalidOperationException("Construction must not track turns."),
            sendTrackedRequestAsync: missing == "sendTrackedRequestAsync" ? null! : (_, _, _, _) => throw new InvalidOperationException("Construction must not dispatch."));
        construct.Should().Throw<ArgumentNullException>().WithParameterName(missing);
    }
}
