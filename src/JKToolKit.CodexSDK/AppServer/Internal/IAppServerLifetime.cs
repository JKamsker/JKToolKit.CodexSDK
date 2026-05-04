namespace JKToolKit.CodexSDK.AppServer.Internal;

internal interface IAppServerLifetime : IAsyncDisposable
{
    Task Completion { get; }

    int? ProcessId { get; }

    int? ExitCode { get; }

    IReadOnlyList<string> DiagnosticTail { get; }
}
