using JKToolKit.CodexSDK.Models;

namespace JKToolKit.CodexSDK.AppServer.Internal;

internal static class CodexAppServerAskForApprovalWiring
{
    internal static object? BuildAskForApproval(CodexAskForApproval? askForApproval, CodexApprovalPolicy? policy) =>
        askForApproval is { } a && !a.Equals(default(CodexAskForApproval)) ? a.ToWireValue() : policy?.Value;
}

