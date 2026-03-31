using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerAskForApprovalWiring
{
    internal static object? BuildAskForApproval(CodexAskForApproval? askForApproval, CodexApprovalPolicy? policy) =>
        askForApproval is { } a && (a.Policy is not null || a.Granular is not null) ? a.ToWireValue() : policy?.Value;
}
